# AUDIO.1 — результаты Editor для #49

[Editor-видео со звуком](editor-game-audio.mp4) · [Каталог SFX, 28 с](cue-catalog.m4a) · [Таймкоды каталога](cue-times.json) · [Audio в MainMenu](main-menu-audio.png) · [Audio в Pause](pause-audio.png)

## VERIFIED

В штатных сценах подключены музыка, шесть пар выстрел/попадание, реакции, Печать, Страж, UI и результаты. Настройки Master/Music/SFX и Mute доступны из MainMenu и Pause. Привязки событий и ограничения голосов описаны в [спецификации](../../AUDIO1.md). Ветка начата от `b8875273ccd71ddb573aefd2a3195a3e4253ecd7`. Прогоны Unity проверили runtime и ассеты `fcffe6c5244d76010b7daea8d44cb32b6269765d`. Последующие изменения пакета evidence касаются записи, проверок файлов и документации.

| Проверка | Результат |
| --- | --- |
| `./Scripts/run-static-checks.sh` | [38/38, version, .meta, GUID, packages, assembly definitions](reports/static-command.txt) |
| `./Scripts/run-edit-mode-tests.sh` | [1310/1310, полный прогон без фильтра](reports/edit-mode.json.gz) |
| `./Scripts/run-play-mode-tests.sh` | [111/111, полный прогон без фильтра](reports/play-mode.json.gz) |
| `git diff --check` | [PASS](reports/diff-command.txt) |
| Компиляция, сохранённые Battle и MainMenu | [Без ошибок компиляции и ссылок](reports/saved-scene-validator.json) |
| Оба прогона штатного сборщика | Каноническая повторяемость в полном EditMode |
| Исходники и ассеты | [50 побайтно воспроизводимых WAV](reports/source-roundtrip.json) · [GUID, частоты, пики, границы циклов](reports/source-audit.json) · [4 отрицательных контроля аудита](reports/editor-audit.json) |

В полном финальном прогоне 0 failed, skipped и inconclusive. Первый EditMode-прогон выявил устаревшее ожидание верхнего слоя Pause. Первый PlayMode-прогон выявил, что под модалкой следовало явно отключать raycasts. Первый прогон записи остановился при запуске stress fixture до обновления UI. Ошибки и исходные результаты сохранены. [Первый EditMode](reports/initial-edit-mode.json.gz) · [Первый PlayMode](reports/initial-play-mode.json.gz) · [Первый захват](reports/initial-capture-console.json)

### Запись игрового микса

Unity Editor 6000.3.28f1, Metal, текущий Editor quality PC. Запись содержит 2549 кадров GameView 960×444, полученных из профиля 2556×1179. `AudioRenderer` снимает мастер-выход Unity в том же кадре при фиксированных 24 кадрах/с. FFmpeg кодирует GameView в H.264 и снятый PCM в AAC stereo, 48 кГц. Источник звука целиком игровой. Это внутренний цифровой захват, до устройства вывода и системной громкости macOS. Частота кадров записи ограничивает разрешение видеотаймкодов одним кадром, аудио имеет собственный DSP-буфер 1024 samples. [Unity AudioRenderer](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AudioRenderer.Render.html) · [Manifest](reports/manifest.json) · [Консоль](reports/console.json) · [Команда FFmpeg](reports/encode-command.json)

| Время | Что записано |
| --- | --- |
| 00:00–00:28 | Музыка меню и естественный стык цикла около 00:27. |
| 00:28–00:32 | MainMenu Audio. Music 43%, Mute, восстановление того же уровня. |
| 00:33–00:39 | New Game, выбор Flame, принятый Build, начало волны, атака, выбор цели, Cancel и принятый Rift Seal с попаданием. |
| 00:39–00:59 | Pause Audio, preview регуляторов, Resume, стык боевого цикла около 00:56, MainMenu и файловый Continue. |
| 01:00–01:10 | Синтетический плотный бой, шесть стихий, Steam burst и Storm chain/hop. В кадре работает production consumer. |
| 01:10–01:40 | Continue из заработанного checkpoint волны 8, штатный бой, угроза и смерть Стража, Victory. |
| 01:40–01:46 | Синтетический probe. Быстрый Страж доходит до базы. Зафиксированы отдельные BossLeak и Defeat. |

AAC декодируется. Длительность аудио 106,165 с, пик −17,73 dBFS. Исходный захват мастер-выхода −17,80 dBFS. Проверенное окно Mute содержит нулевые samples. После выключения Mute окно имеет −34,37 dBFS RMS. На границах исходных музыкальных WAV перепад 87 и 46 единиц PCM16 для меню и боя соответственно. Прогон зафиксировал оба wrap и 5 занятых SFX voices в обычном бою, 12 в плотном, 11 в заработанной волне 8, при лимите 14. В каждом значении учтены и слоты с запланированным impact. PlayMode отдельно заполняет все 14 слотов и проверяет вытеснение и сброс. [Машиночитаемые измерения](reports/editor-audit.json)

### Форматы, бюджет, состояние рабочего checkout

Два stereo WAV 48 кГц импортируются в Vorbis Streaming, quality 0,78. 48 mono SFX импортируются в ADPCM Decompress On Load, 32 кГц. При полном float PCM весь SFX набор занимает до 2 844 416 байт. Эта оценка использует реальные imported samples и не включает потоковые буферы. Unity создаёт 15 AudioSource в активной сцене. Одновременно воспроизводится одна из двух музыкальных дорожек. Импортированные артефакты Editor занимают 2 683 460 байт, включая метаданные кеша. На SFX приходится 1 495 780 байт, BattleMusic — 542 144, MenuMusic — 645 536. Для каждого клипа приложены размер, длительность и GUID. Происхождение и воспроизведение исходников описаны отдельно. [Импорт Unity](reports/unity-import-sizes.json) · [Размеры кеша](reports/unity-artifact-sizes.json) · [Исходники и provenance](../../../ArtSource/Audio/README.md)

Оба личных checkpoint и четыре локальных файла рендера сверены побайтно. Прогоны восстановили 889 проверенных входов, предыдущие отчёты в `TestResults` и обновили ранее открытую Battle в том же Editor. Unity Test Runner обновил служебный отчёт в Application Support. Байты plist PlayerPrefs изменились, аудиоключи после работы отсутствуют, исходные байты этого plist не подтверждены. [Аудит входов и восстановления](reports/byte-audit.json) · [Аудит локального состояния](reports/user-state-audit.json) · [Состояние Editor](reports/after.json)

### Воспроизведение

```bash
python3 ArtSource/Review/run_ui1_gates.py <новое-имя-прогона>
python3 ArtSource/Audio/run_editor_audio.py
python3 ArtSource/Audio/make_catalog_preview.py TestResults/audio1/preview-new
python3 ArtSource/Audio/audit_audio1.py --video Docs/review/AUDIO.1/editor-game-audio.mp4 --manifest Docs/review/AUDIO.1/reports/manifest.json
```

Повтор записи использует свободный каталог `TestResults/audio1/capture` и отдельное сохранение. [Команды Unity Editor](reports/commands.txt)

## NOT VERIFIED / DEFERRED

Независимое прослушивание исходных клипов, стыков и микса ожидает внешнего review. Проверки численного сигнала подтверждают наличие, уровни и отсутствие цифрового клиппинга. Приёмка художественного звука и читаемости сочетаний требует прослушивания. Физический iPhone, нативный lifecycle, слышимость, память и thermal входят в prerelease QA #53/#54/#55.
