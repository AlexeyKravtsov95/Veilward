# STONE «Осколочная печать», ART.3d / #82

Оригинальная геометрия создана Codex для VEILWARD 22.11.2026 по заданию владельца #82. AI-assisted code-driven Blender mesh authoring; внешние модели, reference images, музыка и image-generation services не использовались. Prompt/inputs: issue82, GDD «Осколочная печать», тяжёлая нефритовая конструкция с расколотым монолитом, читаемой печатью, резными плитами и сдержанной бронзой; конструктивные L1/L2/L3 без навеса. Model: Codex на базе GPT-6; точный runtime snapshot не раскрыт инструментами и не выдумывается. Seed/jobID: неприменимы, deterministic mesh script.

Из принятого ART1 source загружаются только собственные материалы с packed palette. Геометрия FLAME/TIDE/GALE не используется и не меняется. Новых creative assets сторонних авторов и redistribution лицензий нет. Юридическое/коммерческое approval AI-assisted результата остаётся за владельцем; создание файла не доказывает исключительных прав.

`author_stone.py` создаёт только три собственных editable .blend и заменяет их ручные правки. Для сохранения ручных Blender правок использовать `export_stone.py`: whitelist трёх STONE source names/directories, прежний проверенный ART1 FBX preset. `check_geometry.py` читает сохранённые sources и проверяет units/transforms/ground/footprint/budgets/UV/normals/semantic groups; пишет geometry-manifest.json. `verify_reexport.py` реально реэкспортирует3/3 и сравнивает Unity readback при открытом Edit Mode этого checkout. Timestamp header FBX может меняться; canonical restoration и повторный readback раскрываются в evidence.

L1 — расколотый монолит с крупной лицевой печатью, L2 — две высокие наклонные боковые плиты, L3 — парные раздельные верхние зубцы и внешние опоры. Один editable mesh с семантическими vertex groups на уровень. UV выбирают содержательные swatches существующей 8×4 palette; отдельные textures не нужны. Surface/Glow и принятый stencil mask, без новых материалов/lights/renderfeatures. `ShotSource` — prefab-local future presentation anchor, механика неизменна.

**Device Deferred to prerelease QA #53/#54/#55.** Editor/source proof не является device/performance PASS или художественным approval владельца.
