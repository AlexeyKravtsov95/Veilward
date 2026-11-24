using UnityEngine;
using UnityEngine.UI;
using Veilward.Domain;
namespace Veilward.Presentation
{
    public sealed class BossHudController : MonoBehaviour
    {
        public const string BossId = "rift-warden";
        [SerializeField] private BattleSceneController battle;
        [SerializeField] private BattlePlacementController placement;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text noticeText;
        [SerializeField] private Text healthText;
        [SerializeField] private Image healthFill;
        private EnemyState _tracked;
        private BattleSimulation _session;
        private string _outcome;
        private EnemyDefinition _displayedDefinition;
        private int _displayedHealth = -2;
        private BattlePhase _displayedPhase;
        private string _displayedOutcome;
        public string Notice => noticeText.text;
        public string Health => healthText.text;
        public bool HealthVisible => healthText.gameObject.activeSelf;
        internal bool IsVisible => panel.activeInHierarchy;
        internal RectTransform PanelRect => panel.GetComponent<RectTransform>();
        private void Start()
        {
            if (battle == null || panel == null || noticeText == null || healthText == null || healthFill == null)
                throw new System.InvalidOperationException("Boss HUD requires all scene references.");
            Refresh();
        }
        private void LateUpdate() => Refresh();
        internal void ResetView() { _tracked = null; _session = null; _outcome = null; Refresh(); }
        public void Refresh()
        {
            var s = battle != null ? battle.Simulation : null;
            if (s != _session) { _session = s; _tracked = null; _outcome = null; _displayedDefinition = null; }
            if (s == null) { panel.SetActive(false); return; }
            var wave = s.Waves[s.State.WaveIndex];
            EnemyDefinition definition = null;
            for (var i = 0; i < wave.Schedule.Count; i++)
                if (wave.Schedule[i].Enemy.Id.Value == BossId)
                { definition = wave.Schedule[i].Enemy; break; }
            panel.SetActive(definition != null && !battle.IsPaused);
            if (definition == null) return;
            var rect = PanelRect;
            var dockRight = placement != null && placement.ShowsBuildDrawer && placement.BuildDrawerOnLeft;
            var anchor = dockRight ? Vector2.one : Vector2.up;
            if (rect.anchorMin != anchor)
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
                rect.anchoredPosition = new Vector2(dockRight ? -10 : 10, -78);
            }
            EnemyState boss = null;
            for (var i = 0; i < s.State.Enemies.Count; i++)
            {
                var enemy = s.State.Enemies[i];
                if (enemy.Definition.Id.Value == BossId && enemy.IsAlive) { boss = enemy; break; }
            }
            if (boss != null) _tracked = boss;
            else if (_tracked != null)
            {
                _outcome = _tracked.IsAlive ? $"The Warden reached the Heart.\n{definition.LeakDamage} damage." :
                    $"Warden defeated · +{definition.Reward} gold";
                _tracked = null;
            }
            healthText.gameObject.SetActive(boss != null);
            healthFill.transform.parent.gameObject.SetActive(boss != null);
            var hp = boss != null ? boss.Health : -1;
            if (_displayedDefinition == definition && _displayedHealth == hp && _displayedPhase == s.State.Phase && _displayedOutcome == _outcome) return;
            _displayedDefinition = definition; _displayedHealth = hp; _displayedPhase = s.State.Phase; _displayedOutcome = _outcome;
            if (boss != null)
            {
                healthText.text = $"HP {boss.Health}/{definition.MaxHealth}   ·   Armor {definition.Armor}";
                healthFill.rectTransform.anchorMax = new Vector2((float)boss.Health / definition.MaxHealth, 1);
                noticeText.text = $"Rift Warden\nDeals {definition.LeakDamage} if it reaches the Heart.";
            }
            else if (_outcome != null) noticeText.text = _outcome;
            else if (s.State.Phase == BattlePhase.Victory) noticeText.text = "Rift Warden defeated.";
            else if (s.State.Phase == BattlePhase.Defeat) noticeText.text = "The Heart fell.";
            else noticeText.text = $"Rift Warden incoming · {definition.MaxHealth} HP\nDeals {definition.LeakDamage} if it reaches the Heart.";
        }
    }
}
