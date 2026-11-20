# ART.2b — physical iPhone art proof (#43)

19.11.2026. Основа: main `b281ec0e72e46a80b5c79ee296487a10d26d4e32`, PR74 merged, reviewed head76677de. #41/#42/#73 завершены в своих scopes. Текущий эталон — летящий FLAME projectile; старые impulse/graybox captures не являются входом этой проверки.

## Scope и acceptance до реализации

Отдельный явный `art-reference` iOS export profile, ArtReference/TestWave (`legacy-single-wave-v1`), version0.1.0/build43. Production default остаётся Bootstrap→MainMenu→Battle/content v2. Source scenes/settings, Domain/balance/schema4/saves/rewards, packages и исторические evidence не меняются. ArtReference snapshot delay6ticks остаётся только в эталоне.

Нужны настоящий export→signed development build→install→launch на connected physical iPhone, нередактированные device frames/normal-speed recording (или точный blocker), runtime probes front/side/behind и synthetic12/.48m с HP. Показать nonlethal/lethal arrival, pause/cleanup/повторные циклы при исходной camera/scale. Изолированные probes не подменяют обычный advancing бой.

Измерения: warmup5s +30s sampling, минимум3повторения normal/behind/dense+projectile. Фиксировать samples, median/p95/max Unity player-loop interval, CPU/GPU только при реально доступном counter, Unity allocated/reserved memory отдельно от native resident memory. Frame interval не является CPU/GPU или доказательством display presentation. Baseline/peak/после повторений; модель/iOS/build/refresh/render resolution/safe area/target60/vsync/quality/shadows/AA и profiling overhead обязательны. Недоступные метрики — NOT VERIFIED. Предварительный target16.67ms для60fps, geometry/material/texture ограничения ART_PIPELINE сверяются отдельно; budgets предлагаются по измерениям для review, не объявляются утверждёнными автоматически.

## Regression scenarios

- ART2B-01: default exporter сохраняет production scene/content validation. Art profile выбирается явно, имеет отдельный output, entry и receipt purpose/profile/scenes/scenario/content/source/version; production receipt не проходит art validation и наоборот. Unknown profile отклоняется до Unity; source SHA/drift/previous-success/symlink protections сохраняются.
- ART2B-02: Editor validator читает текущую сохранённую ArtReference и её TestWave reference без regeneration/save; source scene/settings bytes прежние. Production validation остаётся обязательной также для art export.
- ART2B-03: probe активируется только явным launch argument в ArtReference. Synthetic views/HP помечены и отделены от authoritative simulation; switch/restart/unload чистит объекты/strikes, pause freeze, обычный бой возвращается без Domain mutation от probes. Буферы ограничены.
- ART2B-04: frame sampling имеет положительные samples/длительность/повторы, исключает warmup/setup/file writes; CPU/GPU0 не выдаётся за измерение. Raw samples сохраняются, статистика пересчитывается независимо. Background/pause прерывает измерение, не скрывает gap.
- ART2B-05: physical frames ordinary combat/front/side/behind/dense; projectile flight→arrival→impact, HP/lethal/death и pause/cleanup. Видео normal-speed отдельно от stepped phase captures. DeviceHub input не заменяет human multi-touch.
- ART2B-06: static, host exporter tests, available EditMode/PlayMode, compile/meta/GUID/diff; sanitized tracked evidence+hash manifest+clean-checkout verifier. Независимый technical/visual/evidence review. Только полный acceptance и принятые budgets позволяют Closes43; иначе Refs43. Без self-merge/release/TestFlight.

## Порядок выполнения

1. Добавить failing exporter/profile tests, минимальный отдельный build entry и проверить сохранность production contract.
2. Добавить изолированный runtime probe/ограниченную telemetry и meaningful lifecycle tests.
3. Выполнить Unity gates, commit source; export exact committed SHA в disposable worktree, проверить receipt/import audit, подписать существующим локальным workflow.
4. Установить/запустить physical player, собрать кадры/запись/повторные измерения и ограничения; исправлять только обнаруженные локальные дефекты с before/after.
5. Добавить sanitized evidence/manifest/verifier, независимое review и узкий PR.

Риски: недостаток disk space, недоступность CoreDevice recording/profiler counters, отличие Unity allocated от resident footprint, высокая модель iPhone16Pro не представляет базовый iPhone. Неполное evidence не превращается в performance guarantee или разрешение массового арта. #47/#53/#54/G2 остаются отдельными.


## Device revision — build44

Build43/source4141764 реально export/build/install/launch прошёл, но managed command-line activation не включила harness на IL2CPP iOS: нет overlay/ART2B logs. Этот первый exploratory art frame не является measurement run. Следующий explicit candidate0.1.0/build44: встроенный `Data/Raw/art2b-build.json` активирует probe только в ArtReference. Обычные production builds этого marker не содержат. Scenario задаётся `VEILWARD_ART2B_SCENARIO=normal|front|side|behind|dense` через `devicectl --environment-variables`; default внутри art profile normal. Managed `-art2b` сохраняется для поддерживающих его сред. Regression: empty managed args+art receipt активирует; без receipt не активирует; Battle не активирует; unknown scenario fail-fast. Actual device proof должен подтвердить ART2B_ATTACH/settings и run completion перед учётом измерений.
