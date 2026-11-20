# VEILWARD — Development Roadmap v0.1

20.11.2026 / #78: ART.3a FLAME принята владельцем merge PR77; ART.3b добавляет только TIDE L1–L3. Следующие ART/VFX/UI/аудио/обучающие пакеты проходят Editor/Play Mode/source/import/roundtrip и независимое review, а physical iPhone video/performance/readability отложены до общей prerelease QA #53/#54/#55. Отсутствие per-family device proof само по себе не блокирует приёмку/закрытие завершённого Editor scope. Родитель #44, остальные семейства, G2 и release остаются отдельными gates; старые записи ниже — исторические статусы.

**Planning assumption:** один сильный AI-разработчик (Kimi K3.1) + человек, который утверждает продуктовые решения и проводит device-тесты + GPT-6 как независимый ревьювер. Оценка 16–24 недели до первой товарной версии при готовности ассетов; с нуля до polished premium-визуала может занять дольше. Это не обязательства по срокам.

## Decisions / Gates

- **G0 / Concept lock:** утверждены core loop, формат iOS, 4 стихии, фиксированные маршруты, art direction, non-goals.
- **G1 / Technically playable:** можно поставить башню, запустить волну, выиграть/проиграть, обойтись без ручных изменений Scene после сборки.
- **G2 / Fun gate:** в вертикальном срезе реакции понятны, есть не менее 2 жизнеспособных способов победы; наблюдаем минимум 5 внешних плейтестов.
- **G3 / Content complete:** 3 карты, все 10 башен, 15 волн, tutorial, сохранение, настройка сложности.
- **G4 / Release readiness:** реальные устройства, тестирование IAP (если включено), App Store requirements, тест сборки/TestFlight.

## Phase 0 — Foundation (1 неделя)

**P0.1:** Unity 6.3 LTS project / URP / iOS, gitignore, meta visible, assembly definitions, bootstrap/menu/battle scenes, первые EditMode smoke tests и инструкции локального запуска. **P0.2:** чистый C# domain foundation, config IDs, инварианты моделей, контракт фиксированного шага 30 Hz и Domain EditMode tests; полноценный gameplay не входит. **P0.3:** CI Editor tests, build instructions, reproducibility. **Deliverable:** empty Battle scene runs in Editor and on iPhone.

## Phase 1 — Core TD (2–3 недели)

**P1.1:** fixed-path enemies + health/leaks. **P1.2:** grid and placement validation. **P1.3:** targeting and ranged attacks (one dummy tower). **P1.4:** waves, gold, sell, upgrade, win/lose. **P1.5:** pause/background checkpoint. **Deliverable:** playable graybox 8-wave match.

**P1.4 status:** P1.4a Multi-Wave Lifecycle & Rewards, P1.4b Tower Economy и P1.4c Gameplay Validation & Balance Baseline завершены. PR #10 (`dc17cca`) merged в `main` 2026-11-10. Финальные локальные gates P1.4c: static 38/38, EditMode 208/208, PlayMode 3/3, `git diff --check` PASS; сохранены deterministic replay evidence и десять Play Mode кадров 1600×900.

**P1.5a status:** Pause & Background завершён и прошёл независимое review. PR #11 (`786095d`) merged в `main` 2026-11-11. Добавлены latched manual/lifecycle pause, frozen fixed-step simulation без catch-up, blocked commands/UI, pending-action cleanup, modal Resume и frozen beams. Локальные gates: static 38/38, EditMode 221/221, PlayMode 9/9; три кадра 1600×900 и scenario Console report с 0 errors находятся в `Docs/review/P1.5a/`. Physical iPhone background/focus, built-player touch, safe area, Simulator, signing и Unity CI остаются NOT VERIFIED / BLOCKED и не заменяются Editor evidence.

**P1.5b status:** Wave Checkpoint & Continue завершён и прошёл независимое review. PR #12 (`72591db`) merged в `main` 2026-11-13. Версионированный JSON checkpoint сохраняет Preparation/WaveComplete/Victory/Defeat с атомарной заменой и validated backup; прерванный Combat откатывается к Preparation перед Start Wave, а повторный Continue не начисляет награды повторно. MainMenu предоставляет явные New Game/Continue; Start Wave блокируется при failed save. New Game reset сохраняет recovery copy, а повреждённые или исчерпанные next IDs обрабатываются через backup fallback либо rejection без mutation. Локальные gates: static 38/38, EditMode 261/261, PlayMode 12/12, 0 failed/inconclusive/skipped; визуальный сценарий 1/1 и три кадра 1600×900 с Console 0 errors находятся в `Docs/review/P1.5b/`. Physical iPhone force quit/relaunch, built-player touch, safe area, Simulator, signing и Unity CI остаются NOT VERIFIED / BLOCKED.

## Phase 2 — Elemental vertical slice (3–4 недели)

**P2.1:** all 4 basic archetypes and status pipeline. **P2.2:** 2 hybrid towers + reactions Steam and Storm. **P2.3:** choose/unlock elements after defined waves. **P2.4:** basic ability and boss. **P2.5:** readable touch HUD, explanations and onboarding. **Deliverable:** 1 visually coherent map, 8 waves, one boss, 10 external tests. **Gate G2:** revise design if reactions aren't fun/understandable.

**P2.1a status:** подготовлены спецификация FLAME/TIDE/GALE/STONE, детерминированный status/tick/checkpoint contract, REG-BATTLE-090…127 и план PR P2.1b–P2.1e в `Docs/P2.1A_ELEMENTAL_TOWERS.md` и `Docs/P2.1A_IMPLEMENTATION_PLAN.md`. Все числа обозначены как гипотезы для плейтестов; реализация, reactions, hybrids и element unlocks в этот документационный scope не входят.

**P2.1b–d status:** definitions/schema 2, active status runtime и каталог/UI находятся в `main`; база P2.1e — merge PR #16 (`6b26a87`, 2026-11-15). Реальные Play Mode кадры каталога, боя, Upgrade/Sell/Continue и ограничения сохранены в [P2.1d QA](review/P2.1d/qa.log).

**P2.1 status: DONE после merge P2.1e.** [PR #17](https://github.com/AlexeyKravtsov95/Veilward/pull/17) merged в `main` 2026-11-15, merge commit `5e8f541`. Последовательность P2.1a–e завершена: definitions/schema 2, четыре базовых статуса, каталог/UI и интеграционная проверка. REG-BATTLE-125…127 проверяют saved EightWave, экономику и файловый Continue/Combat rollback. Естественный результат фиксированной стратегии: Defeat на волне 8, tick 3997, HP 0, Gold 112, Kills 46, 8 DoT kills; [balance review](review/P2.1e/balance-review.md), [replay JSON](review/P2.1e/replay-summary.json). Зафиксированные локальные gates P2.1e: static 38/38, EditMode 317/317, PlayMode 15/15, 0 failed/inconclusive/skipped; `git diff --check` PASS. Это завершение технического scope P2.1. **G2 и готовность баланса остаются открытыми:** внешние плейтесты, physical iPhone/touch/safe area/performance и утверждение баланса ещё не выполнены. Реакции/гибриды/unlocks в P2.1 не входят; прежние формулировки «ожидает merge» в отчёте P2.1e относятся к моменту его подготовки.

**P2.2 status: технический scope DONE после review и merge P2.2f.** [PR #23](https://github.com/AlexeyKravtsov95/Veilward/pull/23) merged, merge commit `a506bed`. Definitions, resolver Steam/Storm, production гибриды/schema 2, graybox presentation и детерминированные replay/rollback/impact measurement завершены. Все шесть типов доступны с Preparation по утверждённому временному P-05 до P2.3. [Impact review](review/P2.2f/impact-review.md) не является утверждением баланса. **G2, external playtests, физический iPhone/performance QA, balance approval и TestFlight остаются открытыми.**

**P2.3 status: технический scope DONE.** [PR #32](https://github.com/AlexeyKravtsov95/Veilward/pull/32) merged в `main`, merge SHA `8128010051c2b048b32b0a48df5b862bb2d5a7da`. P2.3a/b/c/d/e/e.1: Domain requirements, native FLAME/GALE и unlock3/6, schema3/legacy routing, UI, natural Victory8 обоих стартов и late file/menu/Combat7. [Acceptance/evidence](review/P2.3e.1/acceptance-matrix.md), [независимое review PASS](review/P2.3e.1/review.md). P2.3e Defeat5 остаётся историческим контролем; его прежние BLOCKED/ожидание merge не являются текущим статусом P2.3. Device/G2, баланс, ART approval и TestFlight отдельно открыты.

**P2.4a status: SPECIFIED, документационный PR #35.** [Способность/босс, API/state/save/input contract и D-01…D-07](P2.4A_ABILITY_BOSS.md), [план #36–40 и REG190…217](P2.4A_IMPLEMENTATION_PLAN.md). Числа предлагаются владельцу; runtime/configs/scenes/art не изменены. Новые REG только SPECIFIED; фактические gates и review — [evidence](review/P2.4a/validation.md). Ability/boss ещё не реализованы.

**ART.1 status: технический прототип влит PR #24 (`e33142a`); направление НЕ утверждено владельцем.** Отдельная `ArtReference` использует сохранённую игровую камеру/масштаб, оригинальные Blender sources → FBX, фрагмент парящего святилища, FLAME «Фонарь углей», Shade и event-driven FLAME stroke. [Спецификация](ART1_VISUAL_PROTOTYPE.md), [материалы просмотра и ограничения](review/ART.1/README.md), [asset manifest](review/ART.1/asset-manifest.md). Проверки Editor и source round trip перечислены в evidence; это не device/performance PASS. Массовое производство и художественная приёмка ожидают решения владельца и проверки на физическом iPhone.

**ART.1.1 status: технический PR #25 влит; художественное утверждение владельца остаётся открытым.** Объём/характер острова, уменьшенные свесы FLAME и контраст нижнего плаща Shade при прежних gameplay coordinates, камере и масштабе. [Спецификация и регрессии](ART1_1_VISUAL_REFINEMENT.md), [before/after, бой и проверки](review/ART.1.1/README.md). Static 38/38, EditMode 547/547, PlayMode 30/30; round trip 8/8 и отрицательный exporter control PASS. [Независимое техническое review](review/ART.1.1/technical-review.md): без открытых блокирующих замечаний. История ART.1 сохранена. Финальное утверждение стиля и device readability остаются открытыми до просмотра владельцем и физического iPhone; массовое производство, self-merge и release не выполняются.

**P2.3e.1 — проверка стратегий:** расширенный ограниченный поиск нашёл natural Victory8 для FLAME и GALE, в отдельных coverage scripts оба Steam/Storm pair+intrinsic с положительным HP loss. [Scripts, бюджет, отсечения и evidence](review/P2.3e.1/validation.md). **VERIFIED** late file/menu matrix: pending/confirmed6, failed-save/retry, Combat7 после kill+positive proc; static38/38, EditMode761/761, PlayMode45/45, Visual2/2, 28 реальных кадров и Console0 unexpected errors; исторические P2.3e Defeat5 остаются контролем. Технический P2.3 DONE после merge #32; device/G2, баланс, ART approval и TestFlight отдельно.

### Последовательность до первой TestFlight

**Технические P2.2/P2.3 DONE → P2.4 способность/босс → оформление первого среза → P2.5 обучение → G2/device QA → M0.** ART direction/device проверка эталона — отдельный обязательный gate перед массовым производством. Полный индекс релизов/зависимостей — [issue #33](https://github.com/AlexeyKravtsov95/Veilward/issues/33); сроки не назначены.

| Этап | Результат и условие перехода | Статус |
|---|---|---|
| P2.2f | Детерминированные матчи/rollback и измерения, независимое review/merge | DONE: PR #23 / a506bed |
| Эталон арта | Фрагмент карты, одна башня, один враг и эффект в едином направлении; Blender `.blend` → FBX → Unity по [ART_PIPELINE](ART_PIPELINE.md) | ART.1 merged PR #24; ART.1.1 технически merged #25; утверждение владельца и iPhone check открыты |
| P2.3 | Настоящее открытие стихий/требования гибридов в Domain и UI | P2.3b/c merged #27/#28; P2.3d merged #30. P2.3e historical Defeat5 сохранён; P2.3e.1: оба старта natural Victory8, оба Steam/Storm pair+intrinsic positive, after6/file/Combat7/menu/visual PASS; [новое evidence](review/P2.3e.1/validation.md). Технический scope DONE после merge #32 / 8128010; G2/device отдельно |
| P2.4 | Базовая способность и босс, проверяемая интеграция восьми волн | #35–39 merged; #40 PR70 revision3 — winning integration, Review до merge |
| Оформление первого среза | Одна оформленная карта, все шесть башен, враги/босс и согласованные UI/VFX/звук | Не выполнено |
| P2.5 | Обучение, объяснения, внешние плейтесты и согласование баланса; G2 остаётся gate | Не выполнен |
| Device QA | Физический iPhone: touch/safe area, background/force quit/Continue, allocations/frame time/memory/thermal | Не выполнен |
| Первая TestFlight | Одна оформленная карта, шесть башен, восемь волн, босс, обучение и согласованные UI/VFX/звук | Не выполнена; отдельное approval |

Технический P2.2 завершён; ART.1 проверяет художественную гипотезу, не закрывая G2 и баланс. Массовое производство начинается только после утверждения эталона владельцем и проверки его в Unity **и на физическом iPhone**. Происхождение/лицензии, source, `.meta`, budgets и import checks — по ART_PIPELINE. ART.1 не добавляет механики или оформление остальных юнитов.

**Обязательное условие владельца для первой TestFlight — готовый арт первого среза.** Graybox с зелёными тестами этого условия не выполняет. Первая TestFlight — ограниченный вертикальный срез, а **полный релиз остаётся отдельной целью**: десять башен, три карты, пятнадцать волн и остальной release scope GDD/Phase 3–5. Ни первая TestFlight, ни полный релиз не отмечены выполненными.

Будущие крупные multi-island карты с мостами/камерой/отдельным балансом, выбор и последовательное открытие миссий, рекорды/звёзды, финальные миссии с боссами и отдельный campaign progress записаны в [BACKLOG](BACKLOG.md). Открытие секторов внутри забега — самостоятельная исследовательская идея. В ART.1.1 эти механики не реализуются; первая TestFlight сохраняет одну законченную миссию с готовым артом.

## Phase 3 — Expand to release content (4–6 недель)

**P3.1:** remaining four hybrid towers, tests, VFX/audio. **P3.2:** 3 unique maps, enemy groups, boss phases. **P3.3:** 15-wave balance, Normal/Hard. **P3.4:** campaigns/results/stars, cosmetics/unlocks. **P3.5:** tutorial polish, status glossary, animation feedback, accessibility. **Deliverable:** content complete build.

## Phase 4 — Platform quality (2–4 недели)

**P4.1:** iPhone/iPad landscape layout + safe areas. **P4.2:** asset budgets, IL2CPP/Metal, profiled stress test. **P4.3:** save migrations, interrupted session, airplane mode. **P4.4:** localization, sound, user settings. **P4.5:** App Store artwork/privacy/terms and IAP product + Restore if paid unlock is selected. **Deliverable:** platform quality для release scope; первая TestFlight вертикального среза имеет отдельную последовательность выше.

## Phase 5 — External beta & App Store (2–3 недели)

**P5.1:** TestFlight + structured feedback (confusion points, loss reasons, device stats). **P5.2:** prioritized bug fixes and balance adjustments. **P5.3:** final smoke on devices, crash-free run, Store metadata, screenshots. **P5.4:** release decision. **Deliverable:** submit-ready build.

## First PR sequence (small, reviewable)

1. `P0.1 project bootstrap` — Unity version/URP/landscape/meta/gitignore/readme; no gameplay.
2. `P0.2 domain skeleton` — testable battle model, config IDs, first pure-C# EditMode tests.
3. `P1.1 enemy wave loop` — spawn/path/leak, deterministic test.
4. `P1.2 tower placement and attack` — one dummy tower, cost/refund/targets.
5. `P1.3 mobile battle UI` — touch placement and HUD, device screenshots.

For each PR: scope, docs updates, test commands+results, on-device evidence if UI/performance changes, known limitations. GPT-6 reviews code architecture, Unity serialization, economy invariants, basic UX, performance regressions and remaining risks before merge.

## Non-negotiable stop rules

- Do not start building 10 towers if a 4-tower demo is not fun.
- Do not purchase expensive art before fixed camera/layout and visual direction are tested on iPhone.
- Do not launch multiplayer/backend before first single-player release is validated.
- Do not mass-produce 3D assets with an agent if their copyright/licensing status is unclear.
- If performance is unstable, fix performance before adding a flood of particles.

## KPI / evaluation (not launch promises)

Playtest comprehension: >=70% discover or execute first reaction following the tutorial; session objective 12–18 min; 3+ viable strategies per map; no reproducible blockers; no rewards duplicated on resume; good frame pacing on baseline iPhone. Store business metrics can only be estimated after testing positioning and discovery; do not invent expected revenue.


**Исторический P2.3e:** A/B Defeat5/tick2246 и ранние ограничения сохранены в [исходном evidence](review/P2.3e/validation.md). Последующий P2.3e.1 закрыл late acceptance без gameplay/balance/config изменений; после review и merge #32 технический P2.3 DONE. Исходные отчёты не переписаны. Device/G2, физический iPhone, баланс, ART approval и TestFlight этим не закрыты.

**Релизные границы по #33:** M0 — одна finished mission/map, шесть башен, восемь волн, одна способность, один босс, tutorial, готовые UI/VFX/audio, human G2/баланс и реальные iPhone QA/profiling; отдельное approval распространения, graybox TestFlight не выпускается. M1 — стабилизация закрытой беты (#57). Полный1.0 —10 башен/3 карты/15 волн/Normal-Hard/кампания/звёзды и отдельный progress save (#58–63); post1.0 EXP — исследование (#64). Эти задачи не расширяют #35 или M0.


**P2.4b/#36: Review, не merged.** Pure Domain «Печати Разрыва» и REG190–197 реализованы после merge #65 от свежего main; production ability-disabled, enabled только internal synthetic fixture. Сохранения/schema4/UI/boss/wave8 — следующие #37–40. D-01…D-07 приняты владельцем как исходные правила, не final balance/G2/device/ART approval. [Фактические gates и независимое review](review/P2.4b/validation.md).

**P2.4c/#37:** schema4, immutable ability checkpoint, старые content IDs/миграции, независимая проверка slots и полный Combat rollback реализованы, ожидают review/merge. Production ability-disabled; enabled persistence только internal synthetic fixture. [Evidence](review/P2.4c/validation.md). UI/final mission/boss остаются #38–40; physical device/G2/ART/release не подтверждаются.

**P2.4d/#38:** UI/preview/Confirm/Cancel/cooldown, input lease/guards и event/VFX consumption реализованы и переданы на Review [PR #68](https://github.com/AlexeyKravtsov95/Veilward/pull/68), в отдельной ветке от merged #67 / `5af2121`. Production ability-disabled; enabled только internal synthetic fixture. Boss/final production activation #39, natural replay #40. [Scope/REG204–208](P2.4D_ABILITY_UI.md), [фактические gates/evidence/review](review/P2.4d/validation.md); статус готовности определяется evidence и owner merge, не этим описанием. Device/G2/ART/release отдельно.

**P2.4e/#39 — In progress / acceptance BLOCKED:** новая versioned ability+boss mission реализована в отдельной ветке, старые factories сохранены. Natural Victory FLAME/GALE не достигнута без изменения принятых чисел; draft Refs-only, без Closes/Done. [Scope](P2.4E_RIFT_WARDEN.md), [evidence](review/P2.4e/validation.md). #40/device/G2/ART/release отдельно.

**P2.4e / #39 — Review (draft), BLOCKED:** [PR #69](https://github.com/AlexeyKravtsov95/Veilward/pull/69), source b970003. Natural Victory FLAME/GALE не достигнута; полные Unity gates FAIL только на этих assertions. 26 Play Mode кадров и независимый source/evidence audit приложены; Refs#39/Refs#40, без закрытия #39, merge/release/TestFlight. [Evidence](review/P2.4e/validation.md).

Доработка PR #69: расширены все ordered choices и законные стратегии, лучшие попытки FLAME/GALE оставляют boss154/68 HP. **Draft / BLOCKED**, Victory assertions не ослаблены; EditMode1016/1018, PlayMode56/58, graphical1/3. Новые26 Play Mode кадров и точные лимиты поиска — [R2 evidence](review/P2.4e/revision-2/validation.md). Старый FAIL evidence сохранён; Refs #39 / Refs #40, без merge/release/TestFlight.

**Актуальный R3 / #39 — Review:** выбран1000HP/armor5 из разрешённой owner matrix, оба natural starts Victory8 и положительная ability по boss, четыре F/T/G coverage counters, natural Victory checkpoint/IO/Retry и terminal Continueoff проверены. Static38/38, Edit1048/1048, Play58/58, graphical3/3 PASS,34кадра; [R3 evidence](review/P2.4e/revision-3/validation.md). R1/R2 BLOCKED выше — исторические итоги. Done только после merge; G2/device/ART/#40 отдельно.


**P2.4f / #40 — revision3, Review до merge:** делегированный HP-проход выбрал600/5 в новом content v2; v1/1000/5 и все прежние evidence bytes сохранены. REG214 обоих стартов и полная winning trace/file/menu/Retry/Combat7–8 matrix зелёные. [Результаты, gates, timing-ограничения и независимое review](review/P2.4f/revision-3/validation.md). #40 остаётся open до фактического merge; human G2/device/ART/#52/#56/release отдельно. Без self-merge/release/TestFlight.
