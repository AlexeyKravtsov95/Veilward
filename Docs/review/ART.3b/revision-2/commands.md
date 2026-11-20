# Команды R2

Cwd: isolated `art3b-tide`, branch `codex/art3b-tide-levels`. Source696dc8ae3cc7799bea586c57d44749163300b06c. Все Unity calls передают `--caller plugin --skill unity-cli --project-path "$PWD"`; original main Editor не используется. Окружение и warmup — validation.md, предыдущие failure reports сохранены.

```sh
# Новые targeted43Edit/3Play/capture2 — reports/art3b-r2-*.json
./Scripts/run-static-checks.sh
unity command 'script eval' --caller plugin --skill unity-cli --project-path "$PWD" --format json -- --code 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Game/Scenes/Battle.unity");return "R2 direct Battle ready";'
unity command 'editor set_auto_tick' --caller plugin --skill unity-cli --project-path "$PWD" --format json
unity command 'editor.playmode start' --caller plugin --skill unity-cli --project-path "$PWD" --format json
unity status --until-ready --project-path "$PWD" --format json
unity command 'editor.playmode set_run_in_background' --caller plugin --skill unity-cli --project-path "$PWD" --format json
unity command 'script run' --caller plugin --skill unity-cli --project-path "$PWD" --format json -- --file ArtSource/Review/CaptureArt3b.cs --entry CaptureArt3b.Capture
# Для actual GameView выбрать fixed1600x900 в текущей PlayMode группе (фактическийRT проверен отдельно).
unity command 'script eval' --caller plugin --skill unity-cli --project-path "$PWD" --format json -- --code 'var f=new Veilward.Tests.PlayMode.TideModelPlayModeTests();f.GetType().GetMethod("ConfigureGameView",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(f,null);return "fixed GameView selected";'
unity command 'script run' --caller plugin --skill unity-cli --project-path "$PWD" --format json -- --file ArtSource/Review/RecordArt3b.cs --entry Art3bEditorRecording.BeginRecording
# Дождаться video-completion.success=true; не вызывать Source edit/recompile/close во время записи.
python3 ArtSource/Review/encode_art3b_video.py
# После успешной записи удалить только completed task recorder overlay, загрузить isolated preview:
unity command 'script run' --caller plugin --skill unity-cli --project-path "$PWD" --format json -- --file ArtSource/Review/CaptureArt3b.cs --entry CaptureArt3b.LoadPreview
unity command 'script run' --caller plugin --skill unity-cli --project-path "$PWD" --format json -- --file ArtSource/Review/CaptureArt3b.cs --entry CaptureArt3b.SelectPreview
unity command 'capture game_view' --caller plugin --skill unity-cli --project-path "$PWD" --format json -- --source screen --width 1600 --height 900 --output-path Docs/review/ART.3b/revision-2/playmode/catalog-build-preview.png
unity command 'editor.playmode stop' --caller plugin --skill unity-cli --project-path "$PWD" --format json
unity command 'script eval' --caller plugin --skill unity-cli --project-path "$PWD" --format json -- --code 'Veilward.Editor.BattleSceneBuilder.Build();return "R2 existing builder warmup completed";'
./Scripts/run-edit-mode-tests.sh
./Scripts/run-play-mode-tests.sh
unity recompile --project-path "$PWD" --format json
# Сохранить generated asset audit/patch и вернуть точные tracked non-C#Assets изHEAD.
python3 ArtSource/Blender/ART3/TIDE/verify_reexport.py
# После полного successful before/after вернуть canonical FBX timestamp bytes и снова snapshot/readback:
git restore --source=HEAD -- Assets/_Game/Art/Models/ART3/TIDE/Tower_TideBellL1.fbx Assets/_Game/Art/Models/ART3/TIDE/Tower_TideBellL2.fbx Assets/_Game/Art/Models/ART3/TIDE/Tower_TideBellL3.fbx
git diff --check
python3 Docs/review/ART.3b/revision-2/verify_delivery.py
```

Capture driver использует установленный Pipeline0.10.0-exp.1 composited GameView RT и PNGencoder, а не `ScreenCapture`/`Screen.width` из Editor callback. `art3b-r2-target-debug.json` подтверждает actual RT1600×900, несмотря на другой Editor backbuffer. Ранний0frame dimension failure сохранён; preflight теперь до store/session, abort останавливает обе coroutine. Размер выбирался после входа вPlay; scoped reviewer сверил backend и source pixels. Window maximise влияет только на собственный task Editor и не меняет camera/scale. Capture overhead не benchmark.

Negative controls выполняются в отдельном task-owned detached `art3b-controls`: plain read-only verifier → добавить untracked file → ожидатьFAIL → удалить; затем committed foreign C# → ожидать inventoryFAIL → switch к исходному cleanHEAD; затем committed append к R2PNG → ожидать artifactSHAFAIL → возврат cleanHEAD и положительный verifier. Точные argument arrays, exitcodes и outputs — reports/delivery-controls.json после фактического run. В controls Editor не запускался, source ветки/чужие worktrees не менялись.
