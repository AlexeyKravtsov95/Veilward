# ART4 — оригинальное окружение первой миссии

Создано 23.11.2026 в OpenAI Codex в рамках #45. Авторская программно описанная геометрия, без скачанных моделей, чужих персонажей, image generation или AI-texture placeholders. Входы: задание владельца #45, принятие стиля #41/#43 и существующая авторская ART1 palette `Art1Palette_BaseColor_128.png`. Новых внешних лицензируемых inputs нет; права на проект/palette остаются у владельца. Точный backend model/version/seed/job ID этой Codex-сессии не предоставлен; seed геометрии **445**. Это provenance, не самостоятельное юридическое заключение.

Editable `.blend` содержит meters/scale1, applied transforms, pivot `(0,0,0)`, именованные semantic vertex groups, palette UV и normals. Все изменения сохраняются в источнике. Остров расположен относительно прежнего production origin, его верх `y=-.065` в Unity; нижние уступы и teeth ниже gameplay plane. Path/socket/entry/heart/pine — отдельные повторяемые kit assets; giant combined level отсутствует. Шесть объектов используют существующие shared surface/glow, максимум2slots; runtime `.blend` не импортируются.

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Blender/ART4/author_environment.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Blender/ART4/check_geometry.py
# После ручного редактирования конкретного source — export, не author:
/Applications/Blender.app/Contents/MacOS/Blender --background ArtSource/Blender/ART4/Environment_SanctuaryIsland.blend --python-exit-code 1 --python ArtSource/Blender/ART4/export_environment.py
PYTHONDONTWRITEBYTECODE=1 python3 ArtSource/Blender/ART4/verify_reexport.py
```

Preset: FBX_SCALE_UNITS, globalScale1, axisForward−Z/upY, bakeSpaceTransform, applied units, triangulated faces, imported normals, no animation/cameras/lights/colliders. Unity: scale1/fileScale/bakeAxis, no tangents, shared ART1 remaps, generated GUID/meta. Реальный roundtrip/readback и measured budgets — [новое evidence](../../../Docs/review/ART.4/validation.md). Исторические ART1/3 sources, prefabs и proof bytes не переписываются.

`author_environment.py` воспроизводит исходную форму; для следующей ручной mesh-правки редактировать `.blend` и экспортировать через `export_environment.py`. Сцена создаётся `BattleSceneBuilder`, kit импортируется отдельным `BattleEnvironmentBuilder.Import`. Decor static и без colliders/runtime scripts. Stencil masks на входе/Сердце/ближайших props используют прежний shader/bit64, это явные presentation passes, учитываемые в counts.

Device Deferred to prerelease QA #53/#54/#55. Source/Editor budgets не подтверждают iPhone performance, владельческую художественную приёмку, G2 или release.
