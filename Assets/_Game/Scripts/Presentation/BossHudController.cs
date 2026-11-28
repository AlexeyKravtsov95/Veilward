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
        [SerializeField] private EnemyInspectorController inspector;
        [SerializeField] private Button inspectButton;
        [SerializeField] private EnemyStatusBadgePair badges;
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
        public EnemyId? BadgeOwner { get; private set; }
        public int VisibleBadgeCount => IsVisible ? badges.VisibleCount : 0;
        internal bool IsVisible => panel.activeInHierarchy;
        internal RectTransform PanelRect => panel.GetComponent<RectTransform>();
        private void Start()
        {
            if (battle == null || panel == null || noticeText == null || healthText == null || healthFill == null ||
                inspector == null || inspectButton == null || badges == null)
                throw new System.InvalidOperationException("Boss HUD requires all scene references.");
            inspectButton.onClick.AddListener(Inspect);
            Refresh();
        }
        private void OnDestroy() { if (inspectButton != null) inspectButton.onClick.RemoveListener(Inspect); }

        private void Inspect()
        {
            Refresh();
            if (BadgeOwner.HasValue) inspector.Select(BadgeOwner.Value);
        }

        internal void ResetView() { _tracked = null; _session = null; _outcome = null; Refresh(); }
        public void Refresh()
        {
            var s = battle != null ? battle.Simulation : null;
            if (s != _session) { _session = s; _tracked = null; _outcome = null; _displayedDefinition = null; }
            if (s == null) { RefreshBadges(null, null); panel.SetActive(false); return; }
            var wave = s.Waves[s.State.WaveIndex];
            EnemyDefinition definition = null;
            for (var i = 0; i < wave.Schedule.Count; i++)
                if (wave.Schedule[i].Enemy.Id.Value == BossId)
                { definition = wave.Schedule[i].Enemy; break; }
            var building = s.State.Phase == BattlePhase.Preparation && placement != null && placement.ShowsBuildDrawer;
            panel.SetActive(definition != null && !battle.IsPaused && !building);
            EnemyState boss = null;
            if (definition != null)
            {
                for (var i = 0; i < s.State.Enemies.Count; i++)
                {
                    var enemy = s.State.Enemies[i];
                    if (enemy.Definition.Id.Value == BossId && enemy.IsAlive) { boss = enemy; break; }
                }
            }
            RefreshBadges(s, s.State.Phase == BattlePhase.Combat ? boss : null);
            if (definition == null) return;
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
                noticeText.text = $"Rift Warden · {definition.LeakDamage} to Heart";
            }
            else if (_outcome != null) noticeText.text = _outcome;
            else if (s.State.Phase == BattlePhase.Victory) noticeText.text = "Rift Warden defeated.";
            else if (s.State.Phase == BattlePhase.Defeat) noticeText.text = "The Heart fell.";
            else noticeText.text = $"Rift Warden incoming\n{definition.MaxHealth} HP · Deals {definition.LeakDamage} to Heart";
        }

        private void RefreshBadges(BattleSimulation simulation, EnemyState boss)
        {
            BadgeOwner = boss?.Id;
            inspectButton.interactable = boss != null && battle.CanAcceptInput(BattleInputOwner.Inspector);
            var presentation = battle != null ? battle.Presentation : null;
            badges.Display(boss == null ? default : EnemyStatusView.Describe(boss, simulation.State.Tick,
                simulation.ReactionCatalog, presentation.GetMemory(boss.Id), presentation.ActiveTime));
        }
    }
}
