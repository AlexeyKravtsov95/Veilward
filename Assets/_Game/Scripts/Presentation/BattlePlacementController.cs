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
        [SerializeField] private EnemyInspectorController enemyInspector;
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
        [SerializeField] private UnityEngine.UI.Button sellButton, closeTowerButton;
        [SerializeField] private GameObject sellConfirmationPanel;
        [SerializeField] private UnityEngine.UI.Button confirmSellButton;
        [SerializeField] private UnityEngine.UI.Button cancelSellButton;

        [SerializeField] private RectTransform catalogRoot;
        [SerializeField] private UnityEngine.UI.Button catalogButtonTemplate;
        [SerializeField] private UnityEngine.UI.Text buildStatsText;
        [SerializeField] private UnityEngine.UI.Text sellConfirmationText;

        private readonly List<UnityEngine.UI.Button> _catalogButtons = new List<UnityEngine.UI.Button>();
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private ElementProgressionState _catalogProgression;
        private BattleSimulation _catalogSimulation;
        private int _catalogGold = -1;
        private BattlePhase _catalogPhase;
        private GridCoordinate _catalogCell;
        private TowerDefinition _selectedDefinition;
        private TowerDefinition _displayedTowerDefinition;
        private (TowerDefinition definition, int gold, int refund, BattlePhase phase) _shownTowerActions;
        private double _nextUpgradeTap;
        private UnityEngine.UI.Text _upgradeLabel, _sellLabel;
        public TowerDefinitionId SelectedTowerTypeId { get; private set; }
        public IReadOnlyList<UnityEngine.UI.Button> CatalogButtons => _catalogButtons;
        public UnityEngine.UI.Text BuildStatsText => buildStatsText;

        private bool _hasSelection;
        private bool _hasTowerSelection;
        private bool _isSellConfirmationVisible;
        private TowerId? _sellConfirmationTowerId;
        private int _pointerSuppressedUntilFrame = -1;
        private readonly System.Collections.Generic.HashSet<int> _heldPointers = new System.Collections.Generic.HashSet<int>();
        private BattleInputOwner _lastPointerOwner;
        private bool _lastPointerPaused;

        public bool HasSelection => _hasSelection;
        internal bool ShowsBuildDrawer => confirmationPanel != null && confirmationPanel.activeSelf;
        internal bool BuildDrawerOnLeft => ((RectTransform)confirmationPanel.transform).anchorMin.x == 0;
        public GridCoordinate SelectedCell { get; private set; }
        public TowerId? SelectedTowerId { get; private set; }
        public UnityEngine.UI.Text TowerStatsText => towerStatsText;
        public UnityEngine.UI.Button UpgradeButton => upgradeButton;
        public bool IsSellConfirmationVisible => _isSellConfirmationVisible;

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            SuppressPointerForCurrentFrame();
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
            _upgradeLabel = upgradeButton.GetComponentInChildren<UnityEngine.UI.Text>(true);
            _sellLabel = sellButton.GetComponentInChildren<UnityEngine.UI.Text>(true);
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
            if (closeTowerButton != null) closeTowerButton.onClick.AddListener(CancelSelection);
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

            RefreshUi();
            if (_heldPointers.Count > 0 && (_lastPointerOwner != battle.ActiveModalOwner || _lastPointerPaused != battle.IsPaused))
                SuppressPointerForCurrentFrame();
            _lastPointerOwner = battle.ActiveModalOwner; _lastPointerPaused = battle.IsPaused;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) DispatchPointer(mouse.position.ReadValue(), -1, TouchPhase.Began);
                if (mouse.leftButton.wasReleasedThisFrame) DispatchPointer(mouse.position.ReadValue(), -1, TouchPhase.Ended);
            }

            if (!EnhancedTouchSupport.enabled) return;
            var touches = Touch.activeTouches;
            for (var index = 0; index < touches.Count; index++)
            {
                var touch = touches[index];
                DispatchPointer(touch.screenPosition, touch.touchId, touch.phase);
            }
        }

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
            _heldPointers.Clear();
            SuppressPointerForCurrentFrame();
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

            if (closeTowerButton != null) closeTowerButton.onClick.RemoveListener(CancelSelection);
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
            if (!upgradeButton.interactable || Time.unscaledTimeAsDouble < _nextUpgradeTap) return;
            if (UpgradeSelectedTower().Success) _nextUpgradeTap = Time.unscaledTimeAsDouble + .30;
        }

        private void HandleConfirmSellClicked()
        {
            ConfirmSellSelectedTower();
        }

        public void SuppressPointerForCurrentFrame()
        {
            _pointerSuppressedUntilFrame = Time.frameCount;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed) _heldPointers.Add(-1);
            if (!EnhancedTouchSupport.enabled) return;
            var touches = Touch.activeTouches;
            for (var i = 0; i < touches.Count; i++)
                if (touches[i].phase != TouchPhase.Ended && touches[i].phase != TouchPhase.Canceled) _heldPointers.Add(touches[i].touchId);
        }

        public bool TrySelectCell(GridCoordinate coordinate)
        {
            var simulation = battle.Simulation;
            if (!battle.CanAcceptInput(BattleInputOwner.Placement) || _isSellConfirmationVisible || simulation == null || !simulation.Grid.Map.Contains(coordinate))
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
            DockBuildDrawer(coordinate);
            ShowSelectionVisual(coordinate, 0f, isPlacement: true);
            towerPreview.gameObject.SetActive(false);
            rangePreview.gameObject.SetActive(false);
            RefreshUi();
            return true;
        }

        public void SelectTowerType(TowerDefinitionId id)
        {
            if (!battle.CanAcceptInput(BattleInputOwner.Placement) || !_hasSelection || _hasTowerSelection || _isSellConfirmationVisible ||
                battle.Simulation.State.Phase != BattlePhase.Preparation) return;
            foreach (var archetype in battle.Simulation.TowerCatalog.Archetypes)
            {
                if (archetype.Id != id || !archetype.ShowInCatalog) continue;
                SelectedTowerTypeId = id;
                _selectedDefinition = archetype.Levels[0];
                var locked = MissingElements(archetype).Count > 0;
                ShowSelectionVisual(SelectedCell, _selectedDefinition.Range, isPlacement: true);
                towerPreview.gameObject.SetActive(!locked);
                rangePreview.gameObject.SetActive(!locked);
                selectionHighlight.gameObject.SetActive(!locked);
                if (!locked) TowerDisplay.ApplyVisual(towerPreview, _selectedDefinition, 1);
                _catalogSimulation = null;
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
            _nextUpgradeTap = 0;
            ShowSelectionVisual(tower.Coordinate, tower.Definition.Range, isPlacement: false);
            RefreshUi();
        }

        private void DockBuildDrawer(GridCoordinate coordinate)
        {
            var side = worldCamera.WorldToViewportPoint(battle.GetCellCenter(coordinate)).x;
            var drawer = (RectTransform)confirmationPanel.transform;
            var anchor = side >= .5f ? Vector2.zero : Vector2.right;
            drawer.anchorMin = drawer.anchorMax = drawer.pivot = anchor;
            drawer.anchoredPosition = new Vector2(anchor.x == 0 ? 10 : -10, 10);
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
            if (!battle.CanAcceptInput(BattleInputOwner.Placement) || _isSellConfirmationVisible || !_hasSelection ||
                _hasTowerSelection || _selectedDefinition == null || battle.Simulation == null)
            {
                return;
            }

            var result = battle.PlaceTowerForOwner(SelectedTowerTypeId, SelectedCell, BattleInputOwner.Placement);
            if (result.Success)
            {
                SuppressPointerForCurrentFrame();
                CancelSelection();
            }
        }

        public TowerOperationResult UpgradeSelectedTower()
        {
            if (!battle.CanAcceptInput(BattleInputOwner.Placement))
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

            var result = battle.UpgradeTowerForOwner(SelectedTowerId.Value, BattleInputOwner.Placement);
            var tower = FindSelectedTower();
            if (result.Success && tower != null)
                ShowSelectionVisual(tower.Coordinate, tower.Definition.Range, isPlacement: false);
            RefreshUi();
            return result;
        }

        public void RequestSellSelectedTower()
        {
            if (!battle.CanAcceptInput(BattleInputOwner.Placement) || _isSellConfirmationVisible || !SelectedTowerId.HasValue ||
                battle.Simulation.State.Phase != BattlePhase.Preparation)
            {
                return;
            }

            var tower = FindSelectedTower();
            if (tower == null) return;
            sellConfirmationText.text = $"Sell {TowerDisplay.ArchetypeName(tower.Definition.Id)}?\n" +
                $"Refund: {(long)tower.InvestedGold * 70 / 100} gold";
            _sellConfirmationTowerId = SelectedTowerId;
            _isSellConfirmationVisible = true;
            RefreshUi();
        }

        public TowerOperationResult ConfirmSellSelectedTower()
        {
            if (!battle.CanAcceptInput(BattleInputOwner.Sell) || !_isSellConfirmationVisible || !_sellConfirmationTowerId.HasValue ||
                SelectedTowerId != _sellConfirmationTowerId)
            {
                return TowerOperationResult.Failed(TowerOperationFailureReason.ConfirmationRequired);
            }

            var result = battle.SellTowerForOwner(_sellConfirmationTowerId.Value, BattleInputOwner.Sell);
            if (result.Success)
            {
                ClearSelection();
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
            if (!battle.CanAcceptInput(BattleInputOwner.Sell)) return;
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
            if (battle != null && !battle.CanAcceptInput(BattleInputOwner.Placement)) return;
            ClearSelection();
        }

        internal void ClearSelection()
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
            if (!battle.CanAcceptInput(BattleInputOwner.Placement) || _isSellConfirmationVisible || battle.Simulation == null)
            {
                return;
            }

            CancelSelection();
            battle.StartWave();
            RefreshUi();
        }

        public void ContinueToNextWave()
        {
            if (!battle.CanAcceptInput(BattleInputOwner.Placement) || _isSellConfirmationVisible || battle.Simulation == null)
            {
                return;
            }

            ClearSelection();
            battle.ContinueToNextWave();
            RefreshUi();
        }

        private void RefreshUi()
        {
            var state = battle.Simulation.State;
            var isPaused = !battle.CanAcceptInput(BattleInputOwner.Placement);
            var isPreparing = state.Phase == BattlePhase.Preparation;
            if (!isPreparing && _hasSelection && !_hasTowerSelection)
            {
                ClearSelection();
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

            var isSellConfirmationActive = battle.CanAcceptInput(BattleInputOwner.Sell) && isPreparing && tower != null &&
                                           _isSellConfirmationVisible;
            confirmationPanel.SetActive(isPreparing && !isSellConfirmationActive &&
                                        _hasSelection && !_hasTowerSelection);
            confirmButton.interactable = !isPaused && isPreparing && !isSellConfirmationActive &&
                                         _hasSelection && !_hasTowerSelection && _selectedDefinition != null &&
                                         battle.Simulation.GetTowerAvailability(new PlaceTowerCommand(SelectedTowerTypeId, SelectedCell)).CanPlace;
            var refreshCatalog = _catalogSimulation != battle.Simulation || _catalogGold != state.Gold ||
                _catalogPhase != state.Phase || _catalogCell != SelectedCell ||
                !ReferenceEquals(_catalogProgression, state.ElementProgression);
            var cardIndex = 0;
            foreach (var archetype in battle.Simulation.TowerCatalog.Archetypes)
            {
                if (!archetype.ShowInCatalog || cardIndex >= _catalogButtons.Count) continue;
                var button = _catalogButtons[cardIndex++];
                button.interactable = !isPaused && isPreparing && !isSellConfirmationActive;
                if (!refreshCatalog) continue;
                var availability = battle.Simulation.GetTowerAvailability(new PlaceTowerCommand(archetype.Id, SelectedCell));
                var locked = MissingElements(archetype).Count > 0;
                var availabilityLabel = locked ? "Locked" :
                    availability.FailureReason == PlaceTowerFailureReason.InsufficientGold ? "Low gold" : string.Empty;
                var selected = archetype.Id == SelectedTowerTypeId;
                var status = selected ? "Selected" : availabilityLabel;
                var text = button.GetComponentInChildren<UnityEngine.UI.Text>(true);
                text.text = TowerDisplay.Name(archetype.Id);
                text.color = selected ? new Color32(231, 194, 121, 255) : new Color32(245, 233, 209, 255);
                SetActionLabel(button.transform.Find("Catalog Price")?.GetComponent<UnityEngine.UI.Text>(), $"{archetype.Levels[0].Price} gold");
                SetActionLabel(button.transform.Find("Catalog State")?.GetComponent<UnityEngine.UI.Text>(), status);
                var glyph = button.GetComponentInChildren<VeilwardUiGlyph>(true);
                if (glyph != null)
                {
                    glyph.Glyph = locked ? VeilwardGlyph.Lock : TowerDisplay.Glyph(archetype.Id);
                    glyph.color = locked ? new Color32(203, 191, 175, 255) : TowerDisplay.ElementTint(archetype.Id);
                }
                if (selected)
                {
                    var requirement = AvailabilityText(archetype, availability);
                    buildStatsText.text = $"Price: {_selectedDefinition.Price} gold\n{TowerDisplay.CoreStats(_selectedDefinition)}\n" +
                        (requirement.Length == 0 ? TowerDisplay.Effect(_selectedDefinition) : requirement);
                }
            }
            _catalogSimulation = battle.Simulation; _catalogGold = state.Gold; _catalogPhase = state.Phase;
            _catalogCell = SelectedCell; _catalogProgression = state.ElementProgression;
            cancelButton.interactable = !isPaused && isPreparing && !isSellConfirmationActive &&
                                        _hasSelection && !_hasTowerSelection;
            startWaveButton.interactable = !isPaused && isPreparing && !isSellConfirmationActive &&
                battle.Simulation.CheckStartWave() == WaveTransitionResult.Started;
            nextWaveButton.interactable = !isPaused && state.Phase == BattlePhase.WaveComplete &&
                                          state.WaveIndex + 1 < state.WaveCount && !isSellConfirmationActive &&
                battle.Simulation.CheckContinueToNextWave() == WaveTransitionResult.Continued;
            towerPanel.SetActive(tower != null);
            sellButton.interactable = !isPaused && isPreparing && tower != null && !isSellConfirmationActive;
            sellConfirmationPanel.SetActive(isSellConfirmationActive);
            confirmSellButton.interactable = isSellConfirmationActive;
            cancelSellButton.interactable = isSellConfirmationActive;
            if (tower == null)
            {
                upgradeButton.interactable = false;
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
                var name = TowerDisplay.ArchetypeName(tower.Definition.Id);
                var element = TowerDisplay.Name(tower.Definition.Id);
                var title = name == element ? name : $"{name} · {element}";
                towerStatsText.text = $"{title} · Level: {tower.Level}\n" +
                    TowerDisplay.CoreStats(tower.Definition) + "\n" + TowerDisplay.Effect(tower.Definition) + "\n" +
                    (hasNextLevel ? $"Upgrade: {nextLevel.Price} gold" : "Upgrade: MAX") +
                    $" · Sell refund: {refund} gold";
            }
            var actions = (tower.Definition, state.Gold, refund, state.Phase);
            if (_shownTowerActions != actions)
            {
                _shownTowerActions = actions;
                SetActionLabel(_upgradeLabel, !isPreparing ? "Build phase only" : !hasNextLevel ? "Max level" :
                    state.Gold < nextLevel.Price ? $"Need {nextLevel.Price} gold" : $"Upgrade · {nextLevel.Price}");
                SetActionLabel(_sellLabel, isPreparing ? $"Sell · +{refund}" : "Build phase only");
            }
            upgradeButton.interactable = !isPaused && isPreparing && !isSellConfirmationActive &&
                                         hasNextLevel && state.Gold >= nextLevel.Price;
        }

        private static void SetActionLabel(UnityEngine.UI.Text label, string value)
        {
            if (label != null && label.text != value) label.text = value;
        }

        private List<string> MissingElements(TowerArchetypeDefinition archetype)
        {
            var missing = new List<string>();
            var unlocked = battle.Simulation.State.ElementProgression.UnlockedElements;
            foreach (var required in archetype.RequiredElements)
            {
                var found = false;
                foreach (var element in unlocked) if (element == required) { found = true; break; }
                if (!found) missing.Add(required.ToString().ToUpperInvariant());
            }
            return missing;
        }

        private string AvailabilityText(TowerArchetypeDefinition archetype, TowerAvailability availability)
        {
            var missing = MissingElements(archetype);
            if (missing.Count > 0) return "Locked — Missing: " + string.Join(" + ", missing);
            return availability.FailureReason switch
            {
                PlaceTowerFailureReason.None => string.Empty,
                PlaceTowerFailureReason.InsufficientGold => "Not enough gold",
                PlaceTowerFailureReason.PendingElementChoice => "Choose an element first",
                PlaceTowerFailureReason.InvalidPhase => "Build between waves",
                PlaceTowerFailureReason.Blocked or PlaceTowerFailureReason.OutsideMap => "Choose a building tile",
                PlaceTowerFailureReason.Occupied => "This tile is occupied",
                _ => "This tower cannot be built here"
            };
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

        internal void DispatchPointer(Vector2 position, int pointerId, TouchPhase phase)
        {
            if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled) { _heldPointers.Remove(pointerId); return; }
            if (phase != TouchPhase.Began || _heldPointers.Contains(pointerId)) return;
            var secondary = _heldPointers.Count > 0; _heldPointers.Add(pointerId);
            if (secondary || battle.IsPaused || Time.frameCount <= _pointerSuppressedUntilFrame) return;
            TryHandlePointer(position, pointerId == -1 ? (int?)null : pointerId);
        }

        private void TryHandlePointer(Vector2 screenPosition, int? pointerId)
        {
            if ((!battle.CanAcceptInput(BattleInputOwner.Placement) && !battle.CanAcceptInput(BattleInputOwner.Inspector) &&
                !battle.CanAcceptInput(BattleInputOwner.Ability)) ||
                _isSellConfirmationVisible || Time.frameCount <= _pointerSuppressedUntilFrame)
                return;
            if (EventSystem.current != null)
            {
                // Проверяем текущую позицию: cached IsPointerOverGameObject зависит от порядка Update.
                var pointer = new PointerEventData(EventSystem.current) { position = screenPosition };
                _uiHits.Clear();
                EventSystem.current.RaycastAll(pointer, _uiHits);
                if (_uiHits.Count > 0) return;
            }

            if (battle.ActiveModalOwner == BattleInputOwner.Ability)
            {
                var abilityRay = worldCamera.ScreenPointToRay(screenPosition);
                if (new Plane(Vector3.up, Vector3.zero).Raycast(abilityRay, out var abilityDistance))
                {
                    var point = abilityRay.GetPoint(abilityDistance);
                    if (float.IsFinite(point.x) && float.IsFinite(point.z))
                        battle.GetComponent<AbilityController>().SelectTarget(new PathPosition(point.x, point.z));
                }
                return;
            }
            if (enemyInspector != null)
            {
                if (enemyInspector.TrySelectAt(screenPosition)) return;
                // Открытый инспектор поглощает world tap; закрытие — отдельная кнопка.
                if (enemyInspector.SelectedEnemyId.HasValue) return;
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
