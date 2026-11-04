# VEILWARD — Development Roadmap v0.1

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

## Phase 2 — Elemental vertical slice (3–4 недели)

**P2.1:** all 4 basic archetypes and status pipeline. **P2.2:** 2 hybrid towers + reactions Steam and Storm. **P2.3:** choose/unlock elements after defined waves. **P2.4:** basic ability and boss. **P2.5:** readable touch HUD, explanations and onboarding. **Deliverable:** 1 visually coherent map, 8 waves, one boss, 10 external tests. **Gate G2:** revise design if reactions aren't fun/understandable.

## Phase 3 — Expand to release content (4–6 недель)

**P3.1:** remaining four hybrid towers, tests, VFX/audio. **P3.2:** 3 unique maps, enemy groups, boss phases. **P3.3:** 15-wave balance, Normal/Hard. **P3.4:** campaigns/results/stars, cosmetics/unlocks. **P3.5:** tutorial polish, status glossary, animation feedback, accessibility. **Deliverable:** content complete build.

## Phase 4 — Platform quality (2–4 недели)

**P4.1:** iPhone/iPad landscape layout + safe areas. **P4.2:** asset budgets, IL2CPP/Metal, profiled stress test. **P4.3:** save migrations, interrupted session, airplane mode. **P4.4:** localization, sound, user settings. **P4.5:** App Store artwork/privacy/terms and IAP product + Restore if paid unlock is selected. **Deliverable:** stable TestFlight candidate.

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
