# ART.3c — GALE «Ветряной талисман», #80

Основа: main b2d31095238e8ce5e276ea1ba0a7741b8afee82f, принятый PR79/TIDE R2. Scope: только GALE L1–L3/source/import и существующий single-owner model binding; остальные семейства, Domain/Infrastructure/configs/schema4/content600/armor5 не меняются. Дизайн ниже выбран исполнителем по явному разрешению #80.

## Контракт до реализации

L1 — подвешенный ромбический нефритовый талисман в лёгкой разомкнутой бронзовой раме на каменном основании. L2 — две крупные боковые ветровые дуги вокруг унаследованного талисмана. L3 — разомкнутая верхняя спираль и вторая поперечная дуга. Конструктивные различия при исходной Battle camera/scale/light; без большой глухой крыши, перекраски другого семейства и uniform scale. Общая ART1 palette, нефрит/бронза/тёплый камень с светлыми ветровыми акцентами, без новых textures/materials/lights/render features/packages.

Редактируемые оригинальные sources и ограниченные author/export scripts в ArtSource/Blender/ART3/GALE; FBX/prefabs в собственных ART3/GALE группах. Meters/applied identity/ground pivot/+Y up/+Z forward, footprint≤.94m, ≤5000tri/2slots. Проверенный export preset, содержательные palette UV/normals, Unity-generated meta/GUID. Принятые ART1/FLAME/TIDE и исторические evidence защищены.

FlameModelBinding сохраняет class/meta GUID и serialized levels/tideLevels/fallbackRenderers; добавляется galeLevels. Cache authoritative definitionId+level, один владелец fallback. Поддерживаемые три семейства без graybox/type tint; STONE/STEAM/STORM/legacy/unknown/missing-level сохраняют fallback. Старый child inactive синхронно до deferred Destroy; repeated Apply не создаёт моделей/материалов/passes. ShotSource — локальный future anchor, gameplay coordinates прежние. Builder воспроизводит refs всех трёх семейств без перестройки accepted assets.

Place→earned Upgrade2→Upgrade3→file/menu Continue→полный Combat rollback→Sell/restart сохраняют TowerId/root transform/collider/selection/footprint/range и единственную активную модель. Catalog/preview/Inspect рабочие. Сохраняется принятый Shade HP anchor, HP/Gust/status читаемость проверяется настоящими Play Mode кадрами. ArtReference delay не переносится в production; generic beam до #47.

## Regression scenarios

- ART3C-01: три оригинальные геометрии, triangles/slots/ground/pivot/axes/UV/normals/import remaps. Повторный export всех трёх: Unity before/after/canonical geometry+UV+normals/GUID/localIDs/material/prefab/scene refs совпадают; protected sources неизменны.
- ART3C-02: FLAME/TIDE/GALE переключения на одинаковых L1/2/3, cache/no tint/no graybox/no duplicates, repeated Apply и deferred cleanup, .5/1 cell scales. Старые fields/legacy/missingrefs/unknown/levels0/4/fallback остаются валидны.
- ART3C-03: authored serialized refs до builder; повторные Gale/Battle/Flame/Tide Bind сохраняют refs и единственного owner, builder не перезаписывает принятые assets. Прежние FLAME/TIDE assertions не ослабляются; меняется только заменённый GALE-graybox contract.
- ART3C-04: saved production MainMenu/Battle, public commands и earned budgets: Place/Upgrade2/3/rejects/file/menu Continue twice/full Combat rollback/Sell/restart. Никаких live Gold/HP/DTO edits; synthetic test clock явно подписан.
- ART3C-05: реальные Play Mode singles/lineup/сравнение трёх семейств с одинаковой camera/scale/light; synthetic front/side/behind/dense probes с head/HP/Gust/Inspect/build/range. Непрерывная Editor запись Upgrade→advancing combat→Continue/rollback→Sell с frame/tick/timestamps/PTS, exact-stage PNG picks и разными hashes L3/rollback. Byte-identical earned replay раскрывает origin/restore/readback; capture overhead/sparse timing не benchmark.
- ART3C-06: static/diff/compile/Console, конечные полные Edit/Play suites с положительными totals; sourceSHA/inventory/tracked artifact hashes/read-only актуальный verifier в clean task-owned checkout и negative foreign source/artifact controls. Независимое code/asset/visual/evidence review, без self-merge/release.

Риски: малое отличие L2, FBX localID drift, cache без типа, stale deferred roots, tint palette, перекрытие HP/Gust, недостоверный earned replay или video picks. Tests и evidence выше проверяют каждый риск.

**Device Deferred to prerelease QA #53/#54/#55.** Телефон/новые native binaries/iOS export не нужны. Editor evidence не означает device PASS. Parent #44 и STONE/STEAM/STORM остаются открыты; финальный PR Closes #80 только при выполнении Editor/source/Battle scope и приёмке, Refs #44.
