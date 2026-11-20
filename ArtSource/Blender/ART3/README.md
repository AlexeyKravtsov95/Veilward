# ART3 — FLAME L1–L3

Продолжение оригинального принятого `ART1/Tower_FlameLantern.blend`; baseline не перезаписан. Specification: `Docs/ART3A_FLAME_LEVELS.md`. Другие семейства не оформлены.

Source of truth — три редактируемых `.blend` Blender5.3.0 (`02f0899d78e3`), packed общая ART1 palette. Все708 исходных vertices сохранены; удалены только faces нулевой площади у концов исходных колец огня. Дополнительные бронзовые рёбра/чаши/разомкнутый венец и раздвоенное ядро — обычные meshes/UV, без runtime modifiers. `derive_flame_levels.py` документирует одноразовое авторское получение; повторный запуск перезаписывает ART3, не применять к вручную изменённым sources. После ручной правки использовать только `export_flame.py`.

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background ArtSource/Blender/ART3/Tower_FlameLanternL1.blend --python-exit-code 1 --python ArtSource/Blender/ART3/export_flame.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Blender/ART3/check_geometry.py
python3 ArtSource/Blender/ART3/verify_reexport.py
```

FBX preset идентичен проверенному ART1: meters/unit1, applied transforms/ground pivot, `-Z forward/Y up`, bake space/axis conversion, mesh-only triangles/normals; Unity scale1/ReadWriteOff/no animation/light/collider/tangents. Shared `MAT_Art1_Surface/Glow`, Point palette128×64/ASTC4×4, нет новых textures/shaders. Unity authoring: `VEILWARD/ART.3a/Import FLAME levels`; builder не пересоздаёт Battle/ART1, обновляет только ART3 prefabs/import settings и binding TestTower. Unity сама создаёт meta/GUID.

Provenance: собственная исходная ART1 geometry/palette из репозитория, принята владельцем в #41/#73; additions20.11.2026 выполнены Codex (GPT-6 family, программное Blender authoring по issue76), без внешних downloaded/generated service assets и чужих персонажей. Reference input — только собственный ART1 source и задание76. Seed/jobID не применимы: явная детерминированная mesh-конструкция, не image-to-3D service. Новых сторонних лицензий/ограничений redistribution не добавлено; это запись происхождения, не правовое заключение. Историческая ART1 provenance сохраняется.
