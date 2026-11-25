using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Veilward.Presentation;

namespace Veilward.Editor
{
    public static partial class BattleSceneBuilder
    {
        private static readonly Color Ink = new Color32(16, 20, 33, 255);
        private static readonly Color Surface = new Color32(27, 34, 51, 250);
        private static readonly Color Raised = new Color32(39, 50, 70, 255);
        private static readonly Color Jade = new Color32(39, 104, 83, 255);
        private static readonly Color Gilt = new Color32(231, 194, 121, 255);
        private static readonly Color Stone = new Color32(245, 233, 209, 255);
        private static readonly Color Muted = new Color32(203, 191, 175, 255);
        private static readonly Color Coral = new Color32(244, 151, 131, 255);
        private static readonly Vector2 Center = new Vector2(.5f, .5f);
        private static readonly Vector2 Left = new Vector2(0, .5f);
        private static readonly Vector2 Right = new Vector2(1, .5f);
        private static readonly Vector2 Top = new Vector2(.5f, 1);
        private static readonly Vector2 Bottom = new Vector2(.5f, 0);
        private static readonly Vector2 TopLeft = Vector2.up;
        private static readonly Vector2 TopRight = Vector2.one;
        private static readonly Vector2 BottomRight = Vector2.right;

        private static void BuildMainMenu()
        {
            var scene = EditorSceneManager.OpenScene(MainMenuScenePath);
            foreach (var root in scene.GetRootGameObjects())
                Object.DestroyImmediate(root);

            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 1, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Ink;
            var canvas = UiCanvas("Main Menu Canvas");
            var safe = SafeRoot(canvas, "Menu Safe Area");
            var composition = Rect("Title Composition", safe, Center, 0, 0, 660, 348);
            var emblem = Rect("Seal Artwork", composition, Left, 0, 0, 282, 282);
            Image("Tile", emblem, Center, 0, 0, 224, 224, new Color32(28, 53, 58, 255), false)
                .rectTransform.localEulerAngles = new Vector3(0, 0, 45);
            Icon("Inner seal", emblem, Center, 0, 0, 242, VeilwardGlyph.Seal, Gilt);
            Icon("Outer seal", emblem, Center, 0, 0, 306, VeilwardGlyph.Seal, new Color32(94, 134, 125, 90));
            foreach (var (glyph, x, y) in new[]
            {
                (VeilwardGlyph.Flame, 0f, 103f), (VeilwardGlyph.Tide, 103f, 0f),
                (VeilwardGlyph.Gale, 0f, -103f), (VeilwardGlyph.Stone, -103f, 0f)
            })
            {
                Image(glyph + " tile", emblem, Center, x, y, 34, 34, Ink, false)
                    .rectTransform.localEulerAngles = new Vector3(0, 0, 45);
                Icon(glyph.ToString(), emblem, Center, x, y, 24, glyph, ElementColor(glyph));
            }

            var actions = Rect("Title and Actions", composition, Right, 0, 0, 336, 348);
            Label("Subtitle", actions, TopLeft, 0, -11, 336, 22, "S P I R I T   D E F E N S E", 15, Gilt, bold: true);
            Label("Title", actions, TopLeft, 0, -32, 336, 74, "VEILWARD", 59, Stone, bold: true);
            Image("Rule", actions, TopLeft, 0, -116, 336, 1, Gilt, false);
            var newGame = Action("New Game Button", actions, "New Game", TopLeft, 0, -135, 336, Jade);
            var continueButton = Action("Continue Button", actions, "Continue", TopLeft, 0, -203, 336, Raised);
            continueButton.interactable = false;
            var status = Label("Status Text", actions, TopLeft, 0, -275, 216, 63, "", 15, Muted, TextAnchor.UpperLeft);
            var retry = Action("Retry Load Button", actions, "Retry", TopRight, 0, -275, 108, Raised);
            retry.gameObject.SetActive(false);
            var opener = Action("Audio Button", safe, "Audio", Vector2.zero, 10, 10, 96, Raised);
            var menuActions = safe.gameObject.AddComponent<CanvasGroup>();
            var audioPanel = CreateAudioPanel(canvas, opener, menuActions, out var audioSafe);
            ApplySafeRoots(canvas, safe, audioSafe);
            CreateEventSystem();
            var menu = new GameObject("Main Menu Controller").AddComponent<MainMenuController>();
            var audioOwner = AddAudioOwner(menu.gameObject, false);
            Bind(audioPanel, ("audioOwner", audioOwner));
            Bind(menu, ("audioOwner", audioOwner), ("audioSettings", audioPanel),
                ("scenario", AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(ConfigPath)),
                ("newGameButton", newGame), ("continueButton", continueButton), ("statusText", status), ("retryButton", retry));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static HudReferences CreateHud()
        {
            var canvas = UiCanvas("HUD");
            var safe = SafeRoot(canvas, "Battle Safe Area");
            var stats = Card("Stats Panel", safe, TopLeft, 10, -10, 318, 60);
            var heart = Metric(stats, "Heart", 10, 84, VeilwardGlyph.Heart, Coral);
            var gold = Metric(stats, "Gold", 106, 80, VeilwardGlyph.Gold, Gilt);
            var wave = Metric(stats, "Wave", 198, 110, VeilwardGlyph.Wave, new Color32(139, 214, 186, 255));
            var phasePlate = Card("Phase Plate", safe, TopLeft, 336, -10, 112, 60);
            var stage = Label("Battle Phase", phasePlate, Center, 0, 0, 108, 40, "", 15, Stone, TextAnchor.MiddleCenter, true);
            var start = Action("Start Wave Button", safe, "Start Wave", TopRight, -78, -10, 138, Jade);
            var next = Action("Next Wave Button", safe, "Next Wave", TopRight, -78, -10, 138, Jade);
            next.gameObject.SetActive(false);
            var pause = Action("Pause Button", safe, "Pause", TopRight, -10, -10, 60, Raised);

            var boss = Card("Boss Panel", safe, TopLeft, 10, -78, 244, 74);
            var bossNotice = Label("Boss Notice", boss, TopLeft, 12, -8, 220, 35, "", 14, Coral, TextAnchor.UpperLeft, true);
            var bossHealth = Label("Boss Health", boss, TopLeft, 12, -42, 220, 18, "", 14, Stone, bold: true);
            var bossBar = Image("Boss HP Bar", boss, Bottom, 0, 8, 220, 4, Ink, false).rectTransform;
            var bossFill = Image("Boss HP Fill", bossBar, Center, 0, 0, 0, 0, Coral, false);
            Stretch(bossFill.rectTransform);
            boss.gameObject.SetActive(false);

            var build = Card("Placement Panel", safe, BottomRight, -10, 10, 266, 290);
            Label("Build Title", build, TopLeft, 10, -4, 246, 21, "Build a tower", 17, Gilt, bold: true);
            var catalog = Rect("Tower Catalog", build, Top, 0, -26, 246, 141);
            var layout = catalog.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(78, 68);
            layout.spacing = new Vector2(6, 5);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            var template = Action("Catalog Button Template", catalog, "", Center, 0, 0, 78, Raised, 68, 13);
            ButtonIcon(template, VeilwardGlyph.Flame, Gilt, 14, 3, top: true);
            var catalogName = template.GetComponentInChildren<Text>();
            SetRect(catalogName.rectTransform, Top, new Vector2(0, -18), new Vector2(76, 16));
            Label("Catalog Price", template.transform, Top, 0, -34, 76, 16, "", 13, Stone, TextAnchor.MiddleCenter, true);
            Label("Catalog State", template.transform, Top, 0, -50, 76, 15, "", 12, Muted, TextAnchor.MiddleCenter);
            template.gameObject.SetActive(false);
            var buildStats = Label("Build Stats", build, TopLeft, 10, -170, 246, 48, "Choose a tower type", 13, Stone, TextAnchor.UpperLeft);
            var confirm = Action("Confirm Button", build, "Build", BottomRight, -10, 8, 118, Jade);
            var cancel = Action("Cancel Button", build, "Cancel", Vector2.zero, 10, 8, 118, Raised);
            build.gameObject.SetActive(false);

            var tower = Card("Tower Panel", safe, BottomRight, -10, 10, 350, 156);
            var towerStats = Label("Tower Stats", tower, TopLeft, 12, -8, 328, 72, "", 14, Stone, TextAnchor.UpperLeft);
            var closeTower = Action("Close Tower Button", tower, "Close", Vector2.zero, 10, 8, 68, Raised, fontSize: 14);
            var upgrade = Action("Upgrade Button", tower, "Upgrade", BottomRight, -10, 8, 128, Jade, fontSize: 14);
            var sell = Action("Sell Button", tower, "Sell", BottomRight, -146, 8, 118, Raised, fontSize: 14);
            tower.gameObject.SetActive(false);

            var inspector = Card("Enemy Inspector Panel", safe, BottomRight, -10, 10, 262, 234);
            var inspectorText = Label("Enemy Inspector Details", inspector, TopLeft, 12, -11, 238, 151, "", 14, Stone, TextAnchor.UpperLeft);
            var closeInspector = Action("Close Inspector Button", inspector, "Close", BottomRight, -10, 8, 100, Raised);
            inspector.gameObject.SetActive(false);

            var abilityBegin = Action("Ability Button", safe, "Rift Seal", Vector2.zero, 10, 10, 166, Jade, fontSize: 14);
            ButtonIcon(abilityBegin, VeilwardGlyph.Seal, Gilt, 31, 10);
            var abilityFeedback = Card("Ability Feedback", safe, Vector2.zero, 10, 77, 254, 44);
            var feedback = Label("Ability Feedback Text", abilityFeedback, Center, 0, 0, 234, 40, "", 14, Stone);
            abilityFeedback.gameObject.SetActive(false);
            var ability = Card("Ability Panel", safe, Vector2.zero, 10, 10, 252, 130);
            var abilityStatus = Label("Ability Status", ability, TopLeft, 12, -10, 228, 43, "", 15, Stone, TextAnchor.UpperLeft);
            var abilityConfirm = Action("Confirm Ability Button", ability, "Cast Seal", Vector2.zero, 10, 8, 112, Jade);
            var abilityCancel = Action("Cancel Ability Button", ability, "Cancel", BottomRight, -10, 8, 112, Raised);
            ability.gameObject.SetActive(false);

            var sellOverlay = Modal(canvas, "Sell Confirmation Panel", out var sellSafe);
            var sellCard = Card("Sell Dialog", sellSafe, Center, 0, 0, 332, 164);
            var sellLabel = Label("Sell Confirmation Label", sellCard, Top, 0, -14, 308, 72, "Sell this tower?", 20, Stone, TextAnchor.MiddleCenter, true);
            var confirmSell = Action("Confirm Sell Button", sellCard, "Sell tower", BottomRight, -12, 12, 146, Jade);
            var cancelSell = Action("Cancel Sell Button", sellCard, "Keep tower", Vector2.zero, 12, 12, 146, Raised);

            var choiceOverlay = Modal(canvas, "Element Choice Overlay", out var choiceSafe);
            var choice = Card("Element Choice Card", choiceSafe, Center, 0, 0, 444, 235);
            var choiceTitle = Label("Choice Title", choice, Top, 0, -12, 420, 34, "", 27, Gilt, TextAnchor.MiddleCenter, true);
            var choiceHelp = Label("Choice Help", choice, Top, 0, -52, 420, 28, "", 15, Stone, TextAnchor.MiddleCenter);
            var choiceRow = Rect("Element Options", choice, Top, 0, -87, 420, 66);
            var choicesLayout = choiceRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            choicesLayout.spacing = 8;
            choicesLayout.childControlWidth = choicesLayout.childControlHeight = true;
            choicesLayout.childForceExpandWidth = true;
            var ids = new[] { "flame", "tide", "gale", "stone" };
            var glyphs = new[] { VeilwardGlyph.Flame, VeilwardGlyph.Tide, VeilwardGlyph.Gale, VeilwardGlyph.Stone };
            var options = new Button[ids.Length];
            for (var i = 0; i < ids.Length; i++)
            {
                options[i] = Action(ids[i], choiceRow, ids[i], Center, 0, 0, 98, Raised, 66, 14);
                ButtonIcon(options[i], glyphs[i], ElementColor(glyphs[i]), 20, 5, top: true);
            }
            var choiceMenu = Action("Choice Menu Button", choice, "Main Menu", Vector2.zero, 12, 12, 124, Raised);
            var cancelChoice = Action("Cancel Element Button", choice, "Clear", Bottom, 0, 12, 96, Raised);
            var confirmChoice = Action("Confirm Element Button", choice, "Unlock", BottomRight, -12, 12, 124, Jade);

            var resultOverlay = Modal(canvas, "Battle Result Overlay", out var resultSafe);
            var result = Card("Battle Result", resultSafe, Center, 0, -14, 350, 256);
            Icon("Result Seal", result, Top, 0, -10, 40, VeilwardGlyph.Seal, Gilt);
            var resultTitle = Label("Result Title", result, Top, 0, -52, 326, 38, "", 32, Gilt, TextAnchor.MiddleCenter, true);
            var resultDetails = Label("Result Details", result, Top, 0, -96, 326, 63, "", 17, Stone, TextAnchor.MiddleCenter);
            var resultSave = Label("Result Save Status", result, Top, 0, -163, 326, 18, "", 14, Coral, TextAnchor.MiddleCenter);
            var resultMenu = Action("Result Menu Button", result, "Main Menu", Bottom, 0, 10, 180, Jade);

            var warningSafe = SafeRoot(canvas, "Save Warning Safe Area");
            var warning = Card("Save Warning", warningSafe, TopRight, -10, -78, 306, 64);
            var saveStatus = Label("Save Warning Text", warning, Left, 12, 0, 173, 62, "", 15, Coral);
            var retrySave = Action("Retry Save Button", warning, "Retry Save", Right, -8, 0, 106, Raised, fontSize: 14);
            warning.gameObject.SetActive(false);

            var diagnostics = Card("Battle Diagnostics", safe, TopLeft, 10, -78, 300, 259);
            var diagnosticStats = Label("Stats", diagnostics, TopLeft, 12, -8, 276, 152, "", 14, Stone, TextAnchor.UpperLeft);
            var reaction = Label("Reaction Summary", diagnostics, TopLeft, 12, -164, 276, 44, "", 14, Gilt, TextAnchor.UpperLeft);
            var saveDiagnostics = Label("Save Status", diagnostics, TopLeft, 12, -212, 276, 39, "", 14, Muted, TextAnchor.UpperLeft);
            diagnostics.gameObject.SetActive(false);

            var pauseOverlay = Modal(canvas, "Pause Overlay", out var pauseSafe);
            var pauseCard = Card("Pause Dialog", pauseSafe, Center, 0, 0, 350, 196);
            Icon("Pause Seal", pauseCard, Top, 0, -12, 34, VeilwardGlyph.Seal, Gilt);
            Label("Paused Label", pauseCard, Top, 0, -49, 320, 38, "Paused", 32, Gilt, TextAnchor.MiddleCenter, true);
            var pauseSave = Label("Pause Save Status", pauseCard, Top, 0, -91, 320, 33, "", 15, Stone, TextAnchor.MiddleCenter);
            var resume = Action("Resume Button", pauseCard, "Resume", BottomRight, -12, 12, 104, Jade);
            var menu = Action("Main Menu Button", pauseCard, "Main Menu", Vector2.zero, 12, 12, 104, Raised);
            var opener = Action("Audio Button", pauseCard, "Audio", Bottom, 0, 12, 104, Raised);
            var audioPanel = CreateAudioPanel(canvas, opener, pauseCard.gameObject.AddComponent<CanvasGroup>(), out var audioSafe);

            ApplySafeRoots(canvas, safe, sellSafe, choiceSafe, resultSafe, warningSafe, pauseSafe, audioSafe);
            var playerHud = canvas.gameObject.AddComponent<BattlePlayerHud>();
            Bind(playerHud, ("heart", heart), ("gold", gold), ("wave", wave), ("phase", stage),
                ("result", resultOverlay.gameObject), ("resultTitle", resultTitle), ("resultDetails", resultDetails),
                ("resultSave", resultSave), ("saveWarning", warning.gameObject), ("saveStatus", saveStatus),
                ("pauseSave", pauseSave), ("diagnostics", diagnostics.gameObject), ("startWave", start), ("nextWave", next),
                ("retrySave", retrySave), ("resultMenu", resultMenu), ("choiceMenu", choiceMenu));

            return new HudReferences
            {
                PlayerHud = playerHud, AudioSettings = audioPanel,
                BossPanel = boss.gameObject, BossNotice = bossNotice, BossHealth = bossHealth, BossFill = bossFill,
                AbilityBegin = abilityBegin, AbilityConfirm = abilityConfirm, AbilityCancel = abilityCancel,
                AbilityPanel = ability.gameObject, AbilityStatus = abilityStatus, AbilityFeedback = feedback,
                ChoicePanel = choiceOverlay.gameObject, ChoiceSafeArea = choiceSafe, ChoiceTitle = choiceTitle,
                ProgressionSummary = choiceHelp, ChoiceOptions = options, ConfirmChoice = confirmChoice,
                CancelChoice = cancelChoice, RetrySave = retrySave, MainMenuButton = menu,
                InspectorPanel = inspector.gameObject, InspectorText = inspectorText, CloseInspectorButton = closeInspector,
                ReactionSummaryText = reaction, StatsText = diagnosticStats, SaveStatusText = saveDiagnostics,
                CatalogRoot = catalog, CatalogButtonTemplate = template, BuildStatsText = buildStats,
                SellConfirmationText = sellLabel, ConfirmationPanel = build.gameObject, ConfirmButton = confirm, CancelButton = cancel,
                StartWaveButton = start, NextWaveButton = next, PauseButton = pause, PauseOverlay = pauseOverlay.gameObject, ResumeButton = resume,
                TowerPanel = tower.gameObject, TowerStatsText = towerStats, UpgradeButton = upgrade, SellButton = sell, CloseTowerButton = closeTower,
                SellConfirmationPanel = sellOverlay.gameObject, ConfirmSellButton = confirmSell, CancelSellButton = cancelSell
            };
        }

        private static Text Metric(Transform parent, string name, float x, float width, VeilwardGlyph glyph, Color accent)
        {
            Icon(name + " Icon", parent, Left, x, 0, 23, glyph, accent);
            Label(name + " Label", parent, TopLeft, x + 29, -8, width - 29, 18, name, 13, Muted);
            return Label(name + " Value", parent, TopLeft, x + 29, -27, width - 24, 25, "", 21, Stone, bold: true);
        }

        private static RectTransform UiCanvas(string name)
        {
            var obj = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            obj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = obj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referenceResolution = new Vector2(740, 400);
            return (RectTransform)obj.transform;
        }

        private static RectTransform SafeRoot(Transform parent, string name)
        {
            var root = Rect(name, parent, Center, 0, 0, 0, 0);
            Stretch(root);
            return root;
        }

        private static void ApplySafeRoots(RectTransform canvas, params RectTransform[] roots)
        {
            var area = canvas.gameObject.AddComponent<BattleHudSafeArea>();
            var serialized = new SerializedObject(area);
            var property = serialized.FindProperty("safeRoots");
            property.arraySize = roots.Length;
            for (var i = 0; i < roots.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = roots[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform Modal(Transform canvas, string name, out RectTransform safe)
        {
            var dim = Image(name, canvas, Center, 0, 0, 0, 0, new Color32(12, 16, 27, 194), true).rectTransform;
            Stretch(dim);
            safe = SafeRoot(dim, name + " Safe Area");
            dim.gameObject.SetActive(false);
            return dim;
        }

        private static RectTransform Card(string name, Transform parent, Vector2 anchor, float x, float y, float w, float h)
        {
            var card = Image(name, parent, anchor, x, y, w, h, Surface, true).rectTransform;
            Image("Jade edge", card, TopLeft, 0, 0, w, 2, new Color32(88, 157, 133, 255), false);
            return card;
        }

        private static Button Action(string name, Transform parent, string title, Vector2 anchor, float x, float y,
            float w, Color baseColor, float h = 60, int fontSize = 16)
        {
            var image = Image(name, parent, anchor, x, y, w, h, baseColor, true);
            var button = image.gameObject.AddComponent<VeilwardUiButton>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.91f, 1f, .92f);
            colors.pressedColor = new Color(.62f, .7f, .66f);
            colors.selectedColor = new Color(1f, .91f, .74f);
            colors.disabledColor = new Color(.57f, .58f, .64f);
            colors.fadeDuration = .12f;
            button.colors = colors;
            var text = Label("Label", image.transform, Center, 0, 0, w, h, title, fontSize, Stone, TextAnchor.MiddleCenter, true);
            Stretch(text.rectTransform, new Vector2(4, 2), new Vector2(-4, -2));
            var edge = Image("Bottom edge", image.transform, Bottom, 0, 0, w - 8, 2, new Color32(16, 20, 33, 125), false).rectTransform;
            edge.anchorMin = Vector2.zero;
            edge.anchorMax = Vector2.right;
            edge.offsetMin = new Vector2(4, 0);
            edge.offsetMax = new Vector2(-4, 2);
            Bind(button, ("face", text.rectTransform));
            return button;
        }

        private static void ButtonIcon(Button button, VeilwardGlyph glyph, Color color, float size, float inset, bool top = false)
        {
            var label = button.GetComponentInChildren<Text>();
            if (top)
            {
                Icon("Symbol", button.transform, Top, 0, -inset, size, glyph, color);
                Stretch(label.rectTransform, new Vector2(1, 2), new Vector2(-1, -size - inset - 5));
            }
            else
            {
                Icon("Symbol", button.transform, Left, inset, 0, size, glyph, color);
                Stretch(label.rectTransform, new Vector2(size + inset + 4, 2), new Vector2(-5, -2));
            }
        }

        private static VeilwardUiGlyph Icon(string name, Transform parent, Vector2 anchor, float x, float y, float size,
            VeilwardGlyph kind, Color color)
        {
            var rect = Rect(name, parent, anchor, x, y, size, size);
            rect.gameObject.AddComponent<CanvasRenderer>();
            var icon = rect.gameObject.AddComponent<VeilwardUiGlyph>();
            icon.Glyph = kind;
            icon.color = color;
            icon.raycastTarget = false;
            return icon;
        }

        private static Color ElementColor(VeilwardGlyph glyph) => glyph switch
        {
            VeilwardGlyph.Flame => Coral,
            VeilwardGlyph.Tide => new Color32(132, 195, 234, 255),
            VeilwardGlyph.Gale => new Color32(139, 214, 186, 255),
            _ => Gilt
        };

        private static Image Image(string name, Transform parent, Vector2 anchor, float x, float y, float w, float h, Color color, bool hit)
        {
            var image = Rect(name, parent, anchor, x, y, w, h).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = hit;
            return image;
        }

        private static Text Label(string name, Transform parent, Vector2 anchor, float x, float y, float w, float h,
            string label, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft, bool bold = false)
        {
            var text = CreateText(name, parent, size, alignment);
            SetRect(text.rectTransform, anchor, new Vector2(x, y), new Vector2(w, h));
            text.text = label;
            text.color = color;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, float x, float y, float w, float h)
        {
            var rect = CreateUiObject(name, parent).GetComponent<RectTransform>();
            SetRect(rect, anchor, new Vector2(x, y), new Vector2(w, h));
            return rect;
        }

        private static void Stretch(RectTransform rect, Vector2 min = default, Vector2 max = default)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }

        private static void Bind(Component component, params (string field, Object value)[] fields)
        {
            var data = new SerializedObject(component);
            foreach (var (field, value) in fields) data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
