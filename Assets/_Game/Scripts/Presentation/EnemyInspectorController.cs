using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Veilward.Domain;

namespace Veilward.Presentation
{
    public sealed class EnemyInspectorController : MonoBehaviour
    {
        [SerializeField] private BattleSceneController battle;
        [SerializeField] private BattlePlacementController placement;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text detailsText;
        [SerializeField] private Button closeButton;
        private readonly StringBuilder _text = new StringBuilder(512);
        private long _displayedTick = -1;
        public EnemyId? SelectedEnemyId { get; private set; }
        public string Details => detailsText != null ? detailsText.text : string.Empty;

        private void Start()
        {
            if (battle == null || placement == null || worldCamera == null || panel == null || detailsText == null || closeButton == null)
                throw new System.InvalidOperationException("Enemy inspector requires all scene references.");
            closeButton.onClick.AddListener(Close);
            Close();
        }
        private void OnDestroy() { if (closeButton != null) closeButton.onClick.RemoveListener(Close); }
        private void LateUpdate() => Refresh();

        public bool Select(EnemyId id)
        {
            if (!battle.CanAcceptInput(BattleInputOwner.Inspector) || Find(id) == null) return false;
            placement.ClearSelection();
            SelectedEnemyId = id;
            _displayedTick = -1;
            Refresh();
            return true;
        }
        public bool TrySelectAt(Vector2 screenPosition)
        {
            if (!battle.CanAcceptInput(BattleInputOwner.Inspector)) return false;
            var enemies = battle.Simulation.State.Enemies;
            EnemyState nearest = null; var best = 32f * 32f;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i]; if (!enemy.IsAlive) continue;
                var p = battle.Simulation.Path.GetPosition(enemy.DistanceAlongPath);
                var screen = worldCamera.WorldToScreenPoint(new Vector3(p.X, .6f, p.Y));
                if (screen.z <= 0) continue;
                var distance = ((Vector2)screen - screenPosition).sqrMagnitude;
                if (distance < best || (distance == best && nearest != null && enemy.Id.Value < nearest.Id.Value))
                { nearest = enemy; best = distance; }
            }
            return nearest != null && Select(nearest.Id);
        }
        public void Close()
        {
            if (battle != null && !battle.CanAcceptInput(BattleInputOwner.Inspector)) return;
            ClearSelection();
        }
        internal void ClearSelection()
        {
            SelectedEnemyId = null;
            _displayedTick = -1;
            if (panel != null) panel.SetActive(false);
            if (detailsText != null) detailsText.text = string.Empty;
            if (placement != null) placement.SuppressPointerForCurrentFrame();
        }
        public void Refresh()
        {
            var enemy = SelectedEnemyId.HasValue ? Find(SelectedEnemyId.Value) : null;
            if (enemy == null) { if (SelectedEnemyId.HasValue) ClearSelection(); return; }
            panel.SetActive(!battle.IsPaused && !placement.IsSellConfirmationVisible);
            var state = battle.Simulation.State;
            if (_displayedTick == state.Tick) return;
            _displayedTick = state.Tick;
            _text.Clear();
            _text.Append(enemy.Definition.Id.Value == BossHudController.BossId ? "RIFT WARDEN" : "SHADE");
            _text.Append("   HP ").Append(enemy.Health).Append('/').Append(enemy.Definition.MaxHealth)
                .Append("   Armor ").Append(enemy.Definition.Armor).Append('\n');

            _text.Append("Effects: ");
            if (enemy.Statuses.Count == 0) _text.Append("None");
            for (var i = 0; i < enemy.Statuses.Count; i++)
            {
                var status = enemy.Statuses[i];
                if (i == 2) _text.Append('\n');
                else if (i != 0) _text.Append(" · ");
                _text.Append(status.Kind).Append(' ').Append(status.Power)
                    .Append(" (").Append(Seconds(status.RemainingTicks)).Append("s)");
            }
            var steam = ReactionPresentationState.Cooldown(enemy, ReactionKind.Steam, state.Tick, battle.Simulation.ReactionCatalog);
            var storm = ReactionPresentationState.Cooldown(enemy, ReactionKind.Storm, state.Tick, battle.Simulation.ReactionCatalog);
            _text.Append("\nREACTIONS\nSteam ").Append(steam == 0 ? "ready" : Seconds(steam) + "s")
                .Append(" · Storm ").Append(storm == 0 ? "ready" : Seconds(storm) + "s").Append('\n');
            var memory = battle.Presentation.GetMemory(enemy.Id);
            Append(memory?.Steam);
            Append(memory?.Storm);
            if (memory == null) _text.Append("No reactions yet");
            var text = _text.ToString();
            if (detailsText.text != text) detailsText.text = text;
        }

        private static string Seconds(long ticks) =>
            (ticks / (float)SimulationTiming.TicksPerSecond).ToString("0.#", CultureInfo.InvariantCulture);

        private void Append(ReactionExplanation? explanation)
        {
            if (!explanation.HasValue) return;
            var e = explanation.Value;
            var steam = e.Reaction.Kind == ReactionKind.Steam;
            var cause = e.Reaction.TriggerKind == ReactionTriggerKind.Intrinsic ? "Innate attack" :
                steam ? "Flame + Tide" : "Tide + Gale";
            _text.Append(steam ? "Steam · " : "Storm · ").Append(cause).Append('\n')
                .Append(e.HitCount).Append(" hit").Append(e.HitCount == 1 ? " · " : "s · ").Append(e.AppliedHpLoss).Append(" HP lost\n");
        }

        private EnemyState Find(EnemyId id)
        {
            if (battle.Simulation == null) return null;
            var enemies = battle.Simulation.State.Enemies;
            for (var i = 0; i < enemies.Count; i++) if (enemies[i].Id == id && enemies[i].IsAlive) return enemies[i];
            return null;
        }
    }
}
