# UI.REWORK.1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Work sequentially in the existing checkout; do not create worktrees or project copies.

**Goal:** Перенести принятый рисованный интерфейс в MainMenu и все существующие Battle UI состояния.
**Architecture:** Сохраняем uGUI, scene builders и runtime owners. Меняем assets/layout и источник визуала иконок, оставляя commands, modal priority и saves прежними. Пакеты #104/#105 отдельно.
**Tech Stack:** Unity6000.3.28f1, existing URP/uGUI/InputSystem, C#, existing art import workflow.
**Spec:** `Docs/UI_REWORK_1_DESIGN.md`, #102/#103, `ArtSource/References/UI_REWORK_1/approved-ui-concept.png`.

## Global Constraints

- Все ограничения design обязательны; минимум44×44 logical pt, Canvas740×400/Expand, EN runtime copy.
- Domain/configs/economy/волны/IDs/save schema/camera/map/персонажи/VFX timing и Packages не менять.
- Визуальное принятие концепта не означает merge101 или приёмку новой реализации.
- Подготовленная branch от PR101 head `ff1e129562879eb31a66820d52d428112d6e3d15`. Пока101 не подтверждён владельцем как влитый, UI PR base — `codex/art-rework0-ink-lanterns`; после подтверждения retarget main без merge audit.
- No worktree/clone/project copies, no destructive cleanup/autostash. Один владелец записи/Unity runs. Device Deferred53/54/55; no self-merge/release.

## File structure and contracts

**Modify:** `Assets/_Game/Scripts/Editor/BattlePlayerUiBuilder.cs` (partial BattleSceneBuilder, содержит и MainMenu), `BattleSceneBuilder.cs` только необходимые binding/build changes; `Assets/_Game/Scenes/MainMenu.unity`, `Battle.unity`; `Assets/_Game/Scripts/Presentation/VeilwardUiGlyph.cs`, `VeilwardUiButton.cs` при необходимости визуальных transitions.
**Preserve owners:** BattlePlayerHud/BattlePlacementController/AbilityController/BossHudController/BattleHudSafeArea/MainMenuController и их публичные команды; runtime edits только если нужны layout/copy и без переноса доменной логики.
**Create assets:** `ArtSource/UI/InkAndLanterns/` masters/provenance, `Assets/_Game/Art/UI/InkAndLanterns/` imports/atlas/materials/fonts по нужде, с .meta.
**Tests:** расширить `Assets/_Game/Tests/EditMode/ProductionUiLayoutTests.cs`, `Assets/_Game/Tests/PlayMode/ProductionUiPlayModeTests.cs`; сохранённые MainMenuControllerTests и другие flow suites.
**Evidence:** `Docs/review/UI.REWORK.1/` spec/readme/reports/images/video/inventory.

## Review Focus

- Декоративный слой/границы панели перехватывают настоящий тап: Task2 проверяет EventSystem raycast/button и world shielding, а не только onClick.
- Маленький визуальный pause/seal значок имеет маленькую touch area или targets пересекаются: Task4 проверяет реальные screen rects/pt/non-overlap.
- Locks/unlocks обновляются на существующей карточке после смены стихии: Task1/3 проверяют текущий Glyph consumer и missing-elements/price states.
- Ошибка save во время placement/choice/terminal превращается в недоступный Retry: Task3 проверяет исходный modal priority и recovery без duplicate actions.
- Reopen/Pause/Continue/Register listeners приводит к двойной покупке/касту или stale control: Task3 повторяет переходы через настоящие UI events и snapshots.

### Task1: Authored assets and compatible glyph rendering

**Files:** art folders, VeilwardUiGlyph.cs, BattlePlayerUiBuilder.cs, ProductionUiLayoutTests.cs.
**Interfaces:** сохраняется `VeilwardUiGlyph.Glyph { get; set; }` и enum; `BattlePlacementController` продолжает находить этот компонент и назначать school/Lock. Сохраняется `VeilwardUiGlyph : MaskableGraphic`. Для production можно добавить serialized `Sprite[] artwork` indexed by existing enum ordinal, без перестановки enum; builder обязан связать полный набор. Ненастроенные исторические fixtures могут сохранять прежний fallback; production не должен молча попадать в него.

- [ ] Прочитать PNG/design, инвентаризировать текущие bindings и artwork sizes. Изготовить отдельные master/background/button/panel/icon assets, provenance/license/prompts. Никаких слов/цифр, впечтанных в control faces.
- [ ] Добавить failing EditMode case `UiRework101_AllProductionGlyphsHaveDistinctAuthoredSprites`: school/Lock/Seal/Heart/Gold/Wave/Pause mapped, school sprites distinct, source reference non-null, production scenes не используют missing-art fallback.
- [ ] Запустить case имеющимся Unity test runner; записать конкретный ожидаемый failure о missing authored mapping.
- [ ] Внедрить sprite draw/UV/texture path внутри текущего graphic. Если добавляется `public override Texture mainTexture`, он возвращает актуальный sprite texture; смена Glyph инвалидирует vertices/material и сохраняет MaskableGraphic clipping. Иконки сохраняют пропорции и original colors, runtime cycle не создаёт новые materials/textures. Builder связывает artwork через serialized refs.
- [ ] Повторить case, school→Lock→school на одном instance, clipping/alpha/import checks; сохранить PASS/limitations.
- [ ] Commit одного законченного asset/glyph deliverable.

### Task2: MainMenu, common surfaces, compact HUD and Pause

**Files:** BattlePlayerUiBuilder.cs/helpers, MainMenu/Battle scenes, ProductionUiPlayModeTests.cs.
**Interfaces:** существующие `BuildMainMenu()`, `CreateHud()`, `Card(...)`, `Action(...)`, `Icon(...)` и HudReferences сохраняя типы/связи. Controllers не получают screenshot-specific state.

- [ ] Добавить failing `UiRework102_DecoratedControlsReceivePointerAndModalBlocksWorld`: через EventSystem.RaycastAll и реальный pointer/event dispatch проверить Continue/NewGame/Pause/Resume и клик по украшенной границе; только правильное действие и никакой world selection под overlay. Позиции для raycast брать из screen corners реальных rects.
- [ ] Запустить case и записать failure только нового decor/modal scenario; не ломать исходные callbacks ради искусственного RED.
- [ ] Изготовить/привязать slices/buttons/menu backdrop/wordmark, заменить большие прямоугольники на принятую систему. Текст остаётся отдельным Text. Все непредназначенные для ввода Graphics получают raycastTarget=false, modal background сохраняет защиту.
- [ ] Обновить compact metrics/phase/start-next/pause/boss strip/ability layouts. Сохранить boss threat/HP/armor/cooldown объяснения, действующие ready/disabled состояния и место для будущих двух badges #104; не удалять status logic и не имитировать будущие исправления.
- [ ] Пересобрать сцены существующим builder, проверить сохранённые refs/повторный build/import и новый case + исходные menu/pause tests.
- [ ] Commit.

### Task3: Restyle every existing card and preserve transactional flows

**Files:** BattlePlayerUiBuilder.cs, связанные presentation файлы только для layout/copy, ProductionUiPlayModeTests.cs.
**Interfaces:** MainMenuController NewGame/Continue/Retry; existing placement/upgrade/sell/cancel, ability Preview/Confirm/Cancel, element choice, inspector, checkpoints. Команды и modal guards unchanged.

- [ ] Расширить регрессии: `UiRework103_CardReopenDoesNotDuplicateActions` (двойное reopen, одна реальная покупка/Upgrade/Sell/Confirm и ожидаемые Gold/tower count); `UiRework104_LockedSchoolAndPricesRefreshInDecoratedCatalog` (school→Lock→school по действующим unlocks, no purchase while locked/poor); `UiRework105_SaveRetryRemainsReachableAcrossModals` (failed save в preparation/choice/result, исходный owner priority, Retry восстанавливает save без повторных наград).
- [ ] Запустить затронутые cases; сохранить реальные failure или явно признать existing PASS, не ослаблять ожидания.
- [ ] Привести build catalog/cards, tower/Upgrade/Sell/confirm, enemy inspector, starting/after3/after6 choices, result и save/error/retry к общим surface/icon/type/state rules.
- [ ] Обновить responsive docking/текстовые области для всех states, не прятать ошибки или причины блокировки. Более длинный существующий текст переносится/адаптируется, а не обрезается до потери смысла.
- [ ] Прогнать новые cases и full affected menu/placement/ability/unlock/save/Continue lifecycle suites.
- [ ] Commit.

### Task4: Layout, safe area and real pointer coverage

**Files:** ProductionUiLayoutTests.cs, ProductionUiPlayModeTests.cs; layout code where failures reveal actual defects.
**Interfaces:** `BattleHudSafeArea.ApplySafeArea(Rect, Vector2, float)` и текущий Ui104 suite сохраняются. Screen rects проверяются после layout rebuild.

- [ ] Расширить layout cases на существующих narrow1136×640/DPR2, modern2556×1179/DPR3/insets132x63 и iPad2048×1536/DPR2: все реально открытые состояния с hit targets ≥44pt, safe bounds, отсутствие нежелательного overlap соседних controls и критического текста.
- [ ] Реально открыть hidden cards/overlays перед проверкой, не ограничиваться sizeDelta неактивных элементов; проверить что visible gameplay path/лица/HP не прячутся стабильным HUD.
- [ ] Поправить только затронутые anchors/layouts, сохраняя камеру/сцену мира.
- [ ] Прогнать layout/pointer cases и снять full GameView PNG этих states/aspect ratios; сохранить измерения logical pt и явные ограничения Editor vs device.
- [ ] Commit.

### Task5: Evidence and focused PR

**Files:** Docs/review/UI.REWORK.1 и обновлённая документация affected UI contracts.
**Interfaces:** current `./Scripts/run-static-checks.sh` и project Unity CLI runner из AGENTS/current docs; не угадывать totals/CLI schema.

- [ ] Выполнить static, compile, обязательные полные EditMode/PlayMode suites, `git diff --check`; сохранить unique complete positive reports и failed/skipped/inconclusive.
- [ ] Проверить source/import/GUID/refs и повторяемость atlas/sprites/scene builder; source hashes actual tested inputs, materials/textures budget before/after, новые allocations при обычном UI cycle. При платформенной разнице PNG encoding отдельно integrity bytes и semantic regeneration, без скрытого допуска.
- [ ] Снять настоящие MainMenu→Battle→Build→Upgrade/Sell→Pause→Resume→result/Continue и errors/Retry. Earned v2 и stress отдельно. В UI-only captures старые status labels/build circles видны честно, не удалены post-process.
- [ ] Проверить видео decoding/frame inventory, кадры, provenance; привести tested SHA и границы VERIFIED/NOT VERIFIED. Не называть PNG/video UI art/device acceptance автоматически.
- [ ] Commit и открыть Draft PR: Refs #103/#102/#48, base согласно Global Constraints. Родители не закрывать; review/owner art/merge отдельно, iPhone prerelease53/54/55.

## Handoff

Владелец уже выбрал визуальный target и способ исполнения внешним GPT Sol6.1/High. Начинай с Task1 в подготовленной ветке; повторного согласования косметических решений не требуется. При настоящем недостающем dependency/конфликте чужих изменений сообщить конкретный blocker, не создавать второй checkout.
