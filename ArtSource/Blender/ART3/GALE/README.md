# GALE «Ветряной талисман», ART.3c / #80

Оригинальная геометрия написана Codex для VEILWARD 21.11.2026 по разрешению владельца в #80. AI-assisted code-driven Blender mesh authoring; image generation services, внешние модели и reference images не использовались. Prompt: issue80, GDD «Ветряной талисман», открытая лёгкая конструкция, камень/нефрит/бронза, крупные конструктивные L1/L2/L3; реализация и ручная программная правка зеркальных дуг — author_gale.py. Seed/job ID: не применимы, детерминированный mesh script. Model/version runtime агента не раскрыт отдельным tool metadata; не выдумывается.

Вход из ART1 — только принятые материалы/packed palette собственного проекта. Геометрия FLAME/TIDE не используется и не меняется. Редактируемые `.blend` принадлежат source группе GALE, FBX — экспорт. Нет сторонней redistribution лицензии или новых импортированных creative assets; юридическое/коммерческое approval AI-assisted результата не заявлено агентом.

`author_gale.py` пересоздаёт только три GALE source (ручные Blender правки будут заменены). Для экспорта ручных правок: открыть соответствующий source и выполнить `export_gale.py`. Export ограничен whitelist трёх GALE имен и своей директорией, сохраняет проверенный preset ART1. `check_geometry.py` читает все sources, проверяет units/transforms/budgets/semantic shapes/UV/normals и пишет geometry-manifest.json. `verify_reexport.py` реально реэкспортирует все три и сравнивает Unity readback; запускать только при открытом Edit Mode task checkout. FBX timestamp header может отличаться: committed FBX возвращаются после проверки и сравниваются повторно, это раскрыто в evidence.

L1 — висящий ромб/разомкнутая рама, L2 — две зеркальные ветровые дуги, L3 — верхняя спираль. Palette UV выбирает цвета существующей 8×4 palette, геометрические UV-развёртки для новых textures не нужны. Shared Surface/Glow и уже принятый stencil mask; без новых textures/materials/animation/VFX. `ShotSource` создаётся prefab builder как локальный future anchor.

Device Deferred to prerelease QA #53/#54/#55. Editor/source proof не подтверждает физический iPhone, коммерческие права, производительность или художественное approval владельца.
