using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Veilward.Domain;

namespace Veilward.Presentation
{
    public sealed class BattlePlayerHud : MonoBehaviour
    {
        [SerializeField] private BattleSceneController battle;
        [SerializeField] private BattlePlacementController placement;
        [SerializeField] private BattlePauseController pause;
        [SerializeField] private AbilityController ability;
        [SerializeField] private ElementChoiceController choice;
        [SerializeField] private EnemyInspectorController inspector;
        [SerializeField] private BattleCheckpointController checkpoints;
        [SerializeField] private Text heart, gold, wave, phase, resultTitle, resultDetails, resultSave, saveStatus, pauseSave;
        [SerializeField] private Button startWave, nextWave, retrySave, resultMenu, choiceMenu;
        [SerializeField] private GameObject result, saveWarning, diagnostics;

        private BattlePhase _shownPhase = (BattlePhase)(-1);
        private int _heart = -1, _gold = -1, _wave = -1, _waveCount = -1, _kills = -1;

        private void OnEnable()
        {
            resultMenu.onClick.AddListener(battle.ReturnToMenu);
            choiceMenu.onClick.AddListener(battle.ReturnToMenu);
        }

        private void OnDisable()
        {
            resultMenu.onClick.RemoveListener(battle.ReturnToMenu);
            choiceMenu.onClick.RemoveListener(battle.ReturnToMenu);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (keyboard.f3Key.wasPressedThisFrame)
                diagnostics.SetActive(!diagnostics.activeSelf);
#endif
            if (!keyboard.escapeKey.wasPressedThisFrame) return;
            if (battle.IsPaused)
            {
                pause.Resume();
                return;
            }

            switch (battle.ActiveModalOwner)
            {
                case BattleInputOwner.Ability: ability.CancelPreview(); break;
                case BattleInputOwner.ElementChoice: choice.CancelSelection(); break;
                case BattleInputOwner.Inspector: inspector.Close(); break;
                case BattleInputOwner.Sell: placement.CancelSellConfirmation(); break;
                case BattleInputOwner.Placement: placement.CancelSelection(); break;
                default:
                    if (!result.activeSelf) pause.Pause();
                    break;
            }
        }

        private void LateUpdate()
        {
            var state = battle.Simulation?.State;
            if (state == null) return;
            var terminal = state.Phase == BattlePhase.Victory || state.Phase == BattlePhase.Defeat;
            var changedPhase = _shownPhase != state.Phase;
            if (terminal && changedPhase)
            {
                placement.ClearSelection();
                inspector.ClearSelection();
            }

            var pendingChoice = battle.ActiveModalOwner == BattleInputOwner.ElementChoice;
            var canRetry = !battle.IsPaused && battle.CanAcceptInput(BattleInputOwner.ElementChoice);
            var failedSave = !checkpoints.LastSaveSucceeded;
            startWave.gameObject.SetActive(state.Phase == BattlePhase.Preparation);
            nextWave.gameObject.SetActive(state.Phase == BattlePhase.WaveComplete);
            result.SetActive(terminal && !battle.IsPaused);
            saveWarning.SetActive(failedSave && !battle.IsPaused);
            if (failedSave)
            {
                var rect = (RectTransform)saveWarning.transform;
                var belowStrip = !terminal && !pendingChoice && placement.ShowsBuildDrawer && placement.BuildDrawerAtTop;
                var warnOnLeft = belowStrip && placement.SelectedCellOnRight;
                var anchor = belowStrip ? (warnOnLeft ? Vector2.zero : Vector2.right) : Vector2.one;
                var offset = new Vector2(warnOnLeft ? 10 : -10, belowStrip ? 8 : pendingChoice || terminal ? -6 : -78);
                rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
                rect.anchoredPosition = offset;
            }
            retrySave.interactable = failedSave && canRetry;
            SetText(saveStatus, canRetry ? "Save failed. Progress may roll back." :
                pendingChoice ? "Save failed. Choose an element to continue." : "Save failed. Close the card to retry.");
            SetText(pauseSave, failedSave ? "Unsaved progress may be lost." : "Continue resumes from the last saved wave.");
            SetText(resultSave, failedSave ? "The result could not be saved." : string.Empty);

            if (_heart != state.BaseHealth)
            {
                _heart = state.BaseHealth;
                heart.text = _heart.ToString();
                heart.color = _heart <= 5 ? new Color32(244, 151, 131, 255) : new Color32(245, 233, 209, 255);
            }
            if (_gold != state.Gold) { _gold = state.Gold; gold.text = _gold.ToString(); }
            if (_wave != state.WaveIndex || _waveCount != state.WaveCount)
            {
                _wave = state.WaveIndex;
                _waveCount = state.WaveCount;
                wave.text = $"{_wave + 1} / {_waveCount}";
            }
            if (changedPhase)
            {
                _shownPhase = state.Phase;
                phase.text = state.Phase switch
                {
                    BattlePhase.Preparation => "Prepare",
                    BattlePhase.Combat => "In battle",
                    BattlePhase.WaveComplete => "Wave cleared",
                    BattlePhase.Victory => "Victory",
                    _ => "Defeat"
                };
            }
            if (!terminal) return;
            if (changedPhase || _kills != state.Kills)
            {
                _kills = state.Kills;
                var won = state.Phase == BattlePhase.Victory;
                resultTitle.text = won ? "Victory" : "Defeat";
                resultTitle.color = won ? new Color32(39, 77, 66, 255) : new Color32(127, 48, 55, 255);
            }
            if (changedPhase || !ReferenceEquals(_resultState, state) || _resultHeart != _heart || _resultGold != _gold || _resultKills != _kills)
            {
                _resultState = state;
                _resultHeart = _heart;
                _resultGold = _gold;
                _resultKills = _kills;
                var reason = state.Phase == BattlePhase.Victory ? "The Heart of the Seal holds." : "The Heart of the Seal fell.";
                resultDetails.text = $"{reason}\nWave {_wave + 1} of {_waveCount}   ·   {_kills} defeated\nHeart {_heart}   ·   {_gold} gold";
            }
        }

        private BattleState _resultState;
        private int _resultHeart, _resultGold, _resultKills;

        private static void SetText(Text view, string value)
        {
            if (view.text != value) view.text = value;
        }
    }
}
