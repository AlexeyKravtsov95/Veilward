# ART.3e — STEAM «Туманная лисица», #84

База main62d4dbd (PR83). Scope: оригинальные STEAM L1–L3, source/import/prefabs и минимальное расширение прежнего FlameModelBinding до пяти семейств. Конструкция выбрана исполнителем по #84; STORM отдельно.

## Контракт до реализации

Лисица — сидящая нефритовая скульптура на компактном каменно-бронзовом основании. Голова с двумя острыми ушами, выступающей светлой мордой и тёмными глазами направлена к исходной Battle camera. Тело и лапы имеют собственный объём. L1 — один объёмный согнутый хвост; L2 — два раздельных поднятых хвоста; L3 — три раздельных хвоста и открытые светлые паровые завитки, без глухого навеса. Отличия крупной конструкции при одинаковых camera/scale/light, без uniform scale/перекраски. Хвосты остаются внутри footprint и не закрывают широким веером соседние HP. Shared ART1 palette Surface/Glow и принятый stencil mask; никаких новых materials/textures/packages/lights/render features.

Только ArtSource/Blender/ART3/STEAM и собственные FBX/prefabs ART3/STEAM. Meters/applied identity/ground pivot, Unity+Yup/+Zforward, footprint≤.94m, ≤5000tri/≤2slots. Editable mesh с семантическими vertex groups, содержательные palette UV/normals. Проверенный FBX preset, Unity-generated meta/GUID. Accepted ART1/F/T/G/STONE и исторические evidence bytes сохраняются.

В FlameModelBinding добавляется optional steamLevels; class/meta GUID и прежние serialized fields неизменны. Cache authoritative type+level, один fallback owner, synchronous inactive до deferred Destroy. Пять authored семейств без graybox/type tint; STORM/legacy/unknown/missing-level сохраняют fallback. Повторные Apply/Bind/Build без дублей/material instances/pass growth. Root/TowerId/collider/selection/footprint/range прежние; ShotSource только future presentation anchor.

STEAM требует настоящих FLAME+TIDE unlocks; StatusEffect=null, intrinsic Steam из действующего TestWave. Цены110+165+220=495. Public commands зарабатывают бюджет в production миссии с законными продажами; готовый checkpoint/Gold/HP/type/unlocks/DTO не подменяются. Place→Upgrade2→3→дважды file/menu Continue на каждом уровне→advancing Combat с настоящими Attack/Intrinsic Steam events и положительным primary/secondary HP loss→полный safe rollback→Sell/restart. Byte-identical replay earned checkpoint допустим с origin/restore/readback и явной подписью; это не fresh run. Нужна естественная вторичная цель в radius, а не новый Burn/Wet ради кадра.

Domain/Infrastructure/configs/баланс/волны/ability/boss600/armor5/content/schema4/saves неизменны. Generic beam допустим; финальные VFX #47. ArtReference delay в production не переносится.

## Regression scenarios

- ART3E-01: три editable sources и Unity imports: бюджеты/units/axes/ground/pivot/UV/normals; объёмные голова/уши/морда/тело/хвосты и крупные отличия L1–3. Reexport3/3 с Unity before/after/canonical geometry/UV/normals/remaps/GUID/localIDs/prefab-scene refs, protected source bytes.
- ART3E-02: пять семейств и same-level switches, type+level cache/no tint/no graybox/no duplicates/deferred cleanup, repeated Apply; root/collider/scale.5/1/ShotSource.
- ART3E-03: authored refs до builder, repeated Build/Bind сохраняют пять семейств/одного owner/serialized compatibility. Unknown/default/legacy/STORM/levels0/4/missingrefs возвращают fallback. Старые четыре suites сохраняются, заменяются только superseded STEAM-graybox assertions.
- ART3E-04: genuine locked rejection до TIDE, public unlock/earned495/Place/Upgrade2/3, duplicate/MAX/repeated Sell guards. File/menu Continue дважды на каждом уровне без reward/byte drift; полный Combat rollback с IDs/cooldowns/ability/progression/Grid; Sell/restart cleanup. Genuine intrinsic Steam/AoE events с positive HP loss secondary, без basic status payload.
- ART3E-05: actual Play Mode singles/lineup/пять семейств при прежних camera/scale/light, front/side/behind каждого уровня и dense12 с production HP/head/status slots; Inspect/build/range читаемы. Assigned presentation positions/HP/copied status snapshots подписаны SYNTHETIC. Continuous Editor Upgrade→advancing Combat/HP/intrinsic Steam/Inspect→Continue/rollback→Sell; timestamps/PTS/exact-stage PNG picks из разных source frames/hashes, L3 до Combat. Capture overhead/sparse sampling не performance benchmark.
- ART3E-06: static/full final Edit/full final Play/diff/compile/Console positive totals. TestedSourceSHA/inventory/tracked hashes/read-only актуальный verifier в чистом текущем checkout; без новых worktrees/clones/copies. Неизменная verifier logic может ссылаться на исторические tamper controls с явной пометкой, что повторно не запускались. Независимое code/asset/visual/evidence review, PR Closes84 после полного scope/приёмки, Refs44, без self-merge/release.

Риски: лисица не читается с камеры; хвост перекрывает HP; type-less cache; stale root; localID drift; недостаточный бюджет495; неверный Steam status proof; отсутствие secondary в radius; недостоверные video picks. Каждый риск покрывается сценарием выше.

**Device Deferred to prerelease QA #53/#54/#55.** Телефон не подключать; native binaries/export/recordings/benchmarks не нужны. Editor не device PASS. Parent44 остаётся открыт, STORM/карта45/враги46/VFX47/UI48/G2/release отдельно.

## Проверяемый earned путь после ограниченного поиска

Выбранный public-command script: FLAME(-3,-1) и FLAME(2,1) с Preparation1; после3 открыть TIDE; STEAM(1,0) с Preparation4; после6 открыть GALE; STEAM L2 с Preparation7. В Preparation8 обе FLAME продаются публично (49+49), STEAM улучшается до L3 за220. К этому моменту50kills/14HP, валовые540gold, потери42 на продаже: полный доступный бюджет498, L3 investment495 и остаток3. Цены/волны/здоровье не менялись. Casts и намеренное сохранение ability в остальных волнах закреплены в `review/ART.3e/inputs/earned.commands.json`.

Fresh New Game/file/menu regression сохраняет L1 в Preparation4 (tick1183/Gold20), L2 в Preparation7 (tick2668/Gold50) и L3 в Preparation8 (tick3168/Gold3); это разные естественные стадии того же run, а не DTO rewrite. Видео начинает byte-identical replay earned L1 checkpoint. Волны4–7 используют явно подписанный test-only clock30Hz, public Step/Cast и штатные consumers/lifetimes; финальный Combat8 — настоящий `BattleSceneController.Update`. В видео нет splice/изменения PTS speed, capture overhead не benchmark.
