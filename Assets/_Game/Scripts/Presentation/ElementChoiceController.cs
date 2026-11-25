using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Veilward.Domain;

namespace Veilward.Presentation
{
    public sealed class ElementChoiceController : MonoBehaviour
    {
        [SerializeField] private BattleSceneController battle;
        [SerializeField] private BattlePlacementController placement;
        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform safeAreaRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Text summaryText;
        [SerializeField] private Button[] optionButtons;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        private static readonly string[] Ids = { "flame", "tide", "gale", "stone" };
        private ElementProgressionState _observed;
        private string _previewChoiceId;
        private bool _sending;
        private bool _dirty = true;
        private bool _wasPaused, _wasAllowed;
        public string PreviewOptionId { get; private set; }
        public IReadOnlyList<Button> OptionButtons => optionButtons;

        private void Start()
        {
            if (battle == null || placement == null || panel == null || safeAreaRoot == null || titleText == null ||
                summaryText == null || optionButtons == null || optionButtons.Length != 4 || confirmButton == null || cancelButton == null)
                throw new InvalidOperationException("Element choice requires all scene references and four canonical options.");
            for (var i = 0; i < optionButtons.Length; i++)
            {
                var id = Ids[i]; optionButtons[i].onClick.AddListener(() => SelectOption(id));
            }
            confirmButton.onClick.AddListener(HandleConfirm);
            cancelButton.onClick.AddListener(CancelSelection);
            Refresh();
        }
        private void Update() => Refresh();
        private void OnDestroy()
        {
            if (confirmButton != null) confirmButton.onClick.RemoveListener(HandleConfirm);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(CancelSelection);
            // Option buttons принадлежат только этому controller и уничтожаются вместе со сценой.
        }
        private void HandleConfirm() => ConfirmChoice();
        public bool SelectOption(string optionId)
        {
            Refresh();
            if (_sending || !battle.CanAcceptInput(BattleInputOwner.ElementChoice) || !IsOption(optionId)) return false;
            _dirty = true;
            PreviewOptionId = optionId;
            _previewChoiceId = battle.Simulation.State.ElementProgression.PendingChoiceId;
            Refresh(); return true;
        }
        public ElementChoiceResult ConfirmChoice()
        {
            Refresh();
            var progression = battle.Simulation.State.ElementProgression;
            if (_sending || !battle.CanAcceptInput(BattleInputOwner.ElementChoice) || PreviewOptionId == null ||
                _previewChoiceId != progression.PendingChoiceId || !IsOption(PreviewOptionId))
                return new ElementChoiceResult(ElementChoiceFailureReason.InvalidOption);
            var command = new ChooseElementCommand(_previewChoiceId, PreviewOptionId);
            _sending = true;
            ClearPreview();
            try { return battle.ChooseElement(command); }
            finally { _sending = false; placement.SuppressPointerForCurrentFrame(); Refresh(); }
        }
        public void CancelSelection()
        {
            if (!battle.CanAcceptInput(BattleInputOwner.ElementChoice)) return;
            ClearPreview(); placement.SuppressPointerForCurrentFrame(); Refresh();
        }
        internal void ClearPreview() { PreviewOptionId = null; _previewChoiceId = null; _dirty = true; }
        private bool IsOption(string id)
        {
            var p = battle.Simulation.State.ElementProgression;
            var index = Array.IndexOf(Ids, id);
            if (index < 0 || p.PendingChoiceId == "none") return false;
            if (p.PendingChoiceId == "start-resonance") return id == "flame" || id == "gale";
            foreach (var element in p.UnlockedElements) if ((int)element == index) return false;
            return true;
        }
        public void Refresh()
        {
            if (battle == null || battle.Simulation == null) return;
            var p = battle.Simulation.State.ElementProgression;
            var changed = !ReferenceEquals(p, _observed);
            var paused = battle.IsPaused;
            if (changed || (paused && !_wasPaused)) ClearPreview();
            var pending = p.PendingChoiceId != "none";
            var allowed = battle.CanAcceptInput(BattleInputOwner.ElementChoice);
            if (panel != null) panel.SetActive(pending && allowed);
            if (!changed && !_dirty && paused == _wasPaused && allowed == _wasAllowed) return;
            _observed = p; _wasPaused = paused; _wasAllowed = allowed; _dirty = false;
            if (titleText != null) titleText.text = p.PendingChoiceId == "start-resonance"
                ? "Choose your first element"
                : "Unlock an element";
            if (summaryText != null)
            {
                if (PreviewOptionId == null)
                    summaryText.text = p.PendingChoiceId == "start-resonance"
                        ? "Choose Flame or Gale to begin."
                        : $"Wave {battle.Simulation.State.WaveIndex + 1} cleared. Choose one element.";
                else
                    summaryText.text = PreviewOptionId switch
                    {
                        "flame" => "Flame keeps enemies burning.",
                        "tide" => "Tide slows advancing enemies.",
                        "gale" => "Gale fires rapidly and amplifies damage.",
                        _ => "Stone breaks through armor."
                    };
            }
            if (optionButtons != null)
                for (var i = 0; i < optionButtons.Length; i++)
                {
                    var button = optionButtons[i]; if (button == null) continue;
                    button.gameObject.SetActive(IsOption(Ids[i]));
                    button.interactable = allowed && !_sending;
                    var label = button.GetComponentInChildren<Text>(true);
                    var selected = PreviewOptionId == Ids[i];
                    label.text = TowerDisplay.Name(new TowerDefinitionId(Ids[i])) + (selected ? "\nSelected" : "");
                    label.color = selected ? new Color32(231, 194, 121, 255) : new Color32(245, 233, 209, 255);
                }
            if (confirmButton != null) confirmButton.interactable = pending && allowed && PreviewOptionId != null && !_sending;
            if (cancelButton != null) cancelButton.interactable = pending && allowed && !_sending;
        }
    }
}
