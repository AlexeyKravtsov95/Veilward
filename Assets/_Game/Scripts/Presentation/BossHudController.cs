using UnityEngine;
using UnityEngine.UI;
using Veilward.Domain;
namespace Veilward.Presentation
{
    public sealed class BossHudController : MonoBehaviour
    {
        public const string BossId = "rift-warden";
        [SerializeField] private BattleSceneController battle;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text noticeText;
        [SerializeField] private Text healthText;
        [SerializeField] private Image healthFill;
        private Canvas _canvas;
        private BattleHudSafeArea _safeArea;
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
        private void FitLayout()
        {
            if (_canvas == null) { _canvas = panel.GetComponentInParent<Canvas>(); _safeArea = _canvas.GetComponent<BattleHudSafeArea>(); }
            var scale = _canvas.scaleFactor;
            if (scale <= 0) return;
            var size = _safeArea != null && _safeArea.AppliedScreenSize.x > 0 ? _safeArea.AppliedScreenSize : new Vector2(Screen.width, Screen.height);
            var area = _safeArea != null && _safeArea.AppliedArea.width > 0 ? _safeArea.AppliedArea : Screen.safeArea;
            var canvasWidth = size.x / scale;
            // Между существующими Stats440 и правыми controls230, с safe-area и зазором16.
            var left = 24 + 440 + area.xMin / scale + 16;
            var right = canvasWidth - 24 - 230 - (size.x - area.xMax) / scale - 16;
            var width = Mathf.Max(200, Mathf.Min(720, right - left));
            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
            rect.anchoredPosition = new Vector2((left + right) * .5f - canvasWidth * .5f, rect.anchoredPosition.y);
            noticeText.rectTransform.sizeDelta = new Vector2(width - 20, noticeText.rectTransform.sizeDelta.y);
            healthText.rectTransform.sizeDelta = new Vector2(width - 20, healthText.rectTransform.sizeDelta.y);
            var bar = (RectTransform)healthFill.transform.parent;
            bar.sizeDelta = new Vector2(width - 40, bar.sizeDelta.y);
        }
        private void LateUpdate() => Refresh();
        internal void ResetView() { _tracked = null; _session = null; _outcome = null; Refresh(); }
        public void Refresh()
        {
            var s = battle != null ? battle.Simulation : null;
            if (s != _session) { _session = s; _tracked = null; _outcome = null; _displayedDefinition = null; }
            if (s == null) { panel.SetActive(false); return; }
            var wave = s.Waves[s.State.WaveIndex];
            EnemyDefinition definition = null; long spawnTick = 0;
            for (var i = 0; i < wave.Schedule.Count; i++)
                if (wave.Schedule[i].Enemy.Id.Value == BossId)
                { definition = wave.Schedule[i].Enemy; spawnTick = wave.Schedule[i].RelativeTick; break; }
            panel.SetActive(definition != null && !battle.IsPaused);
            if (definition == null) return;
            FitLayout();
            EnemyState boss = null;
            foreach (var enemy in s.State.Enemies)
                if (enemy.Definition.Id.Value == BossId && enemy.IsAlive) { boss = enemy; break; }
            if (boss != null) _tracked = boss;
            else if (_tracked != null)
            {
                _outcome = _tracked.IsAlive ? "Страж достиг Сердца: 20 урона. Нет награды за босса и бонуса волны." :
                    "Страж побеждён: +50 gold. Бонус +20 только после всех врагов.";
                _tracked = null;
            }
            healthText.gameObject.SetActive(boss != null);
            healthFill.transform.parent.gameObject.SetActive(boss != null);
            var hp = boss != null ? boss.Health : -1;
            if (_displayedDefinition == definition && _displayedHealth == hp && _displayedPhase == s.State.Phase && _displayedOutcome == _outcome) return;
            _displayedDefinition = definition; _displayedHealth = hp; _displayedPhase = s.State.Phase; _displayedOutcome = _outcome;
            if (boss != null)
            {
                healthText.text = $"Страж Разрыва  •  HP {boss.Health}/{definition.MaxHealth}  •  Armor {definition.Armor}";
                healthFill.rectTransform.anchorMax = new Vector2((float)boss.Health / definition.MaxHealth, 1);
                noticeText.text = "Пропуск: 20 урона — разрушит Сердце. Убийство: +50 gold. Статусы и Печать действуют.";
            }
            else if (_outcome != null) noticeText.text = _outcome;
            else if (s.State.Phase == BattlePhase.Victory) noticeText.text = "Страж побеждён: +50 gold. Все враги пройдены: +20 gold. Victory.";
            else if (s.State.Phase == BattlePhase.Defeat) noticeText.text = "Сердце разрушено. Пропуск Стража: 20 урона; без награды и бонуса волны.";
            else noticeText.text = $"Волна 8: 11 Shade + Страж Разрыва (spawn {spawnTick}, 11 с).\n" +
                $"HP {definition.MaxHealth} • Armor {definition.Armor} • Speed {definition.Speed:0.#} • Kill +{definition.Reward} • Wave +20\n" +
                "Пропуск: 20 урона — разрушит Сердце. Статусы и Печать действуют.";
        }
    }
}
