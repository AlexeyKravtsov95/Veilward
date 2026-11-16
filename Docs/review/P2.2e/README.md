# P2.2e — причина, результат и enemy inspector

Только Task 4 / §8. База `origin/main` **0c0cf0c** (merge PR #21), ветка `codex/p2.2e-reaction-presentation`, отдельный worktree. Domain damage, definitions, цены, config balance, packages и Unity version не менялись.

## VERIFIED

- REG-BATTLE-154…159: full local static **38/38**, EditMode **520/520**, PlayMode **24/24**; failed/inconclusive/skipped **0**. [EditMode](editmode-results.json), [PlayMode](playmode-results.json). Все прежние suites сохранены. Повторный batch/XML прогон на commit **`0b542abdcec85b62e638dbb7517767bf97ada2dd`** также PASS: [totals/SHA](gates.json), [EditMode XML](EditMode.xml), [PlayMode XML](PlayMode.xml). Последующие commits меняют только evidence/docs. [Source manifest](source-sha256.txt) вычислен из git blobs этого commit и совпадает с чистой итоговой worktree.
- Pooled captured Steam ring/impacts и Storm chain; direct beam отличается цветом/толщиной/высотой. Geometry не ищет исчезнувшие Views. Pool ограничен 24 элементами, при насыщении переиспользует oldest slot; stress regression подаёт 100 events.
- Два overhead slots, Steam tie priority, 0.5 s active-time cue, cooldown-aware pair readiness, intrinsic label отдельно; локальный layout разводит уже существующие slots. Inspector показывает все statuses/power/ticks, оба CD и последние объяснения обоих видов с реальным AppliedHpLoss; смерть закрывает selection, HUD сохраняет lethal итог.
- Pause/background, Resume без catch-up; UI/modals/inspector без build click-through; Restore/Continue/New Game очищают transient presentation. Same-ID/level Restore обновляет type/cell/attack origin. Builder/validator проверяют refs/missing scripts/layout.
- [Независимый локальный code review](review.md): оба найденных P2 исправлены с RED → GREEN. [GitHub PR #22](https://github.com/AlexeyKravtsov95/Veilward/pull/22) открыт для дальнейшего независимого review, без self-merge/release.

## Реальный Editor Play Mode QA

Unity **6000.3.28f1 (8f082d2fe476)**, macOS arm64, build target **iOS**; renderer текущий URP. Native `Screen` **1641×1007**, safe area `(0,0,1641,1007)`. Unity Pipeline `capture game_view --source screen --width 1600 --height 900` экспортировал все PNG **1600×900** с overlay HUD. Это export dimensions, не физический iPhone viewport. Capture делает настоящий кадр запущенной игры; кадры не дорисованы.

[qa.log](qa.log) содержит точные команды, raw traces/steps/totals, ошибки промежуточных RED/fixtures и их исправления. Последний capture каждого имени — финальный. [Console audit](console-audit.json): final QA после очистки Console — **0 errors / 0 warnings**, compilationFailed=false; это audit этого сценария, не заявление об отсутствии всех возможных ошибок.

| Кадры | Фактический сценарий |
|---|---|
| [01](01-catalog.png), [02](02-steam-preview.png), [03](03-storm-preview.png), [04](04-storm-occupied.png) | Saved EightWave/shared TestWave, temporary save slot: New Game → пустая клетка → каталог 6 типов → STEAM preview → STORM preview → Confirm → occupied panel. 150 → 40 Gold. |
| [05](05-continued-storm.png) | Production Combat автоматически прошёл tick 34 → 399 и WaveComplete, Gold 60, Kills 0; фактический HUD «Шторм: 2 целей, 12 HP». MainMenu → Continue восстановил WaveComplete, tower, ресурсы и пустые summary/selection/effects. Это не полный replay/impact. |
| [06](06-steam-pair.png), [07](07-storm-pair.png) | **Synthetic in-memory fixtures**, реальные Domain resolver/events и runtime presentation: Burn+Wet → Steam, Wet+Gust → Storm. 3 hits/30 HP и 2 hits/14 HP соответственно. |
| [08](08-steam-intrinsic.png), [09](09-storm-intrinsic.png) | **Synthetic** intrinsic без наложенных статусов: STEAM/STORM «встроенная», 3 hits/30 HP и 2 hits/20 HP. |
| [10](10-steam-lethal.png), [11](11-storm-lethal.png) | **Synthetic lethal**: Steam удалил primary и обе secondary до render, 3 hits/15 HP. Storm сохранил primary и удалил оба hop victims, 2 hits/10 HP; промежуточная смерть не разрушила цепь. Direct 10 HP отдельно. |
| [12](12-dense-inspector.png), [13](13-pause.png) | **Synthetic** 4 врага × Burn/Wet/Gust/Crack, две реакции в tick 1 (seq 1/7), Steam cue priority; slots `[2,2,2,2]`. Inspector содержит оба объяснения (Steam 40 HP / Storm 14 HP), оба CD 60. Pause overlay имеет приоритет. |
| [14](14-basic-pairs-preview.png) | Saved production TIDE preview: обе пары, условие нелетального direct и CD из catalog; панель прежнего размера. |

Fixture source: [qa-fixture.cs.txt](qa-fixture.cs.txt), выполнялся через `script eval --file` вне Assets, не импортировался в player. Synthetic primary продвинут впереди secondary для First targeting, добавлены in-memory statuses/enemies; saved configs не менялись. После одного реального Step consumer забрал события; capture фиксирует `Time.timeScale=0` и вручную продвинутый VFX на 0.2 s (remaining 0.3). Визуальные fixtures не проверяют баланс. Freeze именно Pause при обычном timeScale=1 проверяется отдельными PlayMode lifecycle regressions с `WaitForSecondsRealtime`.

## NOT VERIFIED / ограничения

Physical iPhone readability/touch/safe area/background, built-player/IL2CPP/Metal execution, performance/allocations/thermal, signing, Unity CI, внешние плейтесты и баланс не проверены. Editor screenshots и reference-height font/layout assertions их не заменяют. G2 остаётся открыт. Полный replay/rollback/impact относится к P2.2f. Финальный арт/звук — отдельный этап.
