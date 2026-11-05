using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Veilward.Domain;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Veilward.Presentation
{
    public sealed class BattlePlacementController : MonoBehaviour
    {
        [SerializeField] private BattleSceneController battle;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Transform selectionHighlight;
        [SerializeField] private Transform towerPreview;
        [SerializeField] private GameObject confirmationPanel;
        [SerializeField] private UnityEngine.UI.Button confirmButton;
        [SerializeField] private UnityEngine.UI.Button cancelButton;
        [SerializeField] private UnityEngine.UI.Button startWaveButton;

        private bool _hasSelection;

        public bool HasSelection => _hasSelection;
        public GridCoordinate SelectedCell { get; private set; }

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
        }

        private void Start()
        {
            ValidateReference(battle, nameof(battle));
            ValidateReference(worldCamera, nameof(worldCamera));
            ValidateReference(selectionHighlight, nameof(selectionHighlight));
            ValidateReference(towerPreview, nameof(towerPreview));
            ValidateReference(confirmationPanel, nameof(confirmationPanel));
            ValidateReference(confirmButton, nameof(confirmButton));
            ValidateReference(cancelButton, nameof(cancelButton));
            ValidateReference(startWaveButton, nameof(startWaveButton));
            confirmButton.onClick.AddListener(ConfirmSelection);
            cancelButton.onClick.AddListener(CancelSelection);
            startWaveButton.onClick.AddListener(StartWave);
            CancelSelection();
        }

        private void Update()
        {
            if (battle == null || battle.Simulation == null)
            {
                return;
            }

            RefreshUi();
            if (battle.Simulation.State.Phase != BattlePhase.Preparation)
            {
                return;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                TryHandlePointer(mouse.position.ReadValue(), pointerId: null);
                return;
            }

            var touches = Touch.activeTouches;
            for (var index = 0; index < touches.Count; index++)
            {
                var touch = touches[index];
                if (touch.phase == TouchPhase.Began)
                {
                    TryHandlePointer(touch.screenPosition, touch.touchId);
                    break;
                }
            }
        }

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
        }

        private void OnDestroy()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(ConfirmSelection);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(CancelSelection);
            }

            if (startWaveButton != null)
            {
                startWaveButton.onClick.RemoveListener(StartWave);
            }
        }

        public bool TrySelectCell(GridCoordinate coordinate)
        {
            var simulation = battle.Simulation;
            if (simulation == null || simulation.State.Phase != BattlePhase.Preparation ||
                !simulation.Grid.Map.Contains(coordinate) ||
                simulation.Grid.GetState(coordinate) != GridCellState.Available)
            {
                return false;
            }

            SelectedCell = coordinate;
            _hasSelection = true;
            var position = battle.GetCellCenter(coordinate);
            selectionHighlight.position = position + new Vector3(0f, 0.06f, 0f);
            towerPreview.position = position + new Vector3(0f, 0.06f, 0f);
            selectionHighlight.gameObject.SetActive(true);
            towerPreview.gameObject.SetActive(true);
            confirmationPanel.SetActive(true);
            return true;
        }

        public void ConfirmSelection()
        {
            if (!_hasSelection || battle.Simulation == null)
            {
                return;
            }

            var result = battle.PlaceTower(SelectedCell);
            if (result.Success)
            {
                CancelSelection();
            }
        }

        public void CancelSelection()
        {
            _hasSelection = false;
            SelectedCell = default;
            if (selectionHighlight != null)
            {
                selectionHighlight.gameObject.SetActive(false);
            }

            if (towerPreview != null)
            {
                towerPreview.gameObject.SetActive(false);
            }

            if (confirmationPanel != null)
            {
                confirmationPanel.SetActive(false);
            }
        }

        public void StartWave()
        {
            if (battle.Simulation == null)
            {
                return;
            }

            CancelSelection();
            battle.StartWave();
            RefreshUi();
        }

        private void RefreshUi()
        {
            var isPreparing = battle.Simulation.State.Phase == BattlePhase.Preparation;
            if (!isPreparing && _hasSelection)
            {
                CancelSelection();
            }

            confirmationPanel.SetActive(isPreparing && _hasSelection);
            confirmButton.interactable = isPreparing && _hasSelection;
            cancelButton.interactable = isPreparing && _hasSelection;
            startWaveButton.interactable = isPreparing;
        }

        private void TryHandlePointer(Vector2 screenPosition, int? pointerId)
        {
            if (EventSystem.current != null)
            {
                var overUi = pointerId.HasValue
                    ? EventSystem.current.IsPointerOverGameObject(pointerId.Value)
                    : EventSystem.current.IsPointerOverGameObject();
                if (overUi)
                {
                    return;
                }
            }

            var ray = worldCamera.ScreenPointToRay(screenPosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out var distance))
            {
                return;
            }

            var hit = ray.GetPoint(distance);
            if (battle.Simulation.Grid.Map.TryGetCoordinate(new PathPosition(hit.x, hit.z), out var cell))
            {
                TrySelectCell(cell);
            }
        }

        private static void ValidateReference(UnityEngine.Object value, string name)
        {
            if (value == null)
            {
                throw new InvalidOperationException($"Battle placement reference is required: {name}.");
            }
        }
    }
}
