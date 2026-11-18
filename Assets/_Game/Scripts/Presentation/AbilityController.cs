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
        [SerializeField] private Text statusText;
        [SerializeField] private AbilityPreviewView preview;
        private BattleSimulation _observed;
        private BattleState _observedState;
        private long _epoch, _generation;
        private bool _previewing, _intent;
        private PathPosition _center, _intentCenter;
        private AbilityPreviewLease _intentLease;
        private AbilityCastFailureReason _previewCheckReason;
        private UnityAction _confirmCallback;
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
            _previewing = true; HasTarget = false; LastCast = null; Status = "Выберите центр — бой продолжается";
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
            Status = result.Succeeded ? "Цель выбрана — Confirm / Cancel" : result.FailureReason.ToString();
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
                Status = result.Succeeded ? $"Печать: {result.HitCount} hits, {result.TotalAppliedHpLoss} HP" : result.FailureReason.ToString();
            }
            finally { _generation++; placement.SuppressPointerForCurrentFrame(); Refresh(); }
        }
        public void CancelPreview()
        {
            Reconcile(); if (!battle.CanAcceptInput(BattleInputOwner.Ability)) return;
            Invalidate(); Status = "Отменено — способность не применена"; Refresh();
        }
        internal void Invalidate()
        {
            _generation++; _previewing = _intent = HasTarget = false; UnbindConfirm();
            if (preview != null) preview.Clear();
            if (placement != null) placement.SuppressPointerForCurrentFrame();
        }
        internal void ResetTransient() { Invalidate(); Status = string.Empty; LastCast = null; }
        private bool Fail(string reason) { Status = reason; Refresh(); return false; }
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
                    Status = targetCheck.Value.Succeeded ? "Цель выбрана — Confirm / Cancel" : _previewCheckReason.ToString();
                    preview.Show(_center, s.AbilityDefinition.Radius, targetCheck.Value.Succeeded);
                }
            }
            if (beginButton != null)
            {
                beginButton.gameObject.SetActive(enabled);
                beginButton.interactable = enabled && s.State.Phase == BattlePhase.Combat &&
                    s.AbilityState.CooldownRemainingTicks == 0 && battle.CanAcceptInput() && !HasInputOwnership;
                beginButton.GetComponentInChildren<Text>().text = s.AbilityState.CooldownRemainingTicks == 0
                    ? "Печать — Ready" : $"Печать — {s.AbilityState.CooldownRemainingTicks} ticks";
            }
            if (panel != null) panel.SetActive(enabled && !battle.IsPaused && (HasInputOwnership || (Status.Length > 0 && battle.ActiveModalOwner == BattleInputOwner.None)));
            if (statusText != null) statusText.text = Status;
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
