# VFX.1b — actual Play Mode media

Исходная production camera, 1600×900. PNG из continuous video копируются побайтно из composited GameView frames; manifest связывает каждый с frame/time/stage/event. Ни camera crop, ни перерисовка PNG не применяются.

| Семейство | Launch | Intermediate | Impact |
|---|---|---|---|
| TIDE | [PNG](playmode/tide-launch.png) | [PNG](playmode/tide-flight.png) | [PNG](playmode/tide-impact.png) |
| GALE | [PNG](playmode/gale-launch.png) | [PNG](playmode/gale-flight.png) | [PNG](playmode/gale-impact.png) |
| STONE | [PNG](playmode/stone-launch.png) | [PNG](playmode/stone-flight.png) | [PNG](playmode/stone-impact.png) |

[Pause](playmode/video-pause.png), [Continue clean](playmode/video-continue.png), [Sell](playmode/video-sold.png), [Wet](playmode/video-status-wet.png), [Gust](playmode/video-status-gust.png), [Crack](playmode/video-status-crack.png) — production Update, timeScale1/captureFramerate0. Запись начинается с earned file Continue8 из полного публичного replay GALE→STONE→TIDE; preparation replay выполняется до записи и отдельно подписан, во время видео clock не подменяется.

Дополнительные кадры — earned FLAME→TIDE→GALE/public controllers с явно ускоренным test clock ≤120ticks/render. Это отдельное evidence, не realtime cadence/performance: [dense](playmode/earned-dense-combat.png), [boss/HP/status/inspector](playmode/earned-boss-inspect-composite.png), [ability](playmode/earned-boss-ability-impact.png), [death](playmode/earned-boss-death.png), [Victory8](playmode/earned-victory8.png), [unchanged FLAME L3](playmode/L3-flight.png).

Synthetic, отдельно от earned/native: [mixed4 launch](playmode/synthetic-mixed4-launch.png), [mixed4 flight](playmode/synthetic-mixed4-flight.png), [all4 statuses / two-slot priority / inspector](playmode/synthetic-all4-priority-inspector.png), [четыре формы на отдельных owners](playmode/synthetic-four-status-cues.png). Все4 одновременно недоступны в одном native run (максимум3открытые стихии); fixture явно подписан, не подменяет earned progress или balance acceptance.
