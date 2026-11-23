## Результат

Closes #46. Refs #33.

Production Battle использует оригинального Shade и самостоятельного Стража Разрыва. Для каждого сохранены редактируемые Blender-исходники, FBX и пять clips: Idle, Move, Hit, Death, Leak. Выбор модели выполняется по EnemyDefinitionId; optional boss reference, legacy fallback и прежний Shade GUID/HP/status refs сохранены.

## Архитектура

Анимация и поворот меняют только дочернюю модель. Domain root остаётся на Y=0.6; root motion и damage callbacks отсутствуют. Per-step consumer обрабатывает все источники урона до очистки событий. Визуальные death/leak tails ограничены 16 экземплярами и 0.6 active seconds, замораживаются при Pause и очищаются при restore/menu/unload. Multipart stencil cues следуют анимируемым частям.

Shade — 400 triangles, босс — 1312; два разных shared palette materials на модель. Полные material slots: 5/8, с cues — 10/16. ART4 и все 18 tower references сохранены. Domain/config/balance/content/schema4/economy/spawn/ability и historical Continue не изменены: production 600 HP/armor5, historical v1 1000/5.

## VERIFIED

- Static **38/38**, полный EditMode **1237/1237**, полный PlayMode **93/93**; failed/skipped/inconclusive — 0. Tested source `a01706e`; 730/730 inputs восстановлены, unexpected/new — 0, detach/Refresh успешны.
- Реальный reexport двух `.blend` → FBX → Unity: **8 meshes / 10 clips**, 400 curve bindings / 8960 keys. Geometry, UV, normals, sampled poses, GUID, remaps и references совпали; post-suite readback идентичен.
- Полные FLAME/GALE replay traces до/после совпадают: 7856/7372 entries, оба Victory. Earned direct-Battle replay через public commands дошёл до boss/ability/death/Victory8 с неизменным final state; это явно обозначенный test clock.
- 30 настоящих Play Mode synthetic probes с исходной камерой; earned boss HUD/inspector/death. Финальное непрерывное Editor-видео: 305 composited frames / 31.05 s, MainMenu→NewGame→production Update Combat/ability/death→Upgrade→file Continue/whole Grid+snapshot rollback→Sell. Первый неудачный video attempt сохранён отдельно.
- Независимый source/animation/visual/delivery review: открытых подтверждённых P1/P2 нет. Default delivery, отдельная сохранность 7/7 carry-in, controls 14+9+2+3 и свежие static 38/38 — PASS. Raw failures и Editor recovery раскрыты в evidence.

## NOT VERIFIED / DEFERRED

Физический iPhone, device readability/touch/thermal/performance/player export/signing — **Deferred to prerelease QA #53/#54/#55**. Owner ART approval/G2, #47 final VFX, #48 UI и release остаются отдельно. Generic beams/graybox HUD сохранены в рамках прежнего контракта. Merge/release/TestFlight не выполнялись.

Четыре пользовательских rendering carry-in не входят в PR; фактические inputs учтены отдельно. Default delivery не требует личные TXT/pycache.

## Evidence и документация

[Spec/regressions](https://github.com/AlexeyKravtsov95/Veilward/blob/codex/art5-enemies/Docs/ART5_ENEMIES.md) · [Validation](https://github.com/AlexeyKravtsov95/Veilward/blob/codex/art5-enemies/Docs/review/ART.5/validation.md) · [Gallery/video](https://github.com/AlexeyKravtsov95/Veilward/blob/codex/art5-enemies/Docs/review/ART.5/gallery.md) · [Commands/raw failures](https://github.com/AlexeyKravtsov95/Veilward/blob/codex/art5-enemies/Docs/review/ART.5/commands.md) · [Independent review](https://github.com/AlexeyKravtsov95/Veilward/blob/codex/art5-enemies/Docs/review/ART.5/review.md).
