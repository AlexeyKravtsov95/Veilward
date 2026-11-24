# UI.1 — отчёт по #48

Пакет проверяет штатные MainMenu и Battle в Unity Editor 6000.3.28f1 на macOS arm64. Игровой интерфейс использует имеющийся uGUI и действующие контроллеры команд. Полный сборщик сохраняет обе сцены, а существующий repeatability gate сравнивает канонические YAML после двух прогонов.

## VERIFIED

[Галерея оригинальных PNG](gallery.md) · [Непрерывное видео](production-flow.mp4) · [Машиночитаемый аудит](reports/audit.json) · [Независимое review](review.md)

Проверенный production source — `2b5031b3bdcde4245c7a041a9787dc54bc928a2d` от base `38ca8429f9579ffa3d862944ebf3ece9bc76ee58`. Использованы Unity 6000.3.28f1, Unity CLI 1.0.1, Metal и профиль Mobile.

| Проверка | Результат |
|---|---|
| `./Scripts/run-static-checks.sh` | [38/38, version, packages, .meta, GUID и asmdef](reports/static-command.txt) |
| `./Scripts/run-edit-mode-tests.sh` | [1304/1304 уникальных cases](reports/edit-mode.json.gz) |
| `./Scripts/run-play-mode-tests.sh` | [105/105 уникальных cases](reports/play-mode.json.gz) |
| `git diff --check` | [PASS](reports/diff-command.txt) |
| Editor и ссылки сцен | [Компиляция](reports/compile.json) · [Полный сборщик и валидаторы](reports/builder.json) |

Failed, skipped и inconclusive равны нулю в обоих полных suite. Первый прогон выявил старые текстовые ожидания, обрезку строк карточки и отсутствующую ссылку закрытия в сохранённой сцене. Эти результаты сохранены отдельно. [Первый EditMode](reports/first-edit-mode.json.gz) · [Первый PlayMode](reports/first-play-mode.json.gz)

Побайтно сверены 768 входов Assets, Packages и ProjectSettings. После работы Unity восстановлены оригинальные байты, предыдущие отчёты и импорт в открытом Editor. В записи зафиксирована одна ожидаемая ошибка изолированного сохранения. [Аудит восстановления](reports/byte-audit.json) · [Editor refresh](reports/restored-refresh.json)

| Профиль | Размер GameView | Логический размер | Safe area в пикселях, x/y/w/h | Минимальная цель |
|---|---:|---:|---:|---:|
| Низкий телефон, DPR 2 | 1136×640 | 568×320 pt | 0 / 0 / 1136 / 640 | 46,05 pt |
| Широкий телефон, DPR 3 | 2556×1179 | 852×393 pt | 132 / 63 / 2292 / 1116 | 58,95 pt |
| Планшет 4:3, DPR 2 | 2048×1536 | 1024×768 pt | 0 / 40 / 2048 / 1496 | 83,03 pt |

Размеры GameView заданы как реальные raster targets. Отступы имитированы вызовом штатного safe-area layout на каждом профиле. Измерены экранные границы действующих кнопок и панелей после Canvas layout. Преобразование Scale With Screen Size / Expand 740×400 приведено в [спецификации](../../UI1_PRODUCTION_UI.md). Масштаб и transform боевой камеры сверены с base.

Пакет содержит 46 PNG. Видео содержит 463 последовательно снятых кадра штатного Update, около 20,7 с. Запись управляла кнопками и публичными командами в существующих сценах. Кадры GameView для видео уменьшены до 1280×590, PNG сохранены в исходном размере. На 2,30–3,22 с видны каталог, покупка и цена Upgrade. На 3,64–4,51 с видны недопустимая цель, Cancel, допустимая цель и применение Rift Seal. На 16,85–20,67 с видны Upgrade, Sell, Pause, Resume, переход в меню и Continue к точке подготовки волны 2.

Файловые сбои использовали отдельный тестовый каталог. Defeat построен из детерминированного забега без башен. Victory получена через штатный Update волны 8 после Continue с точки, заработанной обычным v2 replay. Плотный бой и подробный инспектор сняты в изолированном all-open fixture VFX.1d с повышенным HP Shade. В нём показаны семь активных врагов, HP босса и фактический HP loss по двум реакциям через штатный инспектор.

Авторский редактируемый источник эмблемы и пиктограмм находится в [VeilwardUiGlyph.cs](../../../Assets/_Game/Scripts/Presentation/VeilwardUiGlyph.cs). Композиция и настройки компонентов находятся в [BattlePlayerUiBuilder.cs](../../../Assets/_Game/Scripts/Editor/BattlePlayerUiBuilder.cs). Отображаемый шрифт LegacyRuntime поставляется с Unity. Пакет фиксирует реальный asset path шрифта, зависимости сцен и метаданные импорта.

## Воспроизведение

```bash
python3 ArtSource/Review/run_ui1_gates.py <новое-имя-прогона>
python3 ArtSource/Review/run_ui1_editor.py Wide Small Tablet Final
python3 ArtSource/Review/package_ui1.py --gates Docs/review/UI.1/reports --base 38ca8429f9579ffa3d862944ebf3ece9bc76ee58
```

Последняя команда проверяет поставку read-only. Она сопоставляет source и PNG с SHA-256, сверяет bounds, gaps и высоту видимого текста, восстанавливает семантику таймлайна через FFprobe и запускает четыре отрицательных контроля. Флаг `--package` упаковывает новые исходные кадры вместо чтения поставки. [Команды записи](reports/media-commands.txt)

## NOT VERIFIED / DEFERRED

Physical iPhone и нативные safe area, touch и lifecycle входят в prerelease QA #53/#54/#55. G2, выпуск и полная локализация #50 идут отдельными задачами.
