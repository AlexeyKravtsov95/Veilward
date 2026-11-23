# ART.3f — STORM «Грозовой карп», #86

База main d7895fe (merge PR85). Scope: три оригинальные STORM модели, source/import/prefabs, optional stormLevels в прежнем FlameModelBinding, шесть семейств, regression tests и Editor evidence. Конструкция выбрана исполнителем в рамках явной делегации #86.

## Контракт до реализации

Нефритовый небесный карп на каменно-бронзовом основании: объёмные голова/губы/глаза/усики, изогнутое тело, раздвоенный хвост и раздельные плавники. L1 — низкий восходящий изгиб; L2 — выраженная S-поза и крупные раскрытые парные плавники; L3 — поднятый разворот тела и разомкнутые светлые грозовые дуги с просветами. Одинаковые scale/camera/light, без широкой глухой стенки. Shared ART1 Surface/Glow/mask, без новых materials/textures/packages/lights/render features.

Editable sources только ArtSource/Blender/ART3/STORM; exporter пишет только собственные три FBX. Meters/identity/ground pivot/Unity+Yup,+Zforward; footprint≤.94m, ≤5000tri/≤2slots, palette UV/normals/semantic groups. Unity-generated meta/GUID; принятые ART1 и пять семейств/исторические evidence bytes защищены. Реальный reexport и Unity before/after сравнивают geometry/UV/normals/remaps/GUID/localIDs/refs.

FlameModelBinding сохраняет class/meta GUID/levels/fallbackRenderers и пять прежних массивов, добавляет optional stormLevels и одну ветку type+level selector. Один cache/fallback owner; старый child inactive до Destroy. Authored модели без TypeTint/graybox, legacy/unknown/default/missing refs/unsupported levels — fallback. Builder сохраняет все шесть массивов. Root/ID/collider/footprint/range/selection и presentation ShotSource сохраняются.

Gameplay/Domain/config/balance/content/schema4/save contracts неизменны. STORM требует TIDE+GALE, StatusEffect=null; intrinsic работает без Wet/Gust. Цены110/165/220, investment495; intrinsic75/90/100%, range2/2.25/2.5, targetLimit2 дополнительных целей (primary исключена). Сохраняются cooldown/порядок выбора/отсутствие целей, без рекурсивных цепей. Финальный lightningVFX #47 отдельно.

## Regression scenarios

- ART3F-01: три editable mesh, характерные голова/тело/хвост/плавники/позы; реальные budgets/units/pivot/axes/UV/normals/materials. Reexport3/3 с Unity geometry/remaps/GUID/localIDs/refs readback, accepted baseline неизменен.
- ART3F-02: шесть семейств same-level/type+level switches, repeated Apply/Bind/Build без дублей/material instances/stale active children/tint, deferred cleanup, root/collider/.5/1 scales/ShotSource. Authored refs проверяются до Build.
- ART3F-03: старый serialized shape без stormLevels, null/partial/missing refs, default/legacy/unknown/levels0/4 — fallback. Прежние suites не ослабляются; заменяется лишь актуальный STORM-always-fallback контракт.
- ART3F-04: production NewGame/public unlocks/earned495/Place→L2→L3, repeated/invalid commands; два file/menu Continue на уровне, полный checkpoint/snapshot rollback включая живой Grid/progression/IDs/cooldowns/ability/events; Sell/restart. Без setters/DTO edits для earned flow.
- ART3F-05: intrinsic ReactionEvent связан с реальным AttackEvent по TowerId/primary/tick и sequence, DamageResolvedEvent того же события показывает HP loss>0 на дополнительной цели. Отдельный controlled fixture: обе пригодные дополнительные цели ровно один раз, primary не повторяется; no-target/cooldown negative cases. Earned positive secondary и synthetic fixture разделены, Wet+Gust не выдается за intrinsic.
- ART3F-06: настоящие PlayMode PNG singles L1/L2/L3/lineup/6families/selection/preview/behind+HP2.0/dense и короткое непрерывное видео earned lifecycle/positive chain. Production Update и test clock/replay/assigned positions/HP явно подписаны. HP/status anchors не меняются, чужие renderer не отключаются ради occlusion.
- ART3F-07: Static/full Edit/full Play/compile/import/ref/roundtrip/diff checks с positive totals; actual rendering inputs/testedSource/evidence head, актуальный source/artifact/protected-baseline verifier и малые controls без копии Unity project/self-hashing loop. Независимое review кода/источников/визуала.

Риски: нечитаемые крупные позы, occlusion HP/маршрута, localID drift, type-less cache, неверный primary-inclusive Storm proof, недостаточный earned бюджет, неактуальный rendering certificate. Каждый покрыт scenario выше.

Работа в текущем checkout/Library, последовательно одним владельцем. Без новых worktrees/клонов/полных копий, без reset/clean/stash. Четыре исходных пользовательских файла сохраняются; reuse85certificate допустим только после проверки exact SHA/области применимости. Read-only verifier не называется Unity run.

**Device Deferred to prerelease QA #53/#54/#55.** Телефон/export/signing/native video/TestFlight не нужны. Parent44 остаётся5/6 до принятия/merge; G2/device/map/enemies/UI/finalVFX/release отдельно. PR Closes86/Refs44 после полного scope; self-merge/release запрещены.

## План реализации

- [ ] Source: author_storm.py/export_storm.py/check_geometry.py, три Tower_StormCarpL1–3.blend/FBX. Проверить source budgets и отличия поз.
- [ ] Binding: добавить stormLevels; StormModelBuilder.Build/Bind/PrefabPath сохраняют SteamModelBuilder.Bind chain. BattleSceneBuilder вызывает StormModelBuilder.Bind. Проверить authored refs/repeated builds/fallbacks во всех шести семействах.
- [ ] Regression: StormModelAssetTests и StormModelPlayModeTests, минимально обновить superseded fallback tests. Отдельный intrinsic fixture подтверждает ровно две secondary/no primary/HP attribution; earned script использует публичные команды, полный AbilityBossTrace.Snapshot для restore.
- [ ] Evidence: CaptureArt3f/RecordArt3f, реальный reexport, новые full suites, actual-input receipt и scoped delivery verifier/controls. Сохранить точные команды и ограничения; независимый reviewer читает код/source/визуал перед PR.

## Воспроизводимый earned путь

Ограниченный поиск2610wave outcomes нашёл public-command путь GALE(1,0) с Preparation1, GALE(-2,0) с Preparation2, TIDE unlock после3, STORM(1,-1) с Preparation4, FLAME unlock после6, STORM L2 с Preparation7. На Preparation8 публично продаются обе GALE (56+56), оплачивается STORM L3.51kills/15HP, доступный бюджет497, investment495, Gold2. Cast ticks/координаты фиксированы в `review/ART.3f/inputs/earned.commands.json`; setters/DTO edits отсутствуют.

Fresh PlayMode regression повторил earned run и два file/menu Continue на каждом уровне, полный Combat rollback с live Grid, repeated/invalid commands, Sell/restart. Видео replay-ит byte-identical earned L1 checkpoint с SHA/readback, затем public UI/API команды. Prefix волн4–7 использует явно подписанный test clock×4 при fixed30Hz, финальный Combat8 — production Update. Captured PTS не ускорены/не склеены; sparse capture/encoding overhead не benchmark.
