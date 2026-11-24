# Editor Play Mode Gallery / VFX.1c

[Непрерывное видео](editor-flow.mp4) содержит 509 кадров 1600×900. PTS от 0 до 17.882 с, максимальное расхождение с timestamp исходного GameView 0.723 мс. Видео включает MainMenu → earned Continue → Combat8 → Pause/Resume → файловый Continue. [Телеметрия, PTS и SHA-256 исходных кадров](reports/video-proof.json).

## Realtime атаки

| Атака | Событие | Launch | Полёт | Impact |
|---|---|---|---|---|
| STEAM | tick 3320 · башня 7 · цель 63 · L1 | [357](playmode/realtime-steam-launch.png) | [358](playmode/realtime-steam-flight.png) | [362](playmode/realtime-steam-impact.png) |
| STORM | tick 3254 · башня 8 · цель 62 · L1 | [291](playmode/realtime-storm-launch.png) | [292](playmode/realtime-storm-flight.png) | [294](playmode/realtime-storm-impact.png) |

[Pause](playmode/realtime-pause.png) · [Continue после прерывания](playmode/realtime-continue.png).

## Earned test clock

Фазы реакции сняты после фактических simulation events на неизменённой камере. У Storm исходная primary является началом первой дуги. В таблице указаны только дополнительные попадания.

| Реакция | Tick / sequence / tower | Цели и AppliedHpLoss | PNG |
|---|---|---|---|
| Steam / StatusPair | 1255 / 2 / 4 | #21 (10 HP) → #22 (10 HP) | [primary и secondary](playmode/steam-statuspair.png) |
| Steam / Intrinsic | 2978 / 1 / 7 | #55 (18 HP) → #56 (18 HP) | [primary и secondary](playmode/steam-intrinsic.png) |
| Storm / StatusPair | 2725 / 1 / 6 | #49 (4 HP) → #50 (4 HP) | [первый hop](playmode/storm-statuspair-hop1.png) · [второй hop](playmode/storm-statuspair-hop2.png) |
| Storm / Intrinsic | 3134 / 1 / 8 | #59 (6 HP) → #60 (6 HP) | [первый hop](playmode/storm-intrinsic-hop1.png) · [второй hop](playmode/storm-intrinsic-hop2.png) |

- STEAM L3 · [Launch](playmode/steam-l3-launch.png) · [Полёт](playmode/steam-l3-flight.png) · [Impact](playmode/steam-l3-impact.png).
- STORM L3 · [Launch](playmode/storm-l3-launch.png) · [Полёт](playmode/storm-l3-flight.png) · [Impact](playmode/storm-l3-impact.png).

[Полный event key, позиции и source PNG SHA-256](reports/supplement.json).

## Synthetic test clock

[Шесть семейств, восемь противников и Страж](playmode/synthetic-dense-six-boss.png) · [Инспектор Стража](playmode/synthetic-dense-six-boss-inspector.png) · [Четыре статуса и два overhead слота](playmode/synthetic-all-four-inspector.png).
