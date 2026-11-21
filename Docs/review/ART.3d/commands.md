# Команды и окружение ART3d

Task worktree `.tools/worktrees/art3d-stone`, ветка `codex/art3d-stone-levels`, база main d2df69de476151823d0a6b727ec90b243cbf97ac (PR81). TestedSource4c40545a91c46bd000d2335c31af132ba45ded62. Все Editor calls явно адресованы только task checkout через --project-path; чужие worktrees/Editors не изменялись. Команды ниже выполнялись из task root; `$PWD` обозначает его полный путь.

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Blender/ART3/STONE/author_stone.py
for level in 1 2 3; do
  /Applications/Blender.app/Contents/MacOS/Blender --background "ArtSource/Blender/ART3/STONE/Tower_StoneSealL${level}.blend" --python-exit-code 1 --python ArtSource/Blender/ART3/STONE/export_stone.py
done
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Blender/ART3/STONE/check_geometry.py
unity command script eval --caller plugin --skill unity-cli --project-path "$PWD" --code 'Veilward.Editor.StoneModelBuilder.Build();'
PYTHONDONTWRITEBYTECODE=1 ./Scripts/run-edit-mode-tests.sh
PYTHONDONTWRITEBYTECODE=1 ./Scripts/run-play-mode-tests.sh
PYTHONDONTWRITEBYTECODE=1 ./Scripts/run-static-checks.sh
git diff --check
PYTHONDONTWRITEBYTECODE=1 python3 ArtSource/Blender/ART3/STONE/verify_reexport.py
unity command script run --caller plugin --skill unity-cli --project-path "$PWD" --file ArtSource/Review/CaptureArt3d.cs --entry CaptureArt3d.Capture
unity command script run --caller plugin --skill unity-cli --project-path "$PWD" --file ArtSource/Review/CaptureArt3d.cs --entry CaptureArt3d.LoadPreview
unity command script run --caller plugin --skill unity-cli --project-path "$PWD" --file ArtSource/Review/CaptureArt3d.cs --entry CaptureArt3d.SelectPreview
unity command capture game_view --caller plugin --skill unity-cli --project-path "$PWD" --source screen --width 1600 --height 900 --output-path Docs/review/ART.3d/playmode/catalog-build-preview.png
unity command script run --caller plugin --skill unity-cli --project-path "$PWD" --file ArtSource/Review/CaptureArt3d.cs --entry CaptureArt3d.ConfigureView
unity command script run --caller plugin --skill unity-cli --project-path "$PWD" --file ArtSource/Review/RecordArt3d.cs --entry Art3dEditorRecording.BeginRecording
PYTHONDONTWRITEBYTECODE=1 python3 ArtSource/Review/encode_art3d_video.py
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.3d/verify_delivery.py
```

Natural capture preflight: System.Environment.SetEnvironmentVariable("ART3D_CAPTURE","1") только в task Editor, затем `test run --mode playmode --filter StoneModelPlayModeTests --async-tests true`, результат через `test status`. Final full suites без filter и без capture flag. Synthetic capture: direct saved Battle → auto_tick/RunInBackground → PlayMode → job wait Time.frameCount changed; helper проверяет empty direct Battle. Assigned positions/HP/copy actual Crack snapshot подписаны synthetic, live Domain/DTO не мутируются. Preview LoadPreview→SelectPreview использует unchanged earnedL3 isolated store, no purchase. GameViewConfigureView1600×900 возвращает previousIndex35, он восстановлен после записи. Recorder использует установленный composited Pipeline RT/PNG encoder. Видео не перерисовано и не ускорено; exact stage PNG picks/real PTS/source hashes в reports.

Окружение: Unity6000.3.28f1/8f082d2fe476, URP17.3.0, Pipeline0.10.0-exp.1, UnityCLI1.0.1, Blender5.3.0/02f0899d78e3, macOS27.0.1 ARM64, BuildTargetiOS/QualityMobile. Battle camera(8,11,8), ortho8.5, root scale1, production light1.2; PNG/video1600×900. Editor session использует auto_tick16ms/RunInBackground, PlayerSettings не меняются. ProjectVersion/packages/lock прежние. Phone/device binaries/iOS export/signing/TestFlight не запускались.

## Неуспешные/прерванные попытки

Initial red1/1 FAIL ожидаемо доказывает отсутствие stoneLevels до реализации: stone-red.json. После реализации focused Edit21/21. Первый Unity open дочерний процесс завершился до подключения; следующий open с --wait работал. На диске было1.5GiB, затем0: shell выдавал No space left on device, первый Play async run не получил завершённого отчёта и НЕ PASS. Созданный taskEditor остановлен; удалялся только его ignored BurstCache. CLI close также вернул PROJECTS_REGISTRY_UNSUPPORTED, поэтому использован scoped EditorApplication.Exit, затем TERM только task PID. После явного сообщения пользователя об освобождении25GiB работа продолжена в том же worktree; чужие файлы не удалялись.

Первый actual natural Play capture2/2 выявил edge-on модель: неправильная source diagonal orientation. Пять initial PNG сохранены attempt-1-edge-on и не выдаются за final visual PASS. Исправлена только STONE source projection, reexport3/3, добавлен imported seal-facing regression. Один async retry после import/recompile оставался running без completed report и был отменён; running snapshots сохранены. После завершённого recompile новая targeted запись2/2 — oriented-capture-final.json, final PNG в playmode. Final L1/L2/L3/HP/status/comparison проверены отдельно.

Существующий compile warning CS0219 BossGreedySearchTests.cs:30 (unused mask) виден в compile-oriented.json, не введён этим PR и не скрыт. Final up_to_date report не отменяет эту историю. Source bytecode __pycache__, возникший от importlib до final source commit, удалён; runtime/Unity source от этого не менялся.

Unity сериализует материалы/URP/prefabs при tests/build. Только task-generated изменения сохранены в gzip patches и hash audit edit-restored/play-restored, затем bytes восстановлены к tested source. Runtime delta после полного Edit/Play не вносится. Canonical roundtrip readback повторён; старые ART1/FLAME/TIDE/GALE/history evidence не переписываются. Final source inventory/protected baseline/metadata/tracked artifact hashes проверяет read-only verifier в отдельном task-owned controls checkout.

После полных suites retained error buffer сохранён полностью:114negative-path errors, текущий Console25errors/0warnings. Agent не вызывал console clear. Scoped restart (старый taskPID30866 завершён TERM после Exit) создаёт новую сессию, final-editor-ready/compile/console-final подтверждают0errors/0warnings; это отдельный startup audit, не новый полный suite.
