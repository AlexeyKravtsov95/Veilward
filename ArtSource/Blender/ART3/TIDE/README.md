# ART3B — TIDE «Колокол прилива» L1–L3

Specification: `Docs/ART3B_TIDE_LEVELS.md`, issue #78. Три оригинальных редактируемых `.blend` Blender 5.3.0 (`02f0899d78e3`), один mesh с именованными semantic vertex groups на уровень. L1 — полый бронзовый колокол с языком, открытая нефритовая рама и низкая водяная чаша; L2 — крупные боковые водосборники и усиленные открытые арки; L3 — два высоких разомкнутых водяных рога. Сплошной крыши нет. Вода — статическая mesh/palette форма; финальные водные VFX относятся к #47.

Sources — source of truth; ручная правка vertex groups/geometry/UV в Blender поддерживается. `author_tide.py` фиксирует первоначальное детерминированное авторское построение; повторный запуск перезаписывает только TIDE sources и не предназначен для экспортирования ручных правок. `export_tide.py` экспортирует только один открытый source из этой папки и допускает ровно три заданных имени. `check_geometry.py` независимо открывает фактические сохранённые sources, проверяет непустые normals/UV/группы/формы, отсутствие вырожденных triangles, footprint, ground pivot и budgets; пишет `geometry-manifest.json`.

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Blender/ART3/TIDE/check_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender --background ArtSource/Blender/ART3/TIDE/Tower_TideBellL1.blend --python-exit-code 1 --python ArtSource/Blender/ART3/TIDE/export_tide.py
# Аналогично L2/L3; после ручного редактирования не запускать author_tide.py.
```

Preset прежний ART1/FLAME: meters/unit1, applied identity transforms/ground pivot, `-Z forward/Y up`, bake space transform, mesh-only triangles/normals, no animation. Unity importer: scale1/fileScale/bakeAxisConversion, normals Import, ReadWrite/animation/camera/light/collider/tangents Off. Два remapped shared `MAT_Art1_Surface`/`MAT_Art1_Glow`; packed source palette взята из принятого ART1. Новых textures, materials, shaders, lights, dependencies нет. Unity authoring — `VEILWARD/ART.3b/Import TIDE levels`; `.meta`, GUID и prefabs создаёт Unity Editor. Builder не регенерирует FLAME/ART1.

Происхождение: оригинальная явная mesh-конструкция Codex (GPT-6 family, программное Blender authoring) 20.11.2026 по issue #78; входы — собственное текстовое задание и принятая общая ART1 palette/materials из репозитория. FLAME geometry не использована. Внешних загруженных моделей, изображений, персонажей, image-to-3D или иных generative media services не использовано. Seed/job ID не применимы к явной детерминированной конструкции. Новых сторонних лицензий/условий redistribution нет; историческая ART1 provenance сохранена. Это запись происхождения, не правовое заключение. Физический iPhone QA отложен владельцем в #53/#54/#55; исходные geometry checks и Editor evidence не являются device PASS.
