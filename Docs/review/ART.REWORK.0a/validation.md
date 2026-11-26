# ART.REWORK.0a — проверки

Проверенный код `12c0fc7ef8cb42777b7d5ec112ed0be0712a3ab8`, существующий checkout `codex/art-rework0-ink-lanterns`, Unity 6000.3.28f1, macOS / Apple M4 Pro / Metal. Сборка и редактор использовали прежнюю Library. Четыре входных локальных изменения URP/материалов и остальные локальные файлы сохранены побайтно и исключены из PR.

## VERIFIED

- `./Scripts/run-static-checks.sh` — 38/38 и PASS для версии, пакетов, .meta, GUID и сборок. `git diff --check` — PASS. [Статика](reports/static.txt)
- Полный EditMode 1308/1308, полный PlayMode 106/106, пропусков и inconclusive 0. Включены REG214, проверка исходного FLAME projectile, Continue/Reset и отдельная проверка анимации лиса при `Time.timeScale = 2` и Pause. [EditMode](reports/EditMode.json.gz) · [PlayMode](reports/PlayMode.json.gz)
- `unity recompile --project-path "$PWD" --timeout 90 --format json` — 0 ошибок. Один существующий CS0219 в `BossGreedySearchTests.cs`. [Компиляция](reports/compile.json)
- `python3.12 ArtSource/InkAndLanterns/prepare_sprites.py --check` — 12 PNG воспроизводятся побайтно. Два импорта через `Veilward.Editor.InkBattleArtBuilder.ImportAndBindBattle()` сохраняют отпечатки 41 файла, включая prefab, material, PNG, .meta и Battle. [Повторный импорт](reports/import-roundtrip.txt)
- Полный GameView, 817 кадров штатной волны и 500 кадров стресс-сценария. Снимки и видео показывают движение, перекрытия, штатные выстрелы и попадания, смерть/выход и паузу. В штатной записи выполнен файловый Continue. [Видео и кадры](README.md)

### Команды Unity

```sh
unity command test run --caller plugin --skill unity-cli --format json --timeout 600 -- --mode editor
unity command test run --caller plugin --skill unity-cli --format json -- --mode playmode --async-tests true
unity command test status --caller plugin --skill unity-cli --format json
```

Обе suites запущены целиком в одном Editor, EditMode перед PlayMode. Первый PlayMode 106/106 прошёл на Battle, пересобранном тестами builder. Повторный PlayMode 106/106 выполнен после восстановления проверенных файлов с сохранённой Battle-сценой. Отчёт PlayMode относится к повторному прогону. Для PlayMode итог получен после завершения асинхронного запроса. Отчёты сохранены в отдельном каталоге, чтобы сохранить прежние локальные результаты.

### Воспроизведение записи

[Сценарий записи](tools/InkBattleCaptureLocal.cs) временно компилировался в редакторе. Для повторения его можно поместить рядом с presentation scripts перед входом в Play Mode. После подтверждения, что GameView продолжает рисовать кадры, вызов через `unity command script eval --caller plugin --skill unity-cli` — `Veilward.Presentation.InkBattleCaptureLocal.Begin("earned");`, затем `Begin("stress");` после завершения первой записи. Источник пикселей — `ScreenCapture.CaptureScreenshotIntoRenderTexture`/GPU readback для ролика, `CaptureScreenshotAsTexture` для PNG, с полным UI. Сценарий снят с runtime после записи. Формат, декодирование, кадры и хеши проверяются `python3.12 Docs/review/ART.REWORK.0a/verify_delivery.py`.

## NOT VERIFIED / BLOCKED

- Независимое техническое ревью и художественное решение владельца по игровому масштабу ожидаются.
- Физический iPhone, touch, thermal и production memory/frame time — Deferred to prerelease QA #53/#54/#55. Замеры файла записи описывают захват GameView.
