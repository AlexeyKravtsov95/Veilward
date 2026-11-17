using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Veilward.Domain;

namespace Veilward.Presentation
{
    public sealed class ElementChoiceController : MonoBehaviour
    {
        [SerializeField] private BattleSceneController battle;
        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform safeContent;
        [SerializeField] private Text titleText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text progressionText;
        [SerializeField] private Button[] optionButtons;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button menuButton;
        private static readonly string[] OptionIds = { "flame", "tide", "gale", "stone" };
        private readonly List<string> _options = new List<string>(4);
        private ElementProgressionState _observedProgression;
        private ElementProgressionState _previewProgression;
        private string _previewChoiceId;
        private bool _dirty = true;
        private bool _lastAllowed;
        private Rect _lastSafeArea;
        private Vector2 _lastScreenSize;
        public string SelectedOptionId { get; private set; }
        public IReadOnlyList<string> Options => _options;
        public bool IsVisible => panel != null && panel.activeSelf;

        private void Start()
        {
            if (battle == null || panel == null || safeContent == null || titleText == null || descriptionText == null ||
                progressionText == null || optionButtons == null || optionButtons.Length != 4 ||
                confirmButton == null || cancelButton == null || menuButton == null)
                throw new InvalidOperationException("Element choice requires all scene references.");
            for (var i = 0; i < optionButtons.Length; i++)
            {
                var id = OptionIds[i];
                optionButtons[i].onClick.AddListener(() => SelectOption(id));
            }
            confirmButton.onClick.AddListener(HandleConfirm);
            cancelButton.onClick.AddListener(CancelSelection);
            menuButton.onClick.AddListener(ReturnToMenu);
            Refresh();
        }
        private void LateUpdate() => Refresh();
        private void OnDestroy()
        {
            if (confirmButton != null) confirmButton.onClick.RemoveListener(HandleConfirm);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(CancelSelection);
            if (menuButton != null) menuButton.onClick.RemoveListener(ReturnToMenu);
        }
        private void HandleConfirm() => ConfirmChoice();

        public bool SelectOption(string optionId)
        {
            Refresh();
            if (!battle.CanAcceptInput(BattleModalOwner.ElementChoice) || !_options.Contains(optionId)) return false;
            SelectedOptionId = optionId; _dirty = true;
            _previewProgression = battle.Simulation.State.ElementProgression;
            _previewChoiceId = _previewProgression.PendingChoiceId;
            Refresh();
            return true;
        }
        public ElementChoiceResult ConfirmChoice()
        {
            var progression = battle.Simulation.State.ElementProgression;
            if (!battle.CanAcceptInput(BattleModalOwner.ElementChoice))
                return new ElementChoiceResult(ElementChoiceFailureReason.InvalidPhase);
            // Не переносим preview на другое pending, даже если Refresh ещё не отработал.
            if (_previewProgression != progression || _previewChoiceId != progression.PendingChoiceId || SelectedOptionId == null)
            {
                ClearPreview(); Refresh();
                return new ElementChoiceResult(ElementChoiceFailureReason.ChoiceMismatch);
            }
            var command = new ChooseElementCommand(_previewChoiceId, SelectedOptionId);
            ClearPreview();
            var result = battle.ChooseElement(command);
            Refresh();
            return result;
        }
        public void CancelSelection()
        {
            if (!battle.CanAcceptInput(BattleModalOwner.ElementChoice)) return;
            ClearPreview(); Refresh();
        }
        internal void ClearPreview()
        {
            if (SelectedOptionId != null || _previewProgression != null) _dirty = true;
            SelectedOptionId = null; _previewProgression = null; _previewChoiceId = null;
        }
        private void ReturnToMenu()
        {
            if (battle.CanAcceptInput(BattleModalOwner.ElementChoice)) SceneManager.LoadScene("MainMenu");
        }
        public void Refresh()
        {
            if (battle == null || battle.Simulation == null) return;
            var progression = battle.Simulation.State.ElementProgression;
            var allowed = battle.CanAcceptInput(BattleModalOwner.ElementChoice);
            var size = new Vector2(Screen.width, Screen.height);
            if (_observedProgression != progression || battle.IsPaused) ClearPreview();
            if (!_dirty && _observedProgression == progression && _lastAllowed == allowed &&
                _lastSafeArea == Screen.safeArea && _lastScreenSize == size) return;
            _observedProgression = progression;
            _lastAllowed = allowed; _lastSafeArea = Screen.safeArea; _lastScreenSize = size;
            _dirty = false;
            _options.Clear();
            var pending = progression.PendingChoiceId != "none";
            if (pending)
            {
                for (var i = 0; i < OptionIds.Length; i++)
                {
                    if (progression.PendingChoiceId == "start-resonance" ? i == 0 || i == 2 : !Contains(progression.UnlockedElements, (ElementKind)i))
                        _options.Add(OptionIds[i]);
                }
            }
            if (panel == null) return;
            panel.SetActive(pending && allowed);
            if (pending && allowed) battle.ClearWorldSelection();
            var start = progression.PendingChoiceId == "start-resonance";
            titleText.text = start ? "Выберите стартовую стихию" : $"Волна {battle.Simulation.State.WaveIndex + 1} пройдена — откройте стихию";
            descriptionText.text = "Tap — только предварительный выбор. Confirm — открыть стихию.\n" +
                (SelectedOptionId == null ? "Стихия ещё не выбрана. Cancel очищает выбор; решение обязательно." : $"Выбрано: {SelectedOptionId.ToUpperInvariant()}. Подтвердите открытие.");
            for (var i = 0; i < optionButtons.Length; i++)
            {
                optionButtons[i].gameObject.SetActive(_options.Contains(OptionIds[i]));
                optionButtons[i].interactable = allowed && pending;
                optionButtons[i].GetComponentInChildren<Text>(true).text = (SelectedOptionId == OptionIds[i] ? "✓ " : "") + OptionIds[i].ToUpperInvariant();
            }
            confirmButton.interactable = allowed && pending && SelectedOptionId != null;
            cancelButton.interactable = allowed && pending;
            menuButton.interactable = allowed && pending;
            progressionText.text = progression.RulesId == "legacy-all-open-v1"
                ? "Продолжение по прежним правилам — все стихии открыты"
                : "Открыто: " + UnlockedText(progression) + "\n" +
                    (progression.ChoicesSpent >= 2 ? "Все открытия получены. Следующих наград нет." : "Открытие новой стихии после волн 3 и 6.");
            ApplySafeArea(Screen.safeArea, new Vector2(Screen.width, Screen.height));
        }
        private void ApplySafeArea(Rect area, Vector2 size)
        {
            if (safeContent == null || size.x <= 0 || size.y <= 0) return;
            safeContent.anchorMin = new Vector2(area.xMin / size.x, area.yMin / size.y);
            safeContent.anchorMax = new Vector2(area.xMax / size.x, area.yMax / size.y);
            safeContent.offsetMin = safeContent.offsetMax = Vector2.zero;
        }
        private static bool Contains(IReadOnlyList<ElementKind> elements, ElementKind element)
        {
            for (var i = 0; i < elements.Count; i++) if (elements[i] == element) return true;
            return false;
        }
        private static string UnlockedText(ElementProgressionState p)
        {
            if (p.UnlockedElements.Count == 0) return "нет";
            var names = new string[p.UnlockedElements.Count];
            for (var i = 0; i < names.Length; i++) names[i] = p.UnlockedElements[i].ToString().ToUpperInvariant();
            return string.Join(" / ", names);
        }
    }
}
