# VFX.1a / #90 — production FLAME

VERIFIED: движущийся fireball, pool8, pause/Continue/reset, исходные gameplay damage/timing. Static38/38, Edit1252/1252, Play96/96,0failed/skipped/inconclusive. [Подробности](validation.md), [спецификация](../../VFX1A_FLAME_PROJECTILE.md).

[Непрерывное production Update видео — 22,3s](editor-flow.mp4): L1/L2 flight/cadence, frozen pause/resume, ability, earned Upgrade, file Continue/Grid rollback, Sell. 639 исходных composited GameView кадров; исходные timestamps, без склеек/изменения скорости. Device Deferred QA53/54/55.

[Кадры и условия](gallery.md): L1/L2 — exact PNG из этого видео; L3 и dense/boss — отдельный earned public-command test-clock replay, явно подписан. Полный winning replay достигает Victory8, затем исходный earned Preparation8 восстанавливается целиком; обычные Sell и Upgrade дают L3 без изменения Gold/HP/DTO. Immediate HP/Burn/Hit/Death — исходный tick, arrival только косметический.

Проверка committed delivery: `python3 Docs/review/VFX.1a/verify_delivery.py`; controls: `python3 Docs/review/VFX.1a/verify_controls.py`, `python3 Docs/review/VFX.1a/verify_semantic_controls.py`. Личные TXT/pyc не prerequisite. Отдельный explicit audit: `--check-local-preservation`.

NOT VERIFIED: физический iPhone, IL2CPP/Metal player/performance/thermal, human ART/G2/release. [Независимое code/visual review](review.md) — без блокирующих замечаний; owner merge не выполняется.
