using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Veilward.Domain;

namespace Veilward.Presentation
{
    public readonly struct AbilityPreviewLease
    {
        internal AbilityPreviewLease(long epoch, long generation, int wave, long sequence)
        { Epoch = epoch; Generation = generation; Wave = wave; Sequence = sequence; }
        public long Epoch { get; }
        public long Generation { get; }
        public int Wave { get; }
        public long Sequence { get; }
    }

    public sealed class AbilityController : MonoBehaviour
    {
        [SerializeField] private BattleSceneController battle;
        [SerializeField] private BattlePlacementController placement;
        [SerializeField] private Button beginButton, confirmButton, cancelButton;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text statusText, feedbackText;
        [SerializeField] private AbilityPreviewView preview;
        private BattleSimulation _observed;
        private BattleState _observedState;
        private long _epoch, _generation;
        private bool _previewing, _intent;
        private PathPosition _center, _intentCenter;
        private AbilityPreviewLease _intentLease;
        private AbilityCastFailureReason _previewCheckReason;
        private UnityAction _confirmCallback;
        private Text _beginLabel;
        private double _feedbackUntil;
        private (bool combat, bool ready, int seconds) _shownAbility = (false, false, -1);
        public AbilityPreviewLease Lease { get; private set; }
        public bool HasInputOwnership => _previewing || _intent;
        public bool HasTarget { get; private set; }
        public string Status { get; private set; } = string.Empty;
        public AbilityCastResult? LastCast { get; private set; }

        private void Start()
        {
            if (battle == null || placement == null || beginButton == null || confirmButton == null || cancelButton == null ||
                panel == null || statusText == null || preview == null)
                throw new InvalidOperationException("Ability requires all scene references.");
            preview.Initialize();
            _beginLabel = beginButton.GetComponentInChildren<Text>(true);
            beginButton.onClick.AddListener(HandleBegin);
            cancelButton.onClick.AddListener(CancelPreview);
            Refresh();
        }
        private void OnDestroy()
        {
            if (beginButton != null) beginButton.onClick.RemoveListener(HandleBegin);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(CancelPreview);
            UnbindConfirm();
        }
        private void HandleBegin() => BeginPreview();
        private void Update() => Refresh();
        private void UnbindConfirm()
        {
            if (_confirmCallback != null && confirmButton != null) confirmButton.onClick.RemoveListener(_confirmCallback);
            _confirmCallback = null;
        }
        private void Reconcile()
        {
            var s = battle.Simulation;
            if (!ReferenceEquals(s, _observed))
            {
                _epoch++; _observed = s; _observedState = s?.State; ResetTransient();
            }
            if (s == null) return;
            if (!ReferenceEquals(_observedState, s.State)) { _observedState = s.State; ResetTransient(); }
            if (HasInputOwnership && (battle.IsPaused || s.State.Phase != BattlePhase.Combat ||
                s.State.WaveIndex != Lease.Wave || s.AbilityState.NextCastSequence != Lease.Sequence))
            {
                var reason = battle.IsPaused ? "Paused" : s.State.Phase != BattlePhase.Combat ? "InvalidPhase" : "StalePreview";
                Invalidate(); Status = reason;
            }
        }
        public bool BeginPreview()
        {
            Reconcile(); var s = battle.Simulation;
            if (s == null) return false;
            if (!battle.CanAcceptInput() || HasInputOwnership) return Fail(battle.IsPaused ? "Paused" : "InputBlocked");
            if (s.AbilityDefinition == null) return Fail("AbilityDisabled");
            if (s.State.Phase != BattlePhase.Combat) return Fail("InvalidPhase");
            if (s.AbilityState.CooldownRemainingTicks != 0) return Fail("CooldownActive");
            Lease = new AbilityPreviewLease(_epoch, ++_generation, s.State.WaveIndex, s.AbilityState.NextCastSequence);
            _previewing = true; HasTarget = false; LastCast = null; Status = "Tap the field to aim the seal.";
            UnbindConfirm(); var lease = Lease;
            _confirmCallback = () => ConfirmLease(lease); confirmButton.onClick.AddListener(_confirmCallback);
            placement.SuppressPointerForCurrentFrame(); Refresh(); return true;
        }
        public bool SelectTarget(PathPosition center)
        {
            Reconcile();
            if (!_previewing || !battle.CanAcceptInput(BattleInputOwner.Ability)) return false;
            _center = center; HasTarget = true;
            var result = battle.Simulation.CheckCastAbility(Command(center));
            _previewCheckReason = result.FailureReason;
            Status = result.Succeeded ? "Enemies in range. Cast when ready." : result.FailureReason.ToString();
            preview.Show(center, battle.Simulation.AbilityDefinition.Radius, result.Succeeded);
            Refresh(); return true;
        }
        private CastAbilityCommand Command(PathPosition center) => new CastAbilityCommand(
            battle.Simulation.AbilityState.AbilityId, center, Lease.Wave, battle.Simulation.State.Tick, Lease.Sequence);
        public bool ConfirmPreview() => ConfirmLease(Lease);
        public bool ConfirmLease(AbilityPreviewLease lease)
        {
            Reconcile();
            if (battle.IsPaused) return Fail("Paused");
            if (!battle.CanAcceptInput(BattleInputOwner.Ability)) return Fail("InputBlocked");
            if (lease.Epoch != Lease.Epoch || lease.Generation != Lease.Generation ||
                lease.Wave != Lease.Wave || lease.Sequence != Lease.Sequence) return Fail("StalePreview");
            if (!_previewing) return Fail("ConfirmationRequired");
            if (!HasTarget) return Fail("InvalidTarget");
            // Lease потреблён до публикации intent; повторный callback уже не владеет preview.
            _previewing = false; _intent = true; _intentCenter = _center; _intentLease = lease;
            HasTarget = false; preview.Clear(); placement.SuppressPointerForCurrentFrame(); Refresh(); return true;
        }
        internal void DispatchIntent()
        {
            Reconcile(); if (!_intent) return;
            var lease = _intentLease; var center = _intentCenter;
            _intent = false; UnbindConfirm();
            if (lease.Epoch != _epoch || lease.Generation != _generation ||
                !battle.CanAcceptInput(BattleInputOwner.Ability)) { Invalidate(); return; }
            try
            {
                var result = battle.DispatchAbility(this, new CastAbilityCommand(battle.Simulation.AbilityState.AbilityId,
                    center, lease.Wave, battle.Simulation.State.Tick, lease.Sequence));
                LastCast = result;
                Status = result.Succeeded ? $"Rift Seal hit {result.HitCount} · {result.TotalAppliedHpLoss} HP lost" : result.FailureReason.ToString();
                _feedbackUntil = Time.unscaledTimeAsDouble + 2.5;
            }
            finally { _generation++; placement.SuppressPointerForCurrentFrame(); Refresh(); }
        }
        public void CancelPreview()
        {
            Reconcile(); if (!battle.CanAcceptInput(BattleInputOwner.Ability)) return;
            Invalidate();
            Status = "Rift Seal cancelled.";
            _feedbackUntil = Time.unscaledTimeAsDouble + 2.5;
            Refresh();
        }
        internal void Invalidate()
        {
            _generation++; _previewing = _intent = HasTarget = false; UnbindConfirm();
            if (preview != null) preview.Clear();
            if (placement != null) placement.SuppressPointerForCurrentFrame();
        }
        internal void ResetTransient() { Invalidate(); Status = string.Empty; LastCast = null; _feedbackUntil = 0; }
        private bool Fail(string reason)
        {
            Status = reason;
            _feedbackUntil = Time.unscaledTimeAsDouble + 2.5;
            Refresh();
            return false;
        }

        private static string PlayerStatus(string value) => value switch
        {
            "Paused" => "Resume before casting.",
            "InvalidPhase" => "Rift Seal is usable in battle.",
            "CooldownActive" => "Rift Seal is recharging.",
            "NoTargets" => "No enemies in range.",
            "InvalidTarget" => "Aim at a valid point on the field.",
            "AbilityDisabled" or "UnknownAbility" => "Rift Seal is unavailable in this battle.",
            "InputBlocked" => "Close the open card before aiming.",
            "StalePreview" or "StaleCommand" or "ConfirmationRequired" => "Aim the seal again.",
            "IdentifierExhausted" => "Rift Seal is no longer available.",
            _ => value
        };
        public void Refresh()
        {
            if (battle == null || battle.Simulation == null) return;
            Reconcile(); var s = battle.Simulation; var enabled = s.AbilityDefinition != null;
            AbilityCastResult? targetCheck = null;
            if (_previewing && HasTarget)
            {
                targetCheck = s.CheckCastAbility(Command(_center));
                if (targetCheck.Value.FailureReason != _previewCheckReason)
                {
                    _previewCheckReason = targetCheck.Value.FailureReason;
                    Status = targetCheck.Value.Succeeded ? "Enemies in range. Cast when ready." : _previewCheckReason.ToString();
                    preview.Show(_center, s.AbilityDefinition.Radius, targetCheck.Value.Succeeded);
                }
            }
            if (beginButton != null)
            {
                beginButton.gameObject.SetActive(enabled && !HasInputOwnership &&
                    battle.ActiveModalOwner != BattleInputOwner.Placement);
                var remaining = s.AbilityState.CooldownRemainingTicks;
                var inCombat = s.State.Phase == BattlePhase.Combat;
                beginButton.interactable = enabled && inCombat && remaining == 0 && battle.CanAcceptInput();
                var seconds = Mathf.CeilToInt((float)remaining / SimulationTiming.TicksPerSecond);
                var display = (inCombat, beginButton.interactable, seconds);
                if (_shownAbility != display)
                {
                    _shownAbility = display;
                    var label = _beginLabel != null ? _beginLabel : beginButton.GetComponentInChildren<Text>(true);
                    var caption = !inCombat ? "In battle only" : remaining > 0 ? $"Recharging · {seconds}s" :
                        beginButton.interactable ? "Ready" : "Close active card";
                    if (label != null) label.text = "Rift Seal\n" + caption;
                }
            }
            if (panel != null) panel.SetActive(enabled && !battle.IsPaused && HasInputOwnership);
            var playerStatus = PlayerStatus(Status);
            if (statusText != null && statusText.text != playerStatus) statusText.text = playerStatus;
            if (feedbackText != null)
            {
                var showFeedback = enabled && !battle.IsPaused && battle.ActiveModalOwner == BattleInputOwner.None &&
                    !HasInputOwnership && Time.unscaledTimeAsDouble < _feedbackUntil && playerStatus.Length > 0;
                feedbackText.transform.parent.gameObject.SetActive(showFeedback);
                if (showFeedback && feedbackText.text != playerStatus) feedbackText.text = playerStatus;
            }
            if (confirmButton != null)
            {
                confirmButton.gameObject.SetActive(_previewing);
                confirmButton.interactable = _previewing && HasTarget && battle.CanAcceptInput(BattleInputOwner.Ability) &&
                    targetCheck.HasValue && targetCheck.Value.Succeeded;
            }
            if (cancelButton != null) cancelButton.gameObject.SetActive(_previewing);
        }
    }
}
