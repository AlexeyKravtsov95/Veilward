# Editor Play Mode

Оригинальные 1600×900 PNG и два непрерывных видео исходного GameView. Unity 6000.3.28f1, production Update, timeScale 1, captureFramerate 0. Строки кадров сохраняют tick, фазу, ключи событий и SHA-256 оригинального PNG. MP4 учитывает реальное время захвата с переменным PTS.

- [Earned wave 8 с Pause/Resume, файловым Continue и Victory](earned.mp4)
- [Dense all-open с шестью семействами, Печатью и Стражем](dense-all-open.mp4)

| Кадр | Наблюдение |
|---|---|
| [Invalid preview](playmode/invalid-preview.png) | Красный крест и граница, Confirm недоступен |
| [Cancel](playmode/cancel.png) | Линии исчезли, Печать снова Ready |
| [Valid preview](playmode/valid-preview.png) | Янтарный ромб и существующий радиус |
| [Dense impact](playmode/dense-impact.png) | Искры фактических попаданий среди живого босса и реакций |
| [Earned impact](playmode/earned-impact.png) | Захваченные точки урона, включая lethal после удаления |
| [Появление Стража](playmode/boss-spawn.png) | Короткая лавандовая скоба у живого EnemyId |
| [Угроза Сердцу](playmode/boss-threat.png) | Направленный янтарный шеврон при пересечении порога |
| [Leak](playmode/boss-leak.png) | Коралловый шеврон на последнем участке пути и ART5 leak tail |
| [Death](playmode/boss-death.png) | Лавандовое рассеивание и ART5 death tail |
| [Плотный бой](playmode/dense-combat.png) | Активные атаки, реакция, два overhead slots и HP |
| [Выбор и range в бою](playmode/dense-range.png) | Дистанция выбранной башни, живой Страж и действующие атаки |
| [Инспектор Стража](playmode/dense-inspector.png) | HP, броня, cooldown и сохранённая причина реакции |
| [Четыре статуса в инспекторе](playmode/four-status-inspector.png) | Burn, Wet, Gust и Crack одного владельца |
| [Pause](playmode/earned-pause.png) | Зафиксированные tick, геометрия Печати и lifetime |
| [Файловый Continue](playmode/file-continue.png) | Preparation8 после прерывания с восстановленным Grid и чистыми пулами |

[Earned PTS, SHA и выбор кадров](reports/earned-video.json) · [Dense PTS, SHA и выбор кадров](reports/dense-all-open-video.json). Исходные полные последовательности PNG лежат в локальных папках захвата, а в PR входят все порядковые записи, хеши и растровая проверка каждого кадра MP4.
