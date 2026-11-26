# UI.REWORK.1 — дизайн принятого интерфейса

Родитель #102, первый implementation пакет #103. **26.11.2026 владелец ответил «да, неплохо» на последнюю четырёхэкранную версию эскиза.** Это принятие визуального направления для реализации, не подтверждение merge101, готовности runtime, итогового ART/device QA или релиза.

Референс: `ArtSource/References/UI_REWORK_1/approved-ui-concept.png`, исходный imagegen output `exec-b289b54b-3bd6-4d3e-98a4-856232405016.png`, Git blob `f841b15ab8515971a102138265f7edcbc6df24d5`. Предыдущую версию с врагами в каталоге и стройкой во время боя не использовать.

## Цель

MainMenu, HUD и все существующие карточки образуют одну рисованную систему B «Чернила и фонари». Компактные тушевые поверхности индиго/нефрита, кремовая бумага, тёплые кораллово-золотые акценты, самостоятельные выразительные иконки. Убирается вид больших технических прямоугольников. Декор сдержанный, главные действия и числа читаемы. Title/крупные заголовки могут быть декоративными; основной текст и числа сохраняют ясную типографику.

## Рассмотренный подход

Продолжение перекраски прежних примитивных панелей не устраняет прямое замечание владельца об иконках/меню. Переписывание UI framework/Domain не требуется. Выбран **новый отдельный набор рисованных assets на существующем uGUI и владельцах состояний**, по принятому целому экрану. Это сохраняет проверенные commands/modal/save flow и позволяет оценить художественный перенос отдельно.

## Первый пакет #103

- Общие icons/button/panel assets, MainMenu с рисованной фоновой иллюстрацией и существующими New Game/Continue/Retry.
- Компактные Heart/Gold/Wave, Pause, честные Start/Next Wave/phase, boss strip и состояние способности.
- Build catalog/preview card, selected tower/Upgrade/Sell/confirm, enemy inspector, element choice/start resonance, Pause, Victory/Defeat, save errors/Retry.
- Все текущие disabled/locked/missing-elements/insufficient-gold/Continue-unavailable состояния доступны и читаемы. Фиктивных Settings/shop/campaign/новых действий из картинки не добавлять.
- Карта/камера и персонажи из101 сохраняются. Картинка — эстетическая опора, не точные координаты, topology или authoring-ready sprite sheet.

### Visual review 26.11.2026

Общие поверхности используют отдельные спокойные силуэты бумаги и мазка с тонким краем. В HUD компактная полоса Стража показывает имя, HP, броню и урон Сердцу. Медальон Печати показывает готовность и отсчёт, его зона тапа шире самого рисунка. Лента каталога содержит шесть школ, параметры и доступность выбранной школы, Build и Cancel. Когда лента снизу закрыла бы выбранную клетку, она закрепляется под верхним HUD. Предупреждение о записи показывается у противоположного вертикального края. [Сравнение полных кадров GameView](review/UI.REWORK.1/polish/README.md)

## Следующие пакеты

#104: компактные status/reaction badges вместо world-space подписей, босс — у HP strip. #105: контекстная разметка строительных клеток и соразмерные опоры. В #103 для них остаются место и общие tokens, но старые статусы/круги не скрываются только ради красивого capture и не объявляются уже исправленными.

## Неподвижные контракты

Unity6000.3.28f1, существующие Packages и uGUI. Новых packages/версию движка не менять. Domain/economy/waves/ability damage/IDs/unlocks/save schema/checkpoints/mapping/camera без изменений.

Существующие MainMenuController, BattlePlayerHud, BattlePlacementController, AbilityController, BossHudController, BattlePauseController, EnemyInspectorController, ElementChoiceController остаются владельцами действий/данных. Serialized refs и имена используемых тестами controls сохранять либо мигрировать совместимо с осмысленной проверкой, не удаляя действия. `VeilwardUiGlyph.Glyph` продолжает обновлять реальные шесть school icons/Lock через текущий placement consumer.

CanvasScaler reference740×400/Expand и safe-area mapping сохраняются. Touch targets минимум44×44 logical pt; декоративный рисунок может быть меньше hit target. EN copy/цены/HP/cooldown/ошибки — настоящие динамические Text, не впечатываются в фон. Вся карточка сохраняет видимые блокировки/подтверждения. Decorative Graphics не перехватывают input.

## Авторинг

Отдельные sprites/9-slice panels/background/wordmark; никакого цельного screenshot c невидимыми кнопками. Masters, точные prompts, derivation, font/image source and license/provenance сохраняются. Не заявлять ручную дорисовку, если её не было. Иконки читаются формой, не только цветом. Menu background и runtime atlas разделить; raw byte budget/textures/materials/Canvas allocations измерить и описать, не выдавать за native performance.

## Доказательство

GameView: обычный earned v2 Battle, все UI states, MainMenu/Continue и реальные ошибки/Retry; dense scene отдельно как fixture. Baseline1600×900; narrow1136×640/DPR2; modern2556×1179/DPR3 with safe area; iPad2048×1536/DPR2. Полные экраны + короткое видео переходов и нажатий, без ретуши. Реальный EventSystem/raycast path и modal shielding проверяются наряду с callbacks. Исходники/evidence привязаны к проверенному SHA, все ограничения явно названы.

Обязательные static/compile/full applicable suites/meta/GUID/ref/import checks. Тесты и owner art acceptance отдельно. Device Deferred53/54/55. Существующий checkout, без worktree/clone/project copies, preserve foreign changes/no reset-clean-autostash. Не мержить и не выпускать самостоятельно.
