# ART3d — план реализации #82

**Goal:** четвёртая production линейка STONE и безопасный единый binding четырёх семейств.
**Architecture:** optional stoneLevels в прежнем FlameModelBinding, отдельные source/asset/build/test группы; mechanics неизменны.
**Tech Stack:** Unity6000.3.28f1, Blender, C#, Python stdlib, существующий Pipeline/encoder.
**Spec:** [ART3D_STONE_LEVELS.md](ART3D_STONE_LEVELS.md).

## Ограничения и review focus

Accepted ART1/F/T/G и исторические proof bytes сохраняются. Нет packages/Domain/Infrastructure/config/save delta. Missing refs, same-level type switch, deferred cleanup, full checkpoint rollback и HP/status occlusion проверяются явно.

- [ ] Сначала зафиксировать ART3D-01…06. Добавить StoneModelAssetTests/StoneModelPlayModeTests; проверить ожидаемое отсутствие production STONE.
- [ ] Авторить только STONE sources/export, добавить StoneModelBuilder.Build/Bind/PrefabPath и stoneLevels, BattleSceneBuilder wiring. Проверить geometry и Unity import/generated refs.
- [ ] Прогнать focused tests четырёх семейств, earned seven-wave STONE unlock/file/menu flow; сохранить trace/checkpoints и отрицательные guards. Исправить дефекты до final evidence.
- [ ] Adapt task-specific CaptureArt3d/RecordArt3d/encoder из принятого composited capture. Снять actual Play Mode singles/lineup/comparison/occlusion/preview и continuous real-time replay с Crack/Inspect/rollback/Sell; inspect изображения/видео.
- [ ] Реальный roundtrip3/3, canonical readback, static/full Edit/full Play/compile/Console/diff. Сохранить sourceSHA/inventory/hash manifest/current verifier, clean task-owned checkout и negative controls.
- [ ] Независимое code/asset/visual/evidence review, исправления/необходимые повторные gates; PR Closes82/Refs44, Device Deferred53/54/55. Self-merge/release запрещены.
