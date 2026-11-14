using System;
using System.Collections.Generic;
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
        [SerializeField] private UnityEngine.UI.Button nextWaveButton;
        [SerializeField] private LineRenderer rangePreview;
        [SerializeField] private GameObject towerPanel;
        [SerializeField] private UnityEngine.UI.Text towerStatsText;
        [SerializeField] private UnityEngine.UI.Button upgradeButton;
        [SerializeField] private UnityEngine.UI.Button sellButton;
        [SerializeField] private GameObject sellConfirmationPanel;
        [SerializeField] private UnityEngine.UI.Button confirmSellButton;
        [SerializeField] private UnityEngine.UI.Button cancelSellButton;

        [SerializeField] private RectTransform catalogRoot;
        [SerializeField] private UnityEngine.UI.Button catalogButtonTemplate;
        [SerializeField] private UnityEngine.UI.Text buildStatsText;
        [SerializeField] private UnityEngine.UI.Text sellConfirmationText;

        private readonly List<UnityEngine.UI.Button> _catalogButtons = new List<UnityEngine.UI.Button>();
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private Canvas _canvas;
        private RectTransform _placementPanelRect;
        private RectTransform _towerPanelRect;
        private Vector2 _placementPanelOrigin;
        private Vector2 _towerPanelOrigin;
        private Rect _lastSafeArea;
        private Vector2 _lastScreenSize;
        private float _lastCanvasScale;
        private TowerDefinition _selectedDefinition;
        private TowerDefinition _displayedTowerDefinition;
        public TowerDefinitionId SelectedTowerTypeId { get; private set; }
        public IReadOnlyList<UnityEngine.UI.Button> CatalogButtons => _catalogButtons;
        public UnityEngine.UI.Text BuildStatsText => buildStatsText;

        private bool _hasSelection;
        private bool _hasTowerSelection;
        private bool _isSellConfirmationVisible;
        private TowerId? _sellConfirmationTowerId;
        private int _pointerSuppressedUntilFrame = -1;

        public bool HasSelection => _hasSelection;
        public GridCoordinate SelectedCell { get; private set; }
        public TowerId? SelectedTowerId { get; private set; }
        public UnityEngine.UI.Text TowerStatsText => towerStatsText;
        public UnityEngine.UI.Button UpgradeButton => upgradeButton;
        public bool IsSellConfirmationVisible => _isSellConfirmationVisible;

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
            ValidateReference(nextWaveButton, nameof(nextWaveButton));
            ValidateReference(rangePreview, nameof(rangePreview));
            ValidateReference(towerPanel, nameof(towerPanel));
            ValidateReference(towerStatsText, nameof(towerStatsText));
            ValidateReference(upgradeButton, nameof(upgradeButton));
            ValidateReference(sellButton, nameof(sellButton));
            ValidateReference(sellConfirmationPanel, nameof(sellConfirmationPanel));
            ValidateReference(confirmSellButton, nameof(confirmSellButton));
            ValidateReference(cancelSellButton, nameof(cancelSellButton));
            ValidateReference(catalogRoot, nameof(catalogRoot));
            ValidateReference(catalogButtonTemplate, nameof(catalogButtonTemplate));
            ValidateReference(buildStatsText, nameof(buildStatsText));
            ValidateReference(sellConfirmationText, nameof(sellConfirmationText));
            _canvas = confirmationPanel.GetComponentInParent<Canvas>();
            _placementPanelRect = confirmationPanel.GetComponent<RectTransform>();
            _towerPanelRect = towerPanel.GetComponent<RectTransform>();
            _placementPanelOrigin = _placementPanelRect.anchoredPosition;
            _towerPanelOrigin = _towerPanelRect.anchoredPosition;
            RefreshSafeArea();
            foreach (var archetype in battle.Simulation.TowerCatalog.Archetypes)
            {
                if (!archetype.ShowInCatalog) continue;
                var id = archetype.Id;
                var button = Instantiate(catalogButtonTemplate, catalogRoot);
                button.name = $"Catalog {id.Value}";
                button.GetComponentInChildren<UnityEngine.UI.Text>(true).text = TowerDisplay.Name(id);
                button.onClick.AddListener(() => SelectTowerType(id));
                button.gameObject.SetActive(true);
                _catalogButtons.Add(button);
            }
            catalogButtonTemplate.gameObject.SetActive(false);
            confirmButton.onClick.AddListener(ConfirmSelection);
            cancelButton.onClick.AddListener(CancelSelection);
            startWaveButton.onClick.AddListener(StartWave);
            nextWaveButton.onClick.AddListener(ContinueToNextWave);
            upgradeButton.onClick.AddListener(HandleUpgradeClicked);
            sellButton.onClick.AddListener(RequestSellSelectedTower);
            confirmSellButton.onClick.AddListener(HandleConfirmSellClicked);
            cancelSellButton.onClick.AddListener(CancelSellConfirmation);
            var cellSize = battle.Simulation.Grid.Map.CellSize;
            selectionHighlight.localScale = new Vector3(0.98f * cellSize, 0.04f, 0.98f * cellSize);
            towerPreview.localScale = Vector3.one * cellSize;
            rangePreview.useWorldSpace = false;
            rangePreview.loop = true;
            rangePreview.positionCount = 64;

            CancelSelection();
            RefreshUi();
        }

        private void Update()
        {
            if (battle == null || battle.Simulation == null)
            {
                return;
            }

            RefreshSafeArea();
            RefreshUi();
            if (battle.IsPaused || Time.frameCount <= _pointerSuppressedUntilFrame)
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

            if (nextWaveButton != null)
            {
                nextWaveButton.onClick.RemoveListener(ContinueToNextWave);
            }

            if (upgradeButton != null)
            {
                upgradeButton.onClick.RemoveListener(HandleUpgradeClicked);
            }

            if (sellButton != null)
            {
                sellButton.onClick.RemoveListener(RequestSellSelectedTower);
            }

            if (confirmSellButton != null)
            {
                confirmSellButton.onClick.RemoveListener(HandleConfirmSellClicked);
            }

            if (cancelSellButton != null)
            {
                cancelSellButton.onClick.RemoveListener(CancelSellConfirmation);
            }
        }

        private void HandleUpgradeClicked()
        {
            UpgradeSelectedTower();
        }

        private void HandleConfirmSellClicked()
        {
            ConfirmSellSelectedTower();
        }

        public void SuppressPointerForCurrentFrame()
        {
            _pointerSuppressedUntilFrame = Time.frameCount;
        }

        public bool TrySelectCell(GridCoordinate coordinate)
        {
            var simulation = battle.Simulation;
            if (battle.IsPaused || _isSellConfirmationVisible || simulation == null || !simulation.Grid.Map.Contains(coordinate))
            {
                return false;
            }

            var cellState = simulation.Grid.GetState(coordinate);
            if (cellState == GridCellState.Occupied)
            {
                var towers = simulation.State.Towers;
                for (var index = 0; index < towers.Count; index++)
                {
                    if (towers[index].Coordinate == coordinate)
                    {
                        SelectExistingTower(towers[index]);
                        return true;
                    }
                }

                return false;
            }

            if (simulation.State.Phase != BattlePhase.Preparation || cellState != GridCellState.Available)
            {
                return false;
            }

            CancelSelection();
            SelectedCell = coordinate;
            _hasSelection = true;
            ShowSelectionVisual(coordinate, 0f, isPlacement: true);
            towerPreview.gameObject.SetActive(false);
            rangePreview.gameObject.SetActive(false);
            RefreshUi();
            return true;
        }

        public void SelectTowerType(TowerDefinitionId id)
        {
            if (battle.IsPaused || !_hasSelection || _hasTowerSelection || _isSellConfirmationVisible ||
                battle.Simulation.State.Phase != BattlePhase.Preparation) return;
            foreach (var archetype in battle.Simulation.TowerCatalog.Archetypes)
            {
                if (archetype.Id != id || !archetype.ShowInCatalog) continue;
                SelectedTowerTypeId = id;
                _selectedDefinition = archetype.Levels[0];
                buildStatsText.text = $"{TowerDisplay.Name(id)} — {TowerDisplay.Role(_selectedDefinition)}\n" +
                    $"Price: {_selectedDefinition.Price} gold\n" + TowerDisplay.Stats(_selectedDefinition);
                ShowSelectionVisual(SelectedCell, _selectedDefinition.Range, isPlacement: true);
                TowerDisplay.ApplyVisual(towerPreview, _selectedDefinition, 1);
                RefreshUi();
                return;
            }
        }

        private void SelectExistingTower(TowerState tower)
        {
            CancelSelection();
            SelectedCell = tower.Coordinate;
            SelectedTowerId = tower.Id;
            _hasSelection = true;
            _hasTowerSelection = true;
            ShowSelectionVisual(tower.Coordinate, tower.Definition.Range, isPlacement: false);
            RefreshUi();
        }

        private void ShowSelectionVisual(GridCoordinate coordinate, float radius, bool isPlacement)
        {
            var position = battle.GetCellCenter(coordinate);
            selectionHighlight.position = position + new Vector3(0f, 0.06f, 0f);
            towerPreview.position = position + new Vector3(0f, 0.06f, 0f);
            rangePreview.transform.position = position + new Vector3(0f, 0.04f, 0f);
            for (var index = 0; index < rangePreview.positionCount; index++)
            {
                var angle = index * Mathf.PI * 2f / rangePreview.positionCount;
                rangePreview.SetPosition(
                    index,
                    new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            selectionHighlight.gameObject.SetActive(true);
            towerPreview.gameObject.SetActive(isPlacement);
            rangePreview.gameObject.SetActive(true);
            confirmationPanel.SetActive(isPlacement);
            towerPanel.SetActive(!isPlacement);
        }

        public void ConfirmSelection()
        {
            if (battle.IsPaused || _isSellConfirmationVisible || !_hasSelection ||
                _hasTowerSelection || _selectedDefinition == null || battle.Simulation == null)
            {
                return;
            }

            var result = battle.PlaceTower(SelectedTowerTypeId, SelectedCell);
            if (result.Success)
            {
                SuppressPointerForCurrentFrame();
                CancelSelection();
            }
        }

        public TowerOperationResult UpgradeSelectedTower()
        {
            if (battle.IsPaused)
            {
                return TowerOperationResult.Failed(TowerOperationFailureReason.InvalidPhase);
            }

            if (_isSellConfirmationVisible)
            {
                return TowerOperationResult.Failed(TowerOperationFailureReason.ConfirmationRequired);
            }

            if (!SelectedTowerId.HasValue)
            {
                return TowerOperationResult.Failed(TowerOperationFailureReason.TowerNotFound);
            }

            var result = battle.UpgradeTower(SelectedTowerId.Value);
            var tower = FindSelectedTower();
            if (result.Success && tower != null)
                ShowSelectionVisual(tower.Coordinate, tower.Definition.Range, isPlacement: false);
            RefreshUi();
            return result;
        }

        public void RequestSellSelectedTower()
        {
            if (battle.IsPaused || _isSellConfirmationVisible || !SelectedTowerId.HasValue ||
                battle.Simulation.State.Phase != BattlePhase.Preparation)
            {
                return;
            }

            var tower = FindSelectedTower();
            if (tower == null) return;
            sellConfirmationText.text = $"Sell {TowerDisplay.Name(tower.Definition.Id)} (#{tower.Id.Value})?\n" +
                $"Refund: {(long)tower.InvestedGold * 70 / 100} gold";
            _sellConfirmationTowerId = SelectedTowerId;
            _isSellConfirmationVisible = true;
            RefreshUi();
        }

        public TowerOperationResult ConfirmSellSelectedTower()
        {
            if (!_isSellConfirmationVisible || !_sellConfirmationTowerId.HasValue ||
                SelectedTowerId != _sellConfirmationTowerId)
            {
                return TowerOperationResult.Failed(TowerOperationFailureReason.ConfirmationRequired);
            }

            var result = battle.SellTower(_sellConfirmationTowerId.Value);
            if (result.Success)
            {
                CancelSelection();
            }
            else
            {
                CancelSellConfirmation();
            }

            RefreshUi();
            return result;
        }

        public void CancelSellConfirmation()
        {
            SuppressPointerForCurrentFrame();
            _isSellConfirmationVisible = false;
            _sellConfirmationTowerId = null;
            if (sellConfirmationPanel != null)
            {
                sellConfirmationPanel.SetActive(false);
            }

            if (battle != null && battle.Simulation != null)
            {
                RefreshUi();
            }
        }

        public void CancelSelection()
        {
            SuppressPointerForCurrentFrame();
            SelectedTowerTypeId = default;
            _selectedDefinition = null;
            _displayedTowerDefinition = null;
            if (buildStatsText != null) buildStatsText.text = "Choose a tower type";
            _hasSelection = false;
            _hasTowerSelection = false;
            _isSellConfirmationVisible = false;
            _sellConfirmationTowerId = null;
            SelectedCell = default;
            SelectedTowerId = null;
            if (selectionHighlight != null)
            {
                selectionHighlight.gameObject.SetActive(false);
            }

            if (towerPreview != null)
            {
                towerPreview.gameObject.SetActive(false);
            }

            if (rangePreview != null)
            {
                rangePreview.gameObject.SetActive(false);
            }

            if (confirmationPanel != null)
            {
                confirmationPanel.SetActive(false);
            }

            if (towerPanel != null)
            {
                towerPanel.SetActive(false);
            }

            if (sellConfirmationPanel != null)
            {
                sellConfirmationPanel.SetActive(false);
            }
        }

        public void StartWave()
        {
            if (battle.IsPaused || _isSellConfirmationVisible || battle.Simulation == null)
            {
                return;
            }

            CancelSelection();
            battle.StartWave();
            RefreshUi();
        }

        public void ContinueToNextWave()
        {
            if (battle.IsPaused || _isSellConfirmationVisible || battle.Simulation == null)
            {
                return;
            }

            battle.ContinueToNextWave();
            RefreshUi();
        }

        private void RefreshUi()
        {
            var state = battle.Simulation.State;
            var isPaused = battle.IsPaused;
            var isPreparing = state.Phase == BattlePhase.Preparation;
            if (!isPreparing && _hasSelection && !_hasTowerSelection)
            {
                CancelSelection();
            }

            var tower = FindSelectedTower();
            if (_hasTowerSelection && tower == null)
            {
                CancelSelection();
            }

            if (_isSellConfirmationVisible &&
                (!isPreparing || tower == null || _sellConfirmationTowerId != SelectedTowerId))
            {
                _isSellConfirmationVisible = false;
                _sellConfirmationTowerId = null;
            }

            var isSellConfirmationActive = !isPaused && isPreparing && tower != null &&
                                           _isSellConfirmationVisible;
            confirmationPanel.SetActive(isPreparing && !isSellConfirmationActive &&
                                        _hasSelection && !_hasTowerSelection);
            confirmButton.interactable = !isPaused && isPreparing && !isSellConfirmationActive &&
                                         _hasSelection && !_hasTowerSelection && _selectedDefinition != null &&
                                         state.Gold >= _selectedDefinition.Price;
            foreach (var button in _catalogButtons) button.interactable = !isPaused && isPreparing && !isSellConfirmationActive;
            cancelButton.interactable = !isPaused && isPreparing && !isSellConfirmationActive &&
                                        _hasSelection && !_hasTowerSelection;
            startWaveButton.interactable = !isPaused && isPreparing && !isSellConfirmationActive;
            nextWaveButton.interactable = !isPaused && state.Phase == BattlePhase.WaveComplete &&
                                          state.WaveIndex + 1 < state.WaveCount && !isSellConfirmationActive;
            towerPanel.SetActive(tower != null);
            upgradeButton.interactable = false;
            sellButton.interactable = !isPaused && isPreparing && tower != null && !isSellConfirmationActive;
            sellConfirmationPanel.SetActive(isSellConfirmationActive);
            confirmSellButton.interactable = isSellConfirmationActive;
            cancelSellButton.interactable = isSellConfirmationActive;
            if (tower == null)
            {
                towerStatsText.text = string.Empty;
                return;
            }

            var levels = battle.Simulation.TowerCatalog.Get(tower.Definition.Id).Levels;
            var hasNextLevel = tower.Level < levels.Count;
            var nextLevel = hasNextLevel ? levels[tower.Level] : null;
            var refund = (int)((long)tower.InvestedGold * 70 / 100);
            if (_displayedTowerDefinition != tower.Definition)
            {
                _displayedTowerDefinition = tower.Definition;
                towerStatsText.text = $"{TowerDisplay.Name(tower.Definition.Id)} — Level: {tower.Level}\n" +
                    TowerDisplay.Stats(tower.Definition) + "\n" +
                    (hasNextLevel ? $"Upgrade: {nextLevel.Price} gold" : "Upgrade: MAX") +
                    $"\nSell refund: {refund} gold";
            }
            upgradeButton.interactable = !isPaused && isPreparing && !isSellConfirmationActive &&
                                         hasNextLevel && state.Gold >= nextLevel.Price;
        }

        private TowerState FindSelectedTower()
        {
            if (!SelectedTowerId.HasValue)
            {
                return null;
            }

            var towers = battle.Simulation.State.Towers;
            for (var index = 0; index < towers.Count; index++)
            {
                if (towers[index].Id == SelectedTowerId.Value)
                {
                    return towers[index];
                }
            }

            return null;
        }

        private void RefreshSafeArea()
        {
            var size = new Vector2(Screen.width, Screen.height);
            var area = Screen.safeArea;
            var scale = _canvas != null ? _canvas.scaleFactor : 1f;
            if (area == _lastSafeArea && size == _lastScreenSize && scale == _lastCanvasScale) return;
            ApplySafeArea(area, size, scale);
            _lastSafeArea = area;
            _lastScreenSize = size;
            _lastCanvasScale = scale;
        }

        private void ApplySafeArea(Rect area, Vector2 screenSize, float canvasScale)
        {
            if (screenSize.x <= 0f || screenSize.y <= 0f || area.width <= 0f || area.height <= 0f || canvasScale <= 0f) return;
            // Новые панели привязаны к правому нижнему углу; fullscreen Pause Overlay не сужаем.
            var inset = new Vector2(-Mathf.Max(0f, screenSize.x - area.xMax), Mathf.Max(0f, area.yMin)) / canvasScale;
            _placementPanelRect.anchoredPosition = _placementPanelOrigin + inset;
            _towerPanelRect.anchoredPosition = _towerPanelOrigin + inset;
        }

        private void TryHandlePointer(Vector2 screenPosition, int? pointerId)
        {
            if (battle.IsPaused || _isSellConfirmationVisible || Time.frameCount <= _pointerSuppressedUntilFrame)
                return;
            if (EventSystem.current != null)
            {
                // Проверяем текущую позицию: cached IsPointerOverGameObject зависит от порядка Update.
                var pointer = new PointerEventData(EventSystem.current) { position = screenPosition };
                _uiHits.Clear();
                EventSystem.current.RaycastAll(pointer, _uiHits);
                if (_uiHits.Count > 0) return;
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
