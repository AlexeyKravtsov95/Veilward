# ART.3d — STONE «Осколочная печать», #82

Основа: main d2df69de476151823d0a6b727ec90b243cbf97ac, принятый PR81. Только STONE L1–L3, его sources/import и расширение единого FlameModelBinding. Конкретная конструкция выбрана исполнителем по явному разрешению #82.

## Контракт до реализации

L1 — тяжёлый расколотый нефритовый монолит на ступенчатом каменном основании, с крупной светлой печатью на лицевой плоскости и сдержанными бронзовыми скобами. L2 — две наклонные боковые резные плиты/контрфорсы, расширяющие средний силуэт. L3 — раздельные верхние зубцы и парные внешние опорные плиты: открытый расколотый каменный венец без крыши/навеса. Крупная масса отличает STONE от открытых FLAME/TIDE/GALE. Уровни имеют одну базу и разные конструкции, без uniform scale. Высота и боковые просветы ограничивают occlusion; production Shade HP anchor/status slots сохраняются. Существующий mask/cue используется без новых passes сверх принятого model contract.

Editable originals и author/export scripts ограничены ArtSource/Blender/ART3/STONE. FBX/prefabs в собственных ART3/STONE группах. Meters, applied transforms, ground pivot, Unity +Y up/+Z forward, footprint≤.94m, ≤5000tri/≤2slots, содержательные palette UV/normals, shared ART1 palette. Проверенный preset и Unity-generated meta; accepted ART1/FLAME/TIDE/GALE bytes защищены. Новых textures/materials/packages/lights/renderfeatures нет.

В прежний FlameModelBinding добавляется optional serialized stoneLevels; levels/tideLevels/galeLevels/fallbackRenderers, class/meta GUID сохраняются. Cache authoritative type+level, один model/fallback owner, synchronous inactive до deferred Destroy. Repeated Apply не создаёт model/material/pass. Отдельный StoneModelBuilder wire четыре семейства, не пересоздавая accepted assets. Legacy/unknown/STEAM/STORM/missing-level сохраняют fallback. ShotSource — future presentation anchor; Domain coordinates/beam прежние.

Законный production flow: New Game/FLAME → семь волн существующей F/T армии; после3 открыть TIDE, после6 открыть STONE public ChooseElement. Не выдавать GALE за STONE заменой DTO. Earned budget и public Sell оплачивают STONE90+135+180; затем Place→Upgrade2→3→repeated file/menu Continue→Combat8 с реальной «Трещиной»→полный rollback→Sell/restart. Первые волны используют явно подписанный test clock. Видео — byte-identical replay реально заработанного L1 checkpoint с origin/restore/readback, затем real-time UI Upgrade/Combat/Inspect/Continue/Sell.

Domain/Infrastructure/configs/баланс/цены/волны/ability/boss600/armor5/content/schema4/saves неизменны. Generic beam до #47. ArtReference delay не переносится в production.

## Regression scenarios

- ART3D-01: три оригинальных source/mesh; budgets/slots/meters/identity/ground/axes/UV/normals и крупные конструктивные отличия. Reexport3/3 с Unity before/after/canonical geometry+UV+normals/materialremaps/GUID/localIDs/prefab-scene refs, protected baseline bytes.
- ART3D-02: все четыре семейства при одинаковом L1/2/3, cache/no tint/no graybox/no duplicates/deferred cleanup, root/collider/selection/range/scale .5/1 неизменны.
- ART3D-03: authored refs до builders; repeated Build/Bind сохраняют все четыре семейства и одного owner; legacy serialized shape, default/unknown/STEAM/STORM, levels0/4 и missing refs возвращают fallback. Старые F/T/G tests сохраняются, заменяются только STONE-graybox assertions.
- ART3D-04: earned STONE unlock/budget/public commands, duplicate/MAX/repeated Sell guards, Continue дважды на каждом уровне без byte/reward drift, full Combat rollback включая ability/progression/IDs/cooldowns, Sell/restart без roots. Никаких live Gold/HP/DTO edits.
- ART3D-05: actual Play Mode singles/lineup/четыре семейства, front/side/behind каждого уровня и dense12 с HP/head/Crack/Inspect/build/range. Assigned presentation positions/HP и copied actual status snapshots помечены synthetic. Continuous Editor video с advancing ticks/frames/timestamps/PTS, exact-stage PNG picks разных frames/hashes; L3 до Combat, не rollback. Sparse capture/encoder overhead не benchmark.
- ART3D-06: static/diff/compile/Console, полные конечные Edit/Play suites positive totals, testedSourceSHA/sourceinventory/tracked hashes/read-only clean-checkout verifier и negative foreignsource/artifact controls только в task-owned checkout; независимое code/asset/visual/evidence review, PR Closes82/Refs44 после полного scope и приёмки, без self-merge/release.

Риски: нечитаемый L2, большой глухой объём перед HP, localID drift, type-less cache, stale deferred roots, palette tint, недостаточный законный бюджет, неточная video-stage provenance. Каждый покрывается tests/evidence выше.

**Device Deferred to prerelease QA #53/#54/#55.** iPhone/native binaries/iOS export не нужны; Editor не device PASS. Parent44 остаётся open: STEAM/STORM отдельно.

## Коррекция после первого реального Battle кадра

Первая версия source ошибочно направляла лицевую нормаль поперёк Battle camera: FBX preset в фактическом Unity readback сохраняет BlenderY как UnityZ, и печать была видна ребром. Source projection исправлена: горизонталь u=(X−Y)/√2, глубина d=(X+Y)/√2; normals пересчитаны в Blender, все3 source/FBX обновлены. Regression проверяет центр glowing seal vertices по диагонали +X/+Z и её поперечный extent. Initial edge-on PNG сохранены attempt-1, не считаются final visual PASS. Gameplay/root/scale/camera не менялись.
