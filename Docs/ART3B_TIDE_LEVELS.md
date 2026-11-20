# ART.3b — TIDE «Колокол прилива», #78

Основа: main `57cae61557ab71a79aa1d67a1d10bdad7cc972cc`, merge PR77; reviewed FLAME `95a6563` является предком. Одно семейство TIDE L1–L3. ART1/принятые FLAME и прежние evidence сохраняются. Требования issue78, включая право самостоятельно выбрать конструкцию, являются заданием на реализацию.

## Контракт до реализации

Оригинальный крупный бронзовый колокол подвешен в открытой нефритово-каменной раме над водяной чашей. L1 — один колокол и низкая чаша; L2 — крупные боковые водосборники и усиленная открытая рама; L3 — разомкнутый высокий венец/водяные рога над унаследованной конструкцией. Это решение исполнителя внутри направления #78. Различия по крупным формам при одинаковой Battle camera/scale/light, без zoom и подписей. Нет сплошной большой крыши. Palette ART1, водные cyan/indigo акценты через существующие UV palette cells.

Sources в `ArtSource/Blender/ART3/TIDE/`, FBX и prefabs в соответствующих `ART3/TIDE/`. Один editable mesh на уровень с семантически именованными vertex groups для деталей, meters/root identity/ground pivot/Unity +Y up,+Z forward; основание XY±.47m, ≤5000tri/≤2 material slots. Exporter ограничен тремя TIDE sources и прежним проверенным FBX preset. `.meta` создаёт Unity. Нет новых textures/packages/render features/lights/bloom; reuse Surface/Glow и существующего mask. Source/provenance, import/roundtrip geometry/UV/normals/materials/GUID/localIDs/refs обязательны.

Единственный существующий `FlameModelBinding` сохраняет class/file GUID и serialized `levels`/`fallbackRenderers` для старых authored prefabs. Добавляется только serialized `tideLevels`. Выбор по authoritative definitionId+level, cache по type+level; прежнее имя класса — compatibility boundary, не второй владелец. `TowerDisplay.ApplyVisual` вызывает этот единый binding. Unsupported type/level переключает в fallback. Старый model root синхронно inactive до deferred Destroy. Повторный Apply той же пары не создаёт objects/materials/passes. `FlameModelBuilder.Bind` сохраняет tide refs, Battle builder и отдельный Tide builder wire обе линейки без перезаписи FLAME models. Никакого framework/двух fallback owners. ShotSource — семантический локальный anchor, Domain attack coordinates и generic beam не меняются.

Place→Upgrade2→Upgrade3→file/menu Continue→Combat rollback→Sell→restart сохраняют TowerId/position/scale/collider/selection/footprint/range. На каждой башне одна активная модель, graybox не рисуется поверх FLAME/TIDE, palette без общего type tint. Preview/Inspect/catalog остаются рабочими. Цены/уровни/Wet из текущих configs. Domain, Infrastructure, баланс/волны/ability/boss/content600/5/schema4/saves не изменяются. GALE/STONE/STEAM/STORM/legacy/unknown paths сохраняют graybox.

## Regression scenarios

- ART3B-01: все три sources/export/import: applied transforms, ground0, footprint≤.94m, triangles≤5000, два shared palette slots; UV/normals/геометрия не пусты; три конструктивно отличных силуэта. Reexport всех трёх сохраняет Unity geometry/GUID/localIDs/material remaps/prefab-scene refs и protected ART1/FLAME bytes.
- ART3B-02: alternating FLAME1→TIDE1→TIDE2→FLAME2→TIDE3→FLAME3; cache type+level, один активный child, fallback off, palette un-tinted, ShotSource присутствует. .5/1 cell scale не меняет root/collider/ground.
- ART3B-03: unknown/legacy/remaining four types, levels0/4 и missing array entry возвращают fallback без stale roots. Старый authored FLAME-only binding продолжает работать. Повторные Flame/Tide/Battle builders сохраняют refs/GUID/localIDs и не перезаписывают accepted models.
- ART3B-04: actual saved production MainMenu/Battle public-command flow с earned бюджетом: TIDE Place/Upgrade2/3, reject duplicates/max, Continue twice без reward/byte drift, Combat rollback полного safe snapshot, Sell/restart cleanup. FLAME lifecycle regressions не ослабляются; прежний TIDE-graybox assertion заменяется новым production contract, остальные fallback assertions прежние.
- ART3B-05: реальные Play Mode singles L1/2/3, lineup и TIDE/FLAME comparison при идентичных camera/scale/light; synthetic front/side/behind/dense probes подписаны. HP/head/Wet/status/Inspect/build/range читаемы. Короткое Editor video Upgrade→advancing combat (положительный tick/frame delta)→Continue/rollback/Sell. Natural flow только public commands/earned бюджеты; synthetic probes явно отделены. Console/compile audit, generic beam до #47.
- ART3B-06: static38, полные конечные EditMode/PlayMode positive totals, diff/meta/GUID/reference/packages/protected paths; точные команды/settings/sourceSHA/artifact inventory+hashes; read-only clean-checkout verifier и независимое code/asset/visual/evidence review. Skipped/empty suites не PASS.

## Проверки и риски

Риски: FBX localID drift; fallback поверх поддерживаемой модели; cache только по level; palette tint; deferred roots; occlusion HP/Wet; мелкий L2; save proof с незаработанными ресурсами; serialization drift при builders. Проверки привязаны к ART3B-01…06. Общие suites сохраняют исторические goldens/traces/assertions, кроме явно заменённого TIDE fallback контракта.

**Device Deferred to prerelease QA #53/#54/#55** по решению владельца 20.11.2026. Телефон не подключается, native recording/matched benchmark и новые device binaries не создаются. Editor proof не является iPhone performance/readability PASS. Bootstrap→MainMenu→Battle export contract сохраняется. Final водные VFX #47, карта #45, враги #46, UI #48, остальные семейства и родитель #44 отдельно. PR Closes78 при полном Editor/source/Battle scope и приёмке; Refs44; без self-merge/release/TestFlight.
