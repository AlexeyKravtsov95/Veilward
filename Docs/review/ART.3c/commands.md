# Команды и окружение ART3c

Рабочая ветка `codex/art3c-gale-levels`, отдельный `.tools/worktrees/art3c-gale`, fresh main b2d31095238e8ce5e276ea1ba0a7741b8afee82f. Tested source9871af6. Все Unity команды адресованы через `--project-path "$PWD"` только этому checkout. Чужие Editor/worktrees не менялись. Ниже команды запускались из task root.

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Blender/ART3/GALE/author_gale.py
for level in 1 2 3; do
  /Applications/Blender.app/Contents/MacOS/Blender --background "ArtSource/Blender/ART3/GALE/Tower_GaleTalismanL${level}.blend" --python-exit-code 1 --python ArtSource/Blender/ART3/GALE/export_gale.py
done
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Blender/ART3/GALE/check_geometry.py
unity command script eval --caller plugin --skill unity-cli --project-path "$PWD" --code 'Veilward.Editor.GaleModelBuilder.Build();'
PYTHONDONTWRITEBYTECODE=1 ./Scripts/run-unity-tests.sh EditMode batch
./Scripts/run-play-mode-tests.sh
PYTHONDONTWRITEBYTECODE=1 ./Scripts/run-static-checks.sh
git diff --check
PYTHONDONTWRITEBYTECODE=1 python3 ArtSource/Blender/ART3/GALE/verify_reexport.py
unity command script run --caller plugin --skill unity-cli --project-path "$PWD" --file ArtSource/Review/CaptureArt3c.cs --entry CaptureArt3c.Capture
unity command script run --caller plugin --skill unity-cli --project-path "$PWD" --file ArtSource/Review/CaptureArt3c.cs --entry Art3cEditorRecording.BeginRecording
PYTHONDONTWRITEBYTECODE=1 python3 ArtSource/Review/encode_art3c_video.py
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.3c/verify_delivery.py
```

Capture preflight: direct saved Battle в actual Play Mode, task GameView1600×900, auto tick и RunInBackground для собственной сессии. Natural screenshots/earned checkpoints: `ART3C_CAPTURE=1` установлен через System.Environment в taskEditor, затем `test run --mode playmode --filter GaleModel --async-tests true`, итог через `test status`. Это отдельный targeted capture run; полный PlayMode gate запускается без filter. Synthetic capture требует пустого direct Battle, читает real read-only Gust snapshot из отдельно восстановленного earnedL3; live Domain/DTO не редактирует. Preview использует LoadPreview→SelectPreview и Pipeline capture game_view source screen. Запись — in-memory harness, который использует composited GameView RT/encoder уже установленного Pipeline, public UI/API и isolated store.

FBX timestamp header после reexport возвращается к committed source bytes, затем тот же Unity snapshot повторно читается в reexport-canonical-readback.json. Это не замена geometry proof: before/after actual reexport3 уже сравнивается независимо. Protected assets после Unity serialization возвращаются restore_generated.py с audit/patch и точными SHA; canonical source остаётся в Git.

Unity6000.3.28f1/8f082d2fe476, URP17.3.0, Pipeline0.10.0-exp.1, CLI1.0.1, Blender5.3.0/02f0899d78e3, macOS27.0.1 ARM64. BuildTargetiOS, QualityMobile, game camera(8,11,8)/ortho8.5, light1.2, PNG1600×900. Package versions/lock не менялись.

## Окружение и неуспешные попытки

Первый `unity open` дочерний процесс завершился до подключения Pipeline; явный foreground Editor process с task log затем работал. Во время первого resident EditMode диск дошёл до116MiB; процесс sample показал AssetDatabase.SaveAssets→File::Open→retry NSAlert. Suite не завершён и не PASS. Остановлен только taskEditor. Локальные immutable packages сначала повторно использовали готовый cache, затем заменены независимыми APFS clone copies; неизменённые task source/evidence также COW clones для экономии места. Чужие файлы не перезаписывались, не удалялись и не сбрасывались. Cache/Temp/TestResults ignored и в source не входят.

Первый полный batchEdit1159/1161 выявил только прежние GALE-graybox checks REG117/119. Они заменены сильным production mesh/palette/ref/no-graybox contract, без ослабления FLAME/TIDE. Исходный failed XML сохранён отдельно. Source geometry первый L2 width assertion обнаружил однонаправленные дуги; левая зеркалирована в source до final proof. Перенесённый семицветный TIDE UV assertion исправлен на шесть фактически используемых GALE palette cells; ни одного старого FLAME/TIDE geometry assertion не ослаблено.

BatchPlayMode `-nographics` дал Null Device и crash в существующем rendering test FlameProjectilePlayModeTests.HealthPixels, XML отсутствует. Попытка через auto при неготовом Editor повторила тот же непригодный backend. Оба результата не PASS. После crash task-owned dangling backup/lock были сохранены в TestResults/CrashBackups, отсутствие владельца lock проверено lsof; тестовая InitTestScene удалена только в task checkout. Финальный gate — полный graphical Metal PlayMode, rendering tests не исключены. Crash log сохранён с gzip.

CLI initial advisory AGENT_PLUGIN_AVAILABLE не требовал установки: host Unity skills уже доступны. Новые plugins/packages, telemetry, phone connection, iOS export/signing/release не выполнялись. Physical iPhone Deferred до53/54/55.
