using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Veilward.Presentation;
using Object = UnityEngine.Object;

namespace Veilward.Editor
{
    public static partial class BattleSceneBuilder
    {
        private static readonly Color Ink = new Color32(16, 20, 33, 255);
        private static readonly Color PaperText = new Color32(57, 37, 43, 255);
        private static readonly Color PaperMuted = new Color32(103, 65, 57, 255);
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

        private const string UiArtPath = "Assets/_Game/Art/UI/InkAndLanterns/";
        private const string Primary = "CoralButton";
        private const string Secondary = "PaperButton";
        private const string Option = "DarkButton";
        private static Dictionary<string, Sprite> _inkSprites;

        [MenuItem("VEILWARD/Rebuild Player UI")]
        public static void RebuildPlayerUi()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            _inkSprites = null;
            var scene = EditorSceneManager.OpenScene(BattleScenePath);
            var battle = scene.GetRootGameObjects().Select(root => root.GetComponent<BattleSceneController>())
                .Single(component => component != null);
            var previous = scene.GetRootGameObjects().Single(root => root.GetComponent<BattlePlayerHud>() != null);
            var hud = CreateHud();
            BindPlayerUi(battle, hud);
            Object.DestroyImmediate(previous);
            ValidateRequiredReferences();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            BuildMainMenu();
        }

        private static void BuildMainMenu()
        {
            var scene = EditorSceneManager.OpenScene(MainMenuScenePath);
            var roots = scene.GetRootGameObjects();
            var camera = roots.Select(root => root.GetComponent<Camera>()).FirstOrDefault(component => component != null);
            if (camera == null)
            {
                camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(0, 1, -10);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Ink;
            }

            var canvas = UiCanvas("Main Menu Canvas");
            var backdrop = Image("Shrine Illustration", canvas, Center, 0, 0, 0, 0, Color.white, false);
            backdrop.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiArtPath + "MenuBackdrop.jpg")
                ?? throw new InvalidOperationException("Missing shrine illustration.");
            Stretch(backdrop.rectTransform);
            var crop = backdrop.gameObject.AddComponent<AspectRatioFitter>();
            crop.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            crop.aspectRatio = backdrop.sprite.rect.width / backdrop.sprite.rect.height;

            var safe = SafeRoot(canvas, "Menu Safe Area");
            var actions = Rect("Title and Actions", safe, Left, 26, 0, 320, 352);
            Decoration("Fox Crest", actions, Top, 0, -1, 82, 72, "FoxCrest");
            DisplayLabel("Title", actions, Top, 0, -62, 320, 65, "VEILWARD", 62, Stone);
            Label("Subtitle", actions, Top, 0, -125, 310, 22, "S P I R I T   D E F E N S E", 13, Gilt, TextAnchor.MiddleCenter, true);
            var continueButton = Action("Continue Button", actions, "Continue", Top, 0, -156, 270, Primary, fontSize: 22);
            ButtonIcon(continueButton, VeilwardGlyph.Flame, Gilt, 35, 25);
            continueButton.interactable = false;
            var newGame = Action("New Game Button", actions, "New Game", Top, 0, -221, 270, Secondary, fontSize: 22);
            ButtonDecoration(newGame, "Gate", 33, 25);

            Decoration("Checkpoint strip", actions, Top, 0, -286, 274, 56, "InkStrip");
            var status = Label("Status Text", actions, TopLeft, 41, -288, 145, 61, "", 13, Stone);
            var retry = Action("Retry Load Button", actions, "Retry", TopRight, -25, -288, 100, Option);
            retry.gameObject.SetActive(false);
            ApplySafeRoots(canvas, safe);
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null) CreateEventSystem();
            var menu = roots.Select(root => root.GetComponent<MainMenuController>()).FirstOrDefault(component => component != null);
            if (menu == null) menu = new GameObject("Main Menu Controller").AddComponent<MainMenuController>();
            Bind(menu, ("scenario", AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>(ConfigPath)),
                ("newGameButton", newGame), ("continueButton", continueButton), ("statusText", status), ("retryButton", retry));
            foreach (var root in roots)
                if (root.GetComponent<Canvas>() != null) Object.DestroyImmediate(root);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static HudReferences CreateHud()
        {
            var canvas = UiCanvas("HUD");
            var safe = SafeRoot(canvas, "Battle Safe Area");
            var stats = Card("Stats Panel", safe, TopLeft, 10, -10, 285, 54, "InkStrip");
            var heart = Metric(stats, "Heart", 17, 62, VeilwardGlyph.Heart);
            var gold = Metric(stats, "Gold", 91, 82, VeilwardGlyph.Gold);
            var wave = Metric(stats, "Wave", 178, 102, VeilwardGlyph.Wave);
            var phasePlate = Card("Phase Plate", safe, TopLeft, 304, -13, 128, 44, "PaperButton");
            var stage = DisplayLabel("Battle Phase", phasePlate, Center, 0, 0, 115, 35, "", 20, PaperText);
            var start = Action("Start Wave Button", safe, "Start Wave", TopRight, -78, -8, 137, Primary);
            var next = Action("Next Wave Button", safe, "Next Wave", TopRight, -78, -8, 137, Primary);
            next.gameObject.SetActive(false);
            var pause = Action("Pause Button", safe, "", TopRight, -10, -8, 60, Secondary);
            Icon("Pause Mark", pause.transform, Center, 0, 0, 44, VeilwardGlyph.Pause, PaperText);

            var boss = Card("Boss Panel", safe, TopLeft, 10, -67, 301, 79, "InkStrip");
            Decoration("Warden Mask", boss, Left, 9, 0, 61, 61, "Warden");
            // Справа остаётся полоса для двух status badges в #104.
            var bossNotice = Label("Boss Notice", boss, TopLeft, 72, -9, 219, 34, "", 12, Stone, TextAnchor.UpperLeft, true);
            var bossHealth = Label("Boss Health", boss, TopLeft, 72, -44, 162, 17, "", 12, Stone, bold: true);
            var bossBar = ArtImage("Boss HP Bar", boss, Vector2.zero, 68, 9, 169, 10, "HealthTrack", false).rectTransform;
            var bossFill = Image("Boss HP Fill", bossBar, Center, 0, 0, 0, 0, Coral, false);
            Stretch(bossFill.rectTransform);
            boss.gameObject.SetActive(false);

            var build = Card("Placement Panel", safe, BottomRight, -10, 10, 462, 184);
            var buildTitle = DisplayLabel("Build Title", build, TopLeft, 15, -7, 433, 26,
                "Build a tower", 23, Gilt, TextAnchor.MiddleLeft).rectTransform;
            buildTitle.anchorMax = Vector2.one;
            buildTitle.offsetMin = new Vector2(15, -33);
            buildTitle.offsetMax = new Vector2(-15, -7);
            var catalog = Rect("Tower Catalog", build, Top, 0, -34, 434, 70);
            var layout = catalog.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(68, 70);
            layout.spacing = new Vector2(5, 5);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 6;
            var template = Action("Catalog Button Template", catalog, "", Center, 0, 0, 68, Option, 70, 13);
            ButtonIcon(template, VeilwardGlyph.Flame, Gilt, 23, 3, top: true);
            var catalogName = template.GetComponentInChildren<Text>();
            SetRect(catalogName.rectTransform, Top, new Vector2(0, -25), new Vector2(65, 15));
            Label("Catalog Price", template.transform, Top, 0, -40, 64, 14, "", 12, Stone, TextAnchor.MiddleCenter, true);
            Label("Catalog State", template.transform, Top, 0, -54, 64, 13, "", 11, Muted, TextAnchor.MiddleCenter);
            template.gameObject.SetActive(false);
            var buildStats = Label("Build Stats", build, TopLeft, 16, -112, 218, 63, "Choose a tower type", 12, Stone, TextAnchor.UpperLeft);
            var confirm = Action("Confirm Button", build, "Build", BottomRight, -10, 10, 102, Primary);
            var cancel = Action("Cancel Button", build, "Cancel", BottomRight, -116, 10, 102, Secondary);
            build.gameObject.SetActive(false);

            var tower = Card("Tower Panel", safe, BottomRight, -10, 10, 353, 179);
            var towerStats = Label("Tower Stats", tower, TopLeft, 16, -15, 318, 91, "", 14, Stone, TextAnchor.UpperLeft);
            var closeTower = Action("Close Tower Button", tower, "Close", Vector2.zero, 10, 9, 73, Secondary, fontSize: 14);
            var upgrade = Action("Upgrade Button", tower, "Upgrade", BottomRight, -10, 9, 126, Primary, fontSize: 14);
            var sell = Action("Sell Button", tower, "Sell", BottomRight, -141, 9, 119, Secondary, fontSize: 14);
            tower.gameObject.SetActive(false);

            var inspector = Card("Enemy Inspector Panel", safe, Vector2.zero, 10, 10, 310, 225);
            var inspectorText = Label("Enemy Inspector Details", inspector, TopLeft, 13, -6, 284, 148, "", 14, Stone, TextAnchor.UpperLeft);
            var closeInspector = Action("Close Inspector Button", inspector, "Close", BottomRight, -10, 9, 100, Secondary);
            inspector.gameObject.SetActive(false);

            var abilityBegin = Action("Ability Button", safe, "Rift Seal", BottomRight, -10, 10, 166, "JadeButton", 72, 14);
            ButtonIcon(abilityBegin, VeilwardGlyph.Seal, Gilt, 57, 7);
            var abilityFeedback = Card("Ability Feedback", safe, BottomRight, -10, 85, 240, 45, "InkStrip");
            var feedback = Label("Ability Feedback Text", abilityFeedback, Center, 0, 0, 217, 39, "", 14, Stone, TextAnchor.MiddleCenter);
            abilityFeedback.gameObject.SetActive(false);
            var ability = Card("Ability Panel", safe, BottomRight, -10, 10, 264, 132);
            var abilityStatus = Label("Ability Status", ability, TopLeft, 16, -13, 231, 44, "", 14, Stone, TextAnchor.UpperLeft);
            var abilityConfirm = Action("Confirm Ability Button", ability, "Cast Seal", Vector2.zero, 10, 8, 116, Primary);
            var abilityCancel = Action("Cancel Ability Button", ability, "Cancel", BottomRight, -10, 8, 116, Secondary);
            ability.gameObject.SetActive(false);

            var sellOverlay = Modal(canvas, "Sell Confirmation Panel", out var sellSafe);
            var sellCard = Dialogue("Sell Dialog", sellSafe, 346, 173);
            var sellLabel = Label("Sell Confirmation Label", sellCard, Top, 0, -18, 293, 72, "Sell this tower?", 19, PaperText, TextAnchor.MiddleCenter, true);
            var confirmSell = Action("Confirm Sell Button", sellCard, "Sell tower", BottomRight, -15, 12, 150, Primary);
            var cancelSell = Action("Cancel Sell Button", sellCard, "Keep tower", Vector2.zero, 15, 12, 150, Secondary);

            var choiceOverlay = Modal(canvas, "Element Choice Overlay", out var choiceSafe);
            var choice = Dialogue("Element Choice Card", choiceSafe, 446, 242, -11);
            var choiceTitle = DisplayLabel("Choice Title", choice, Top, 0, -14, 372, 35, "", 31, PaperText);
            var choiceHelp = Label("Choice Help", choice, Top, 0, -51, 382, 29, "", 15, PaperMuted, TextAnchor.MiddleCenter);
            var choiceRow = Rect("Element Options", choice, Top, 0, -84, 412, 76);
            var choicesLayout = choiceRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            choicesLayout.spacing = 8;
            choicesLayout.childControlWidth = choicesLayout.childControlHeight = true;
            choicesLayout.childForceExpandWidth = true;
            var ids = new[] { "flame", "tide", "gale", "stone" };
            var glyphs = new[] { VeilwardGlyph.Flame, VeilwardGlyph.Tide, VeilwardGlyph.Gale, VeilwardGlyph.Stone };
            var options = new Button[ids.Length];
            for (var i = 0; i < ids.Length; i++)
            {
                options[i] = Action(ids[i], choiceRow, ids[i], Center, 0, 0, 96, Option, 76, 14);
                ButtonIcon(options[i], glyphs[i], Gilt, 34, 4, top: true);
            }
            var choiceMenu = Action("Choice Menu Button", choice, "Main Menu", Vector2.zero, 15, 12, 124, Secondary);
            var cancelChoice = Action("Cancel Element Button", choice, "Clear", Bottom, 0, 12, 96, Secondary);
            var confirmChoice = Action("Confirm Element Button", choice, "Unlock", BottomRight, -15, 12, 124, Primary);

            var resultOverlay = Modal(canvas, "Battle Result Overlay", out var resultSafe);
            var result = Dialogue("Battle Result", resultSafe, 360, 258, -14);
            Icon("Result Seal", result, Top, 0, -8, 50, VeilwardGlyph.Seal, Gilt);
            var resultTitle = DisplayLabel("Result Title", result, Top, 0, -57, 326, 43, "", 42, PaperText);
            var resultDetails = Label("Result Details", result, Top, 0, -101, 326, 58, "", 16, PaperText, TextAnchor.MiddleCenter);
            var resultSave = Label("Result Save Status", result, Top, 0, -160, 326, 21, "", 14, PaperMuted, TextAnchor.MiddleCenter);
            var resultMenu = Action("Result Menu Button", result, "Main Menu", Bottom, 0, 11, 181, Primary);
            ButtonDecoration(resultMenu, "Gate", 28, 15);

            var warningSafe = SafeRoot(canvas, "Save Warning Safe Area");
            var warning = Card("Save Warning", warningSafe, TopRight, -10, -78, 270, 72);
            var saveStatus = Label("Save Warning Text", warning, Left, 14, 0, 147, 61, "", 13, Coral);
            var retrySave = Action("Retry Save Button", warning, "Retry Save", Right, -8, 0, 96, Secondary, fontSize: 14);
            warning.gameObject.SetActive(false);

            var diagnostics = Card("Battle Diagnostics", safe, TopLeft, 10, -78, 300, 259);
            var diagnosticStats = Label("Stats", diagnostics, TopLeft, 12, -8, 276, 152, "", 14, Stone, TextAnchor.UpperLeft);
            var reaction = Label("Reaction Summary", diagnostics, TopLeft, 12, -164, 276, 44, "", 14, Gilt, TextAnchor.UpperLeft);
            var saveDiagnostics = Label("Save Status", diagnostics, TopLeft, 12, -212, 276, 39, "", 14, Muted, TextAnchor.UpperLeft);
            diagnostics.gameObject.SetActive(false);

            var pauseOverlay = Modal(canvas, "Pause Overlay", out var pauseSafe);
            var pauseCard = Dialogue("Pause Dialog", pauseSafe, 339, 243);
            DisplayLabel("Paused Label", pauseCard, Top, 0, -18, 270, 52, "Paused", 49, PaperText);
            var resume = Action("Resume Button", pauseCard, "Resume", Top, 0, -75, 231, Primary, fontSize: 21);
            ButtonIcon(resume, VeilwardGlyph.Flame, Gilt, 34, 20);
            var menu = Action("Main Menu Button", pauseCard, "Main Menu", Top, 0, -138, 231, Secondary, fontSize: 21);
            ButtonDecoration(menu, "Gate", 33, 20);
            var pauseSave = Label("Pause Save Status", pauseCard, Bottom, 0, 13, 302, 26, "", 13, PaperMuted, TextAnchor.MiddleCenter);

            ApplySafeRoots(canvas, safe, sellSafe, choiceSafe, resultSafe, warningSafe, pauseSafe);
            var playerHud = canvas.gameObject.AddComponent<BattlePlayerHud>();
            Bind(playerHud, ("heart", heart), ("gold", gold), ("wave", wave), ("phase", stage),
                ("result", resultOverlay.gameObject), ("resultTitle", resultTitle), ("resultDetails", resultDetails),
                ("resultSave", resultSave), ("saveWarning", warning.gameObject), ("saveStatus", saveStatus),
                ("pauseSave", pauseSave), ("diagnostics", diagnostics.gameObject), ("startWave", start), ("nextWave", next),
                ("retrySave", retrySave), ("resultMenu", resultMenu), ("choiceMenu", choiceMenu));

            return new HudReferences
            {
                PlayerHud = playerHud,
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

        private static Text Metric(Transform parent, string name, float x, float width, VeilwardGlyph glyph)
        {
            Icon(name + " Icon", parent, Left, x, 0, 33, glyph, Color.white);
            return Label(name + " Value", parent, Left, x + 36, 0, width - 31, 34, "", 22, Stone, bold: true);
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

        private static RectTransform Card(string name, Transform parent, Vector2 anchor, float x, float y,
            float w, float h, string face = "InkCard") =>
            ArtImage(name, parent, anchor, x, y, w, h, face, true).rectTransform;

        private static RectTransform Dialogue(string name, Transform parent, float w, float h, float y = 0)
        {
            var card = Card(name, parent, Center, 0, y, w, h, "PaperCard");
            Decoration("Blossom Branch", card, TopLeft, -17, 12, 68, 68, "Blossom")
                .rectTransform.localEulerAngles = new Vector3(0, 0, 15);
            return card;
        }

        private static Button Action(string name, Transform parent, string title, Vector2 anchor, float x, float y,
            float w, string face, float h = 60, int fontSize = 16)
        {
            var image = ArtImage(name, parent, anchor, x, y, w, h, face, true);
            var button = image.gameObject.AddComponent<VeilwardUiButton>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, .94f, .83f);
            colors.pressedColor = new Color(.75f, .71f, .67f);
            colors.selectedColor = new Color(1f, .86f, .68f);
            colors.disabledColor = face == Secondary ? new Color(.74f, .7f, .68f) : new Color(.48f, .52f, .58f);
            colors.fadeDuration = .12f;
            button.colors = colors;
            var textColor = face == Secondary ? PaperText : Stone;
            var text = fontSize >= 20
                ? DisplayLabel("Label", image.transform, Center, 0, 0, w, h, title, fontSize, textColor)
                : Label("Label", image.transform, Center, 0, 0, w, h, title, fontSize, textColor, TextAnchor.MiddleCenter, true);
            Stretch(text.rectTransform, new Vector2(9, 4), new Vector2(-9, -4));
            Bind(button, ("face", text.rectTransform));
            return button;
        }

        private static void ButtonDecoration(Button button, string artwork, float size, float inset)
        {
            Decoration("Symbol", button.transform, Left, inset, 0, size, size, artwork);
            var label = button.GetComponentInChildren<Text>();
            Stretch(label.rectTransform, new Vector2(size + inset + 4, 4), new Vector2(-9, -4));
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
                Stretch(label.rectTransform, new Vector2(size + inset + 4, 2), new Vector2(-8, -2));
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
            using var data = new SerializedObject(icon);
            var array = data.FindProperty("artwork");
            var kinds = (VeilwardGlyph[])Enum.GetValues(typeof(VeilwardGlyph));
            array.arraySize = kinds.Length;
            foreach (var glyph in kinds)
                array.GetArrayElementAtIndex((int)glyph).objectReferenceValue = SpriteFor(glyph.ToString());
            data.ApplyModifiedPropertiesWithoutUndo();
            return icon;
        }

        private static Sprite SpriteFor(string name)
        {
            _inkSprites ??= AssetDatabase.LoadAllAssetsAtPath(UiArtPath + "InkUi.png").OfType<Sprite>()
                .ToDictionary(sprite => sprite.name);
            return _inkSprites.TryGetValue(name, out var sprite)
                ? sprite : throw new InvalidOperationException("Missing UI artwork: " + name);
        }

        private static Image Decoration(string name, Transform parent, Vector2 anchor, float x, float y, float w, float h,
            string artwork) => ArtImage(name, parent, anchor, x, y, w, h, artwork, false);

        private static Image ArtImage(string name, Transform parent, Vector2 anchor, float x, float y, float w, float h,
            string artwork, bool hit)
        {
            var image = Image(name, parent, anchor, x, y, w, h, Color.white, hit);
            image.sprite = SpriteFor(artwork);
            if (image.sprite.border != Vector4.zero) image.type = UnityEngine.UI.Image.Type.Sliced;
            else image.preserveAspect = true;
            return image;
        }

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

        private static Text DisplayLabel(string name, Transform parent, Vector2 anchor, float x, float y, float w,
            float h, string label, int size, Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var text = Label(name, parent, anchor, x, y, w, h, label, size, color, alignment);
            text.font = AssetDatabase.LoadAssetAtPath<Font>(UiArtPath + "CormorantGaramond-BoldItalic.ttf")
                ?? throw new InvalidOperationException("Missing UI display font.");
            text.verticalOverflow = VerticalWrapMode.Overflow;
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
            using var data = new SerializedObject(component);
            foreach (var (field, value) in fields) data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
