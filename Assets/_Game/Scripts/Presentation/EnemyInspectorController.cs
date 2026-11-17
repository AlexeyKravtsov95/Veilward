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
        private Canvas _canvas;
        private Vector2 _origin;
        private long _displayedTick = -1;
        public EnemyId? SelectedEnemyId { get; private set; }
        public string Details => detailsText != null ? detailsText.text : string.Empty;

        private void Start()
        {
            if (battle == null || placement == null || worldCamera == null || panel == null || detailsText == null || closeButton == null)
                throw new System.InvalidOperationException("Enemy inspector requires all scene references.");
            _canvas = panel.GetComponentInParent<Canvas>();
            _origin = panel.GetComponent<RectTransform>().anchoredPosition;
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
            var scale = _canvas != null ? _canvas.scaleFactor : 1;
            if (scale > 0)
                panel.GetComponent<RectTransform>().anchoredPosition = _origin + new Vector2(
                    -Mathf.Max(0, Screen.width - Screen.safeArea.xMax), Mathf.Max(0, Screen.safeArea.yMin)) / scale;
            var state = battle.Simulation.State;
            if (_displayedTick == state.Tick) return;
            _displayedTick = state.Tick;
            _text.Clear(); _text.Append("Враг #").Append(enemy.Id.Value).Append("  HP ").Append(enemy.Health).Append('\n');
            for (var i = 0; i < 4; i++)
                for (var j = 0; j < enemy.Statuses.Count; j++)
                {
                    var status = enemy.Statuses[j]; if ((int)status.Kind != i) continue;
                    _text.Append(status.Kind).Append(": power ").Append(status.Power).Append(", ")
                        .Append(status.RemainingTicks).Append(" ticks\n");
                }
            if (enemy.Statuses.Count == 0) _text.Append("Активных статусов нет\n");
            _text.Append("Steam CD: ").Append(ReactionPresentationState.Cooldown(enemy, ReactionKind.Steam, state.Tick, battle.Simulation.ReactionCatalog))
                .Append(" ticks   Storm CD: ").Append(ReactionPresentationState.Cooldown(enemy, ReactionKind.Storm, state.Tick, battle.Simulation.ReactionCatalog)).Append(" ticks\n");
            var memory = battle.Presentation.GetMemory(enemy.Id);
            Append(memory?.Steam); Append(memory?.Storm);
            if (memory == null) _text.Append("Реакций ещё не было");
            var text = _text.ToString(); if (detailsText.text != text) detailsText.text = text;
        }
        private void Append(ReactionExplanation? explanation)
        {
            if (!explanation.HasValue) return;
            var e = explanation.Value;
            _text.Append(e.Cause).Append("\n").Append(e.HitCount).Append(" hits, AppliedHpLoss ").Append(e.AppliedHpLoss)
                .Append(" HP всего; tick ").Append(e.Reaction.Tick).Append('\n');
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
