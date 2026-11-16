# ART.1.1 — asset manifest

Измерения saved `.blend` и реально импортированного FBX, Unity 6000.3.28f1; source authoring Blender 5.3.0 (`02f0899d78e3`). [Machine-readable Unity audit](imported-models.json), [source counts](../../../ArtSource/Blender/ART1/geometry-manifest.json).

| Asset | Source vertices / triangles | Unity vertices / triangles | Slots |
|---|---:|---:|---:|
| `Tower_FlameLantern` | 708 / 1264 | 2624 / 1232 | 2 |
| `Enemy_Shade` | 133 / 185 | 431 / 185 | 2 |
| `Environment_FloatingIsland` | 434 / 776 | 1490 / 776 | 1 |
| `Environment_PathTile` | 64 / 112 | 240 / 112 | 1 |
| `Environment_BuildPad` | 192 / 352 | 704 / 320 | 1 |
| `Prop_Waystone` | 120 / 220 | 480 / 220 | 1 |
| `Prop_MossRock` | 42 / 72 | 126 / 72 | 1 |
| `Prop_RitualShrub` | 89 / 138 | 276 / 138 | 1 |

Каждая модель ниже 5 000 triangles и не более двух material slots по ART_PIPELINE. Unity splits vertices для hard normals/UV; source vertex count не выдаётся за runtime count. Верхняя граница height FLAME 2,285 m, основание .94×.94 m; Shade прежние общие габариты .84×1.35×.5626 m. У острова depth до -7.2 m, три отдельных нижних массива и шесть выступающих террас.

- Единственная shared palette **128×64**, Point/Clamp, mipmaps off. iPhone override **ASTC 4×4**, max size 128; новых textures нет.
- Прежние `Art1Surface` / `Art1Glow`, Unity `MAT_Art1_Surface` (URP/Lit) / `MAT_Art1_Glow` (opaque URP/Unlit); backface culling включён. VFX materials прежние. Нет новых shaders, renderer features, lights или packages.
- Sources: `ArtSource/Blender/ART1/<Asset>.blend`; FBX: `Assets/_Game/Art/Models/ART1/<Asset>.fbx`; prefabs: `Assets/_Game/Prefabs/ART1/P_<Asset>.prefab`. Имена, `.meta`, GUID, model subasset IDs и prefab references сохранены. [Round trip](reexport.txt), [before](reexport-before.json), [after](reexport-after.json), [reference audit](reference-audit.json).
- Изменена геометрия только острова, FLAME и Shade. Пять road/pad/prop kit моделей переиспользованы; после полного round trip timestamp-only FBX noise для них восстановлен из базы, итоговый imported snapshot совпал побайтово по геометрии/IDs/references. ART.1 evidence не менялось.

## Происхождение

Продолжение оригинальных AI-assisted meshes ART.1 из PR #24: автор правок Codex по заданию владельца, 2026-11-16→17. Никаких скачанных моделей, изображений, музыки, сторонних персонажей или reference inputs не добавлено. `refine_art11.py` сохраняет однократные правки существующих editable источников, `export_art1.py` экспортирует сохранённые sources. Input — только собственные восемь `.blend` ART.1; исходная конструкция остаётся в `create_art1.py`, применять её поверх refinements нельзя. Новые prompts/генерация изображений/сторонние 3D services не использовались. Юридическая/коммерческая оценка AI-assisted assets и утверждение стиля по-прежнему у владельца.

## NOT VERIFIED / OPEN

Это геометрический/import budget PASS, **не device performance PASS**. Физический iPhone, ASTC качество/resident memory, allocations/frame time/thermal, LOD и художественная приёмка не проверены. Массовое производство не утверждено.
