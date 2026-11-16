# ART.1 — редактируемый source и экспорт

Оригинальные модели для проверки гипотезы «парящее святилище стихий». Стиль/массовое производство не утверждены.

## Редактирование

Открыть нужный `.blend` в Blender 5.3.0 (`02f0899d78e3`). Geometry остаётся обычным редактируемым mesh: Edit Mode, отдельные connected components, UVMap, именованные Surface/Glow slots. Палитра packed в каждый `.blend`; внешних исходных картинок нет. `.blend` находятся вне Assets и не импортируются Unity.

`create_art1.py` — воспроизводимая исходная конструкция из оригинальной геометрии. Её повторный запуск **пересоздаёт source**; для ручных правок использовать только `export_art1.py`, который читает сохранённую модель. Операции bevel применены в mesh, а не скрыты в Unity. Никакого Blender runtime dependency у Unity нет.

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background ArtSource/Blender/ART1/Tower_FlameLantern.blend --python ArtSource/Blender/ART1/export_art1.py
```

Экспорт всегда в прежний путь FBX; `.meta` не трогать. При изменении mesh/material names или структуры submeshes повторить reference audit; такие изменения не обещаны автоматически совместимыми.

## Фактический preset

- Metric, unit scale 1.0: 1 Blender unit = 1 m = 1 Unity unit.
- Blender +Z up; этот preset переводит Blender **+Y front в Unity +Z front**. Проверено на позиции реальной маски Shade после импорта; не выводится только из имени axis flag.
- FBX `axis_forward='-Z'`, `axis_up='Y'`, `use_space_transform=True`, `bake_space_transform=True`.
- `global_scale=1`, `apply_unit_scale=True`, `apply_scale_options='FBX_SCALE_UNITS'`.
- Mesh only, selected objects, modifiers applied, `use_triangles=True`, `mesh_smooth_type='FACE'`; animation/cameras/lights/leaf bones отсутствуют.
- Location/rotation/scale applied; mesh origin `(0,0,0)`, root transform identity. Tower/Shade pivot — центр ground plane; island origin — центр поверхности по X/Z, верхняя поверхность немного ниже Y=0; kit props — центр основания/snapping plane.
- Unity importer: scale 1, use file scale, bake axis conversion, imported normals, no tangents/animations/cameras/lights/colliders, Read/Write off. Prefabs не исправляют размер через отрицательный/скрытый scale.
- Shared palette 128×64, Point/Clamp, no mipmaps; iPhone override ASTC 4×4. Обычный mesh material — URP/Lit; магические вставки — opaque URP/Unlit. Нет новых shaders, bloom, дополнительных renderer features или realtime point lights.

## Reference round trip

При открытом worktree в Unity Edit Mode:

```sh
python3 ArtSource/Blender/ART1/verify_reexport.py
```

Скрипт сохраняет imported GUID/local IDs, vertex/normal/UV/index hash, prefab dependencies и hashes `.meta`, prefab/scene YAML. Затем открывает **все восемь сохранённых .blend**, экспортирует в те же FBX и повторно импортирует Unity. Сравнение before/after должно быть точным. Отчёты — `Docs/review/ART.1/reexport-{before,after}.json`.

Unity scene/prefab authoring: меню `VEILWARD → ART.1 → Build ArtReference`. Builder работает только с ART.1 assets; не изменяет Build Settings, production balance или рабочие сцены. Для просмотра открыть `Assets/_Game/Scenes/ArtReference.unity`, Play. Showcase повторяет saved первую волну с одним FLAME; после завершения пауза 2 s и новый цикл. Inspector/context eval `SetPaused(true)`, `AdvanceForReview(n)`, `RestartShowcase()` позволяют зафиксировать реальное состояние. Lifecycle focus/background latch требует явного Resume через `SetPaused(false)` после возврата; нового игрового UI нет, это reference scene для Editor review.
