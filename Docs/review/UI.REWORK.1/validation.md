# Проверка UI.REWORK.1

## VERIFIED

Unity 6000.3.28f1 (8f082d2fe476), macOS arm64, Metal, активная платформа редактора iOS. Код и сохранённые сцены проверялись в существующем checkout. Проверенная ревизия реализации `decdd8a570e93f4ea3185179ba7b5c72461a2978`. Конкретные входы каждой сцены, скриптов и импортов перечислены в [SHA-256](reports/inputs.sha256).

- Сборка C# завершена с 0 ошибками. В Editor suite остался ранее существовавший CS0219 в `BossGreedySearchTests.cs`. [Компиляция](reports/compile.json)
- Полные прогоны дали EditMode 1315/1315 и PlayMode 109/109. Failed, skipped и inconclusive равны 0. [EditMode](reports/EditMode.json) · [PlayMode](reports/PlayMode.json)
- Static checks 38/38 и `git diff --check`. Скрипт проверяет версию проекта, Packages, GUID, .meta и assembly definitions. [Static](reports/static.log)
- Регрессии проверяют authored sprite UV при смене стихии и Lock, первый результат EventSystem raycast на краях кнопок, закрытие поля модальным фоном, один расход и возврат за команду, сохранение после Retry. Проверка повреждённого слота подтверждает состояние Retry и Continue. [REGRESSION](../../REGRESSION.md)
- Реальные открытые группы контролов проверены после layout rebuild. Минимальная цель 60×60 единиц Canvas даёт 46,05×46,05 logical pt на узком профиле. Применён прежний CanvasScaler 740×400/Expand. Профили повторены в GameView, modern и iPad используют заданные тестовые inset. [Кадры](README.md)
- В Battle совпадают 383 сериализованные записи, в MainMenu 14. Из общих записей изменились только ссылки контроллеров и корней сцен на uGUI. Неразрешённых локальных fileID нет. [Проверка YAML](reports/scene-scope.txt)
- Из обычного earned v2 checkpoint волна 8 завершилась Victory, 16 HP Сердца и 64 побеждённых врага. Применения Печати прошли через Preview и Confirm. В story покупка FLAME за 70 gold и продажа за 49 gold дали 129 gold после Continue. [Индекс кадров и хеши файлов](reports/media.json)

### Импорт и ресурсы

Импортированы 24 отдельных спрайта из одной текстуры 1024×512 и один отдельный фон меню. Восемь поверхностей имеют 9-slice. В runtime используется стандартный материал uGUI. Для нового UI нет дополнительных Render Features. Повторный прогон `render.py` дал байтово идентичные PNG и JPEG. Файлы атласа, фона и шрифта занимают 304 892, 529 413 и 732 920 байт. RuntimeMemorySizeLong в редакторе показал 4 195 264 байта для атласа и 809 456 байт для фона. После прогрева 1000 переключений Tide ↔ Lock с перестройкой mesh дали 0 новых managed bytes на измеренном потоке. [Import inventory](reports/imports.json)

### Визуальная проверка

Первый кадр выявил обрезанный заголовок из-за метрики декоративного шрифта. Повторный кадр показывает весь заголовок и отдельный контрастный статус слота. Второй проход уменьшил контраст фактуры и сместил ветку на угол бумаги. Play Mode кадры с устоявшимся tint показывают различимые состояния доступной и отключённой кнопок. Динамические числа и причины блокировки остаются текстом.

### Воспроизведение

```sh
./Scripts/run-static-checks.sh
git diff --check
unity recompile --project-path "$PWD" --timeout 90 --format json
unity command script eval --caller plugin --skill unity-cli --format json -- --code 'Veilward.Editor.BattleSceneBuilder.RebuildPlayerUi();'
python3.12 Docs/review/UI.REWORK.1/tools/run_edit_with_snapshots.py
unity command script eval --caller plugin --skill unity-cli --format json -- --code 'UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport);'
unity command test run --caller plugin --skill unity-cli --format json --timeout 600 -- --mode playmode --async-tests true
unity command test status --caller plugin --skill unity-cli --format json
unity command script eval --caller plugin --skill unity-cli --format json -- --file Docs/review/UI.REWORK.1/tools/inspect_imports.cs
python3.12 Docs/review/UI.REWORK.1/tools/verify_scene_scope.py
python3.12 Docs/review/UI.REWORK.1/tools/verify_media.py
```

Команда `VEILWARD/Rebuild Player UI` заменяет Canvas и привязки обеих сцен. Она сохраняет существующие объекты мира и контроллеры. Ранее существовавшие тесты полного генератора записывают сцены и общие ассеты во время EditMode. Обёртка сохраняет их байты перед прогоном, открывает Bootstrap и восстанавливает эти файлы после прогона. Локальные изменения четырёх файлов совпали с исходным SHA-256 diff.

[Драйвер записи](tools/UiReworkCaptureLocal.cs) временно помещался в `Assets/_Game/Scripts/Presentation/`. В активном Play Mode он запускается через `unity command script eval` вызовом `Veilward.Presentation.UiReworkCaptureLocal.Begin("story");`. Поддерживаются также `earned`, `profiles`, `defeat`, `stress` и `milestones`. Дождаться `complete.txt` каждого режима, затем остановить Play Mode и убрать временные `.cs` и `.meta`. PNG получены через `ScreenCapture.CaptureScreenshotAsTexture`. Видео получено через Unity AsyncGPUReadback и FFmpeg, H.264, 1600×900, 20 кадров/с, без аудио. Кнопки используют EventSystem RaycastAll и pointer events. Слоты временные. Для earned использован [заработанный ранее v2](../VFX.1d/reports/safe-file.json). Повреждённая запись, поражение с 1 HP, плотная волна и пороги после волн 3/6 используют явные fixtures.

## NOT VERIFIED / BLOCKED

Физический iPhone/iPad, touch latency, native производительность, аппрув владельцем итогового оформления и выпуск проверяются отдельно. Device QA [#53](https://github.com/AlexeyKravtsov95/Veilward/issues/53), [#54](https://github.com/AlexeyKravtsov95/Veilward/issues/54), [#55](https://github.com/AlexeyKravtsov95/Veilward/issues/55).
