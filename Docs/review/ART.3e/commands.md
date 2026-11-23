# Команды и окружение ART3e

Текущий существующий checkout `/Users/alexeykravtsov/Desktop/workspace/Veilward`, ветка `codex/art3e-steam-levels`, база main62d4dbd (PR83), TestedSource9654133029902fb2541ee833d6ef80710755a5bb. Новые worktree/клоны/полные копии не создавались; Library/TestResults переиспользуются. Другой открытый Editor на art3d-stone не изменялся. Все Unity calls явно адресованы этому project-path и выполняются последовательно одним владельцем. `$PWD` ниже — текущий root.

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Blender/ART3/STEAM/author_steam.py
for level in 1 2 3; do
  /Applications/Blender.app/Contents/MacOS/Blender --background "ArtSource/Blender/ART3/STEAM/Tower_SteamFoxL${level}.blend" --python-exit-code 1 --python ArtSource/Blender/ART3/STEAM/export_steam.py
done
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Blender/ART3/STEAM/check_geometry.py
unity command script eval --caller plugin --skill unity-cli --project-path "$PWD" --code 'Veilward.Editor.SteamModelBuilder.Build();'
PYTHONDONTWRITEBYTECODE=1 python3 ArtSource/Blender/ART3/STEAM/verify_reexport.py
unity command script run --caller plugin --skill unity-cli --project-path "$PWD" --timeout 300 --file ArtSource/Review/SearchSteamTiming.cs --entry SearchSteamTiming.Search
unity command test run --caller plugin --skill unity-cli --project-path "$PWD" --mode playmode --filter SteamModelPlayModeTests --async-tests true
unity command test status --caller plugin --skill unity-cli --project-path "$PWD"
unity command script run --caller plugin --skill unity-cli --project-path "$PWD" --file ArtSource/Review/CaptureArt3e.cs --entry CaptureArt3e.Capture
unity command script run --caller plugin --skill unity-cli --project-path "$PWD" --file ArtSource/Review/CaptureArt3e.cs --entry CaptureArt3e.ConfigureView
unity command script run --caller plugin --skill unity-cli --project-path "$PWD" --file ArtSource/Review/RecordArt3e.cs --entry Art3eEditorRecording.BeginRecording
PYTHONDONTWRITEBYTECODE=1 python3 ArtSource/Review/encode_art3e_video.py
PYTHONDONTWRITEBYTECODE=1 ./Scripts/run-static-checks.sh
PYTHONDONTWRITEBYTECODE=1 ./Scripts/run-edit-mode-tests.sh
PYTHONDONTWRITEBYTECODE=1 ./Scripts/run-play-mode-tests.sh
git diff --check
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.3e/verify_controls.py
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.3e/verify_delivery.py
```

Play Mode preflight: installed CLI1.0.1 (self-update check current), editor set_auto_tick16ms, session-only runInBackground override, explicit project-path, status --until-ready после domain reload, job wait на изменении Time.frameCount. GameView1600×900 (previousIndex18, evidence36), индекс18 восстановлен; PlayerSettings/quality assets не изменялись. Runtime probes зафиксировали qualityMobile/Mobile_RPAsset; остановленный Editor показывает PC — это отдельный edit-state audit, не runtime profile. Unity6000.3.28f1/8f082d2fe476, URP17.3.0, Pipeline0.10.0-exp.1, Blender5.3.0/02f0899d78e3, Python3.12/macOS27.0.1 ARM64. iOS active target; player/device builds отсутствуют.

Natural capture: ART3E_CAPTURE=1 только в task Editor на targeted2/2, затем null; fresh New Game/FLAME/public commands и file/menu Continue. Saved L1/2/3 принадлежат Preparation4/7/8 одного действительно заработанного run; repeated Continue не пишет bytes. Видео replay isolated VideoStore не fresh run: origin/readback SHA совпадают. Prefix test clock30Hz+штатные lifetimes; Combat8 production Update. Изменения Domain/Gold/HP/DTO отсутствуют. Composited Pipeline RT/PNG encoder, real captured PTS, exact stage PNG picks; sparse timing/overhead не benchmark.

## Непрошедшие и незавершённые попытки

Red1/1 FAIL ожидаемо подтвердил отсутствие steamLevels до реализации. Первая source geometry проверка нашла вырожденные triangles в sweep воротника: tangent совпал с reference axis; исправлен author, повторены3sources/export. Следующая проверка выявила оставшееся STONE semantic group name в адаптированном checker; исправлен только checker, final source3/3 PASS. Первые synthetic helper calls: transient EDITOR_NOT_RUNNING во время domain reload и недоступный internal CreateHistoricalAllOpenSimulation из dynamic assembly; после status-ready/public factory получен actual probe PASS. Helpers compile typos/JSON dynamic-type parse и source group failures сохранены в attempts.tar.gz, не выдаются за PASS.

Bounded strategy experiments: первоначальные F/T/GALE/one-tower варианты не дали495; best446/451/477/489/494 — не playable acceptance. Найден correlation bug между parity upgrade и mode, добавлены независимые layout/timing policies/hold ability и cooldown-aware snapshots. Финальная layout16 дала498; каждый branch восстанавливает exact earned safe checkpoint, не редактирует DTO; затем fresh integration replay повторил результат. Исторические failed helper versions и raw outcomes в attempts.tar.gz; текущий SearchSteamTiming читает committed winning settings. Один расширенный search превысил default60s CLI timeout; Editor продолжил CPU calculation, полный response потерян, run НЕ PASS. Дальнейшие calls используют timeout300 и progress/final files. Невозможность прочих стратегий не доказана.

Полные suites/builder могут сериализовать материалы/сцены/prefabs. Generated-only patch и byte audit сохраняют delta, затем восстанавливаются только изменения этих runs; исходные четыре dirty файла сохраняются. Их исходные bytes дополнительно проверены по Git blob prefixes из initial status/diff. Из-за generated serialization первый full Play был повторён с восстановленными inputs. Оба preliminary Play85/85 и Edit1195/1195 положительны; финальный Source965 меняет только same-event assertion и получает отдельные final runs.

После этой assertion правки CLI recompile завершился RECOMPILE_TIMEOUT120; status сообщил STATUS_ALL_UNREACHABLE. Compiler errors в последнем логе не найдены. Gates scripts корректно отказались запускать второй Editor при live descriptor; пустые результаты/cp errors НЕ PASS. Нативный UI read также недоступен (cgWindowNotFound). Выполнен scoped TERM только созданного task Editor68436 и reopen того же checkout/Library; чужой Editor32445 не затронут. Final compile/Console/suites после восстановления фиксируются отдельно.

Автоматический approval review отклонил одну shell-команду из-за `rm -f` для временного bytecode cache; команда целиком не выполнилась. Вместо неё syntax проверен ast.parse без создания/удаления cache. Новых approval обходов или удаления чужих файлов нет.

Для `git diff --check` у экспортированного `video-inspector.txt` удалена только завершающая пустая строка; содержимое Inspect не менялось.

## Разрешённый clean-checkout verifier

После явного ответа владельца «Да, временно сохранить и восстановить» остановлен только task Editor74060 (identity проверена по executable и project path). Ровно четыре carry-in файла сохранены вне проекта в `~/.codex/backups/veilward-art3e-carry-in-s843u3s1/`, directory mode0700. Имена, original/Git SHA256, head, UTC и stdout — в `reports/delivery-approved.json`.

Операция выполнялась одним Python process с try/finally: проверить исходные bytes и ровно четыре tracked изменения → записать `git show HEAD:<path>` bytes этих четырёх файлов → убедиться в clean tracked checkout → `PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.3e/verify_delivery.py` → проверить отсутствие новых tracked изменений → finally восстановить backup bytes и сверить все четыре original SHA256 и original change set. Exit0, PASS3511/3008/96. Первая остановка по identity guard не послала сигнал: CLI использовал lowercase `-projectpath`; после проверки flag case без вывода process arguments остановлен верный task Editor. Source/assets вне разрешённых четырёх файлов не менялись. Unity/tests/media в этом окне не запускались.

После добавления receipt/evidence-only commit db21387 операция повторена для обновлённых hashes: root и независимый reviewer отдельно запустили неизменённый полный verifier при clean tracked Git bytes — PASS3511/3008/97. После независимой проверки originals4/4 восстановлены и проверены по SHA256/change set. Финальный evidence-only commit записывает это заключение; full verifier повторяется на нём без изменения собственных inputs, а raw result остаётся в ignored TestResults, чтобы не создавать цикл самохеширования evidence.
