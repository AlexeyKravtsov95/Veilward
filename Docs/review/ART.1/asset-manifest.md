# ART.1 — asset manifest / provenance

Дата создания: 2026-11-16. Все перечисленные meshes и palette созданы специально для этого запроса в OpenAI Codex с помощью Blender Python. Запрошенная пользователем модель сессии — GPT Sol 6.1 / High; отдельный media service, seed/job ID модели и image references не использовались. Фактическая версия Blender: 5.3.0 / 02f0899d78e3.

Входные материалы: только художественный brief владельца, GDD и ART_PIPELINE этого репозитория. Чужих персонажей, изображений, моделей, музыки и downloaded textures нет. Геометрия, UV и палитра оригинальные, AI-assisted; пользователь поручил создание прототипа. Наличие AI output не считается подтверждением исключительных прав. Утверждение коммерческого использования и художественного направления владельцем не выполнено; отдельная правовая экспертиза/terms review не проводилась.

## Фактические meshes

| Asset | Triangles | Material slots | Source / runtime |
|---|---:|---:|---|
| Tower_FlameLantern | 1264 | 2 | `ArtSource/Blender/ART1/Tower_FlameLantern.blend` → `Assets/_Game/Art/Models/ART1/Tower_FlameLantern.fbx` |
| Enemy_Shade | 183 | 2 | `ArtSource/Blender/ART1/Enemy_Shade.blend` → `Assets/_Game/Art/Models/ART1/Enemy_Shade.fbx` |
| Environment_FloatingIsland | 932 | 1 | `ArtSource/Blender/ART1/Environment_FloatingIsland.blend` → `Assets/_Game/Art/Models/ART1/Environment_FloatingIsland.fbx` |
| Environment_PathTile | 112 | 1 | `ArtSource/Blender/ART1/Environment_PathTile.blend` → `Assets/_Game/Art/Models/ART1/Environment_PathTile.fbx` |
| Environment_BuildPad | 352 | 1 | `ArtSource/Blender/ART1/Environment_BuildPad.blend` → `Assets/_Game/Art/Models/ART1/Environment_BuildPad.fbx` |
| Prop_Waystone | 220 | 1 | `ArtSource/Blender/ART1/Prop_Waystone.blend` → `Assets/_Game/Art/Models/ART1/Prop_Waystone.fbx` |
| Prop_MossRock | 72 | 1 | `ArtSource/Blender/ART1/Prop_MossRock.blend` → `Assets/_Game/Art/Models/ART1/Prop_MossRock.fbx` |
| Prop_RitualShrub | 138 | 1 | `ArtSource/Blender/ART1/Prop_RitualShrub.blend` → `Assets/_Game/Art/Models/ART1/Prop_RitualShrub.fbx` |

Все цифры — triangulated LOD0 из Blender, подтверждаются imported mesh index counts в EditMode. Prefabs: `Assets/_Game/Prefabs/ART1/P_<asset>.prefab`. Каждая обычная модель <5000 triangles и <=2 slots. LOD/skinning/animation отсутствуют; Shade движется/поворачивается root transform по Domain path.

## Textures / materials / VFX

| Asset | Размер / slots | Происхождение |
|---|---|---|
| Art1Palette_BaseColor_128.png | 128×64 RGBA, 32 solid swatches | Авторская палитра в create_art1.py; packed в .blend; shared всеми восемью моделями |
| MAT_Art1_Surface | 1 URP/Lit, palette | Оригинальная настройка ART.1 |
| MAT_Art1_Glow | 1 opaque URP/Unlit, palette | Ядро/face fissures; без прозрачных оболочек |
| MAT_Art1_EmberStroke / EmberCore | 2 URP/Unlit, без texture | Два tapered ribbons + 12-segment impact petals, 8 pooled strikes, .22 s lifetime |
| HealthBackground / HealthFill | Existing URP/Unlit, без texture | Переиспользованы из проекта вместе с EnemyHealthBarView; новые UI assets не создавались |

iPhone texture override — ASTC 4×4; фактическое качество/память на iPhone не проверены. Emission воспринимается через непритенённый материал, не через lights/bloom. VFX получает только настоящие FLAME AttackEvents; здоровье/золото не рисуются произвольными значениями.

## Воспроизводимость и ограничения

Island jitter имеет фиксированный Python random seed 101. Других случайных входов нет. Исходники — редактируемые meshes, отдельные connected components и UVMap; ручные изменения выполняются в .blend с повторным export, не в FBX. Source-specific scripts не являются общим exporter framework. Git LFS не включался.

Основная оценка читаемости — в сохранённой Battle camera. Closeups увеличивают масштаб только для рассмотрения формы. Физический iPhone, thermal/memory/frame timing, built-player quality и правовая/продуктовая приёмка не подтверждены.
