# P2.3e.1 — acceptance matrix

| Критерий | Статус | Evidence |
|---|---|---|
| 150 Gold/20 HP; public EightWave/commands; DTO без edits | VERIFIED | Четыре scripts/summary/trace, config-audit.json; search sources |
| FLAME/GALE до6 и natural terminal8 | VERIFIED | A coverage Victory8 HP6/Gold90/Kills54; B HP8/Gold112/Kills56 |
| Выживание отдельно от реакций | VERIFIED | A/B-survival Victory8 HP6/8, Gold229/290; не объявлены полным reaction coverage |
| Каждый выбранный script дважды New Game/full trace | VERIFIED | 8 gzip traces; decompressed bytes + SHA; REG188 |
| Steam/Storm pair и intrinsic positive AppliedHpLoss | VERIFIED | Все4 вида в каждом coverage script; reaction-damage-audit.json |
| Доступность/покупки/procs в разрешённых волнах | VERIFIED | entitlements/receipts; никаких procs до4, A Storm и B Steam лишь7…8 |
| Pending/confirmed6 и Preparation7 | VERIFIED | files/A,B.audit.txt; playmode traces; screenshots pending6/confirmed6/preparation7 |
| File Continue до/после Confirm6; failed save/restart/retry | VERIFIED | Whole snapshot/no-write Load; 2 expected save errors на UI route (3 и6), retries не выдают выбор снова |
| Combat7 после real kill+positive reaction, две одинаковые попытки, dirty/fresh rollback | VERIFIED | files/A,B.rollback7.txt; no enemies/events; menu clean summary/beam/VFX/selection/preview |
| Ранний rollback | VERIFIED | Новые A4/B5; прежний B4 остаётся историческим контролем, без преждевременного требования proc от армии без TIDE |
| Настоящий MainMenu/controllers и 8 wave snapshots | VERIFIED | 2 PlayMode tests, сравнение с full Domain snapshots, natural Victory |
| Реальные поздние UI screenshots/Console | VERIFIED | 28 кадров1920×1080; 16 known Editor warnings на route, 0 unexpected errors; test clock не performance QA |
| Исторические P2.3e Defeat5/Stone и synthetic fixtures | VERIFIED | Неизменные historical files, полные suites; synthetic не выдаются за natural |
| Static/полные EditMode/PlayMode/diff | VERIFIED | 38/38; 761/761; 45/45; Visual2/2; gates.json/reports |
| Runtime/config/packages/art/meta | VERIFIED | Без production delta от merge PR31; config-audit.json/static |
| P2.3 overall | NOT DONE | Production late evidence закрыто; independent review/owner approval/merge отдельно |
| Device/G2/баланс/ART approval/TestFlight | NOT VERIFIED / BLOCKED | Не входят в этот PR; Editor evidence их не заменяет |
