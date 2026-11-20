# ART.2b evidence index

Частичная доставка [#43](https://github.com/AlexeyKravtsov95/Veilward/issues/43); tested source7b753e3, physical iPhone16Pro, art-reference0.1.0(44). Scope/acceptance/risk/test specification: [ART2B_DEVICE_PROOF](../../ART2B_DEVICE_PROOF.md).

- [Результаты и ограничения](validation.md), [точные команды](commands.md), [независимый review](review.md).
- [Нативное видео normal combat1×](device/normal-native.mp4), [атрибуция/PTS](reports/video-proof.json); device/*.png и normal-sequence/ — исходные PNG. Synthetic probes подписаны в кадре.
- [Play Mode normal](playmode-normal.png), [dense](playmode-dense.png), [полные EditMode](reports/edit-mode.json), [PlayMode](reports/play-mode.json).
- [Raw timing runs](measurements/), [пересчёт](reports/measurements.json), [native memory](reports/native-memory-summary.json), [Metal capability](reports/metal-capability.json).
- [Receipt](reports/export-receipt.json), [source audit](reports/source-audit.json), [manifest](manifest.json).

В чистом checkout, содержащем tested source в Git history:

```sh
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.2b/verify_delivery.py
```

Verifier не запускает Unity и не изменяет файлы. Он проверяет полный artifact inventory/SHA256, фиксированный tested source и protected history, непустые1111/74case suites,3×3 полных measurement windows. Manifest не хеширует сам себя. Повторные device runs требуют локального подписания и устройства; никаких private credentials или идентификаторов в пакете нет. Raw signed build/trace/native video retained locally, game-only evidence опубликовано с явно указанной обработкой.
