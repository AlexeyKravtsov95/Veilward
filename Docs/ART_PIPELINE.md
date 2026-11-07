# VEILWARD — Art Pipeline (подготовительный контракт)

Это правила будущей работы, не запуск массового производства. Первый эталонный ассет, окончательные budgets и визуальный стиль утверждаются владельцем после проверки в Unity и на реальном iPhone. Exporter, asset manager и новая render system в этот тикет не входят.

## Source of truth и форматы

- Редактируемый 3D source — **Blender `.blend`**. Он хранится отдельно от Unity assets, например в `ArtSource/Blender/`, и не импортируется в `Assets`.
- В Unity импортируется **FBX** в `Assets/_Game/Art/Models/`. Рядом сохраняются Unity-generated `.meta`.
- Текстуры, материалы и prefabs размещаются в соответствующих `Art/Textures`, `Art/Materials`, `Prefabs` подпапках. Внешние source textures не дублируются без необходимости.
- `.blend` не является runtime asset. Изменения вносятся в source и повторно экспортируются, а не редактируются только в импортированном FBX.

## Единицы, оси и pivots

- Рабочие единицы — **meters**: 1 Blender unit = 1 метр = 1 Unity unit. Масштаб применяется до export; importer scale не используется для скрытого исправления размера.
- Целевая ориентация Unity: **+Y up, +Z forward**. Blender использует +Z up; conversion задаётся один раз в утверждённом export preset и проверяется на эталонной модели.
- Pivot башни/персонажа — центр основания на ground plane; pivot дорожного/prop объекта выбирается для предсказуемого snapping. Центровка и applied transforms проверяются до экспорта.
- Forward/up, scale, pivot и normals нельзя считать правильными без Unity preview и device check.

## Именование

Используются стабильные ASCII PascalCase имена без пробелов и временных суффиксов `final_final`:

| Asset | Правило | Пример |
|---|---|---|
| Blender source | `<Domain>_<Name>.blend` | `Tower_FlameLantern.blend` |
| FBX | `<Domain>_<Name>.fbx` | `Tower_FlameLantern.fbx` |
| Texture | `<Domain>_<Name>_<Map>_<Size>` | `Tower_FlameLantern_BaseColor_1024.png` |
| Material | `MAT_<Domain>_<Name>` | `MAT_Tower_FlameLantern.mat` |
| Prefab | `P_<Domain>_<Name>.prefab` | `P_Tower_FlameLantern.prefab` |

Map suffixes: `BaseColor`, `Normal`, `Mask`, `Emission`. Переименование уже связанных assets выполняется в Unity с сохранением `.meta`, а не удалением и повторным импортом.

## Предварительные mobile budgets

Это стартовые ограничения для graybox-to-production prototype, **не измеренный performance PASS**:

| Категория | Предварительный budget |
|---|---|
| Обычная башня / враг | До 5 000 triangles на LOD0 |
| Героический prop / elite | До 10 000 triangles на LOD0 |
| Boss | До 15 000 triangles на LOD0 |
| Обычная модель | До 2 materials; минимум texture sets и material slots |
| Обычные textures | До 1024×1024 на texture set |
| Boss / уникальный hero | До 2048×2048 только после обоснования |
| UI atlas | До 2048×2048; без неиспользуемого прозрачного пространства |
| iOS compression | ASTC с проверкой визуального качества и resident memory |

LOD1+, skinning, particles, lights и shadows оцениваются отдельно. Бюджеты пересматриваются по Unity Profiler/Xcode Instruments на выбранном базовом iPhone; нельзя обещать 60 fps только по размеру assets.

## GUID и обновления

- `.meta` коммитятся вместе с asset и переносятся при rename/move.
- Не удалять `.meta`, не генерировать GUID вручную и не заменять asset через delete/reimport без необходимости.
- Prefab/scene references и import settings проверяются после обновления. `Scripts/run-static-checks.sh` отклоняет missing/orphan `.meta` и duplicate GUID.
- Крупные бинарные исходники могут использовать **Git LFS только после решения владельца**: учитываются стоимость storage/bandwidth, quotas и доступность LFS для contributors/CI. Нельзя включать LFS незаметно для существующего history.

## Проверка эталонного ассета

1. Проверить source units/axes/pivot/normals и отсутствие неиспользуемых materials/textures.
2. Импортировать FBX, сохранить `.meta`, проверить scale, orientation, materials, shader compatibility и serialized references в Unity.
3. Сравнить с утверждённым визуальным референсом в реальной Battle lighting/camera и приложить Play Mode screenshots.
4. Проверить модель на физическом iPhone: читаемость silhouette, safe area, texture quality, overdraw, memory и frame time. Editor preview не считается device PASS.
5. Зафиксировать измерения и только затем утверждать budgets/массовое производство.

## Происхождение и лицензии

Для каждого asset сохраняется provenance record: автор/источник, дата, source files, license/разрешение, ограничения redistribution и подтверждение владельца. Чужие персонажи, музыка, модели и изображения без подтверждённой лицензии запрещены.

Для **AI-generated** content дополнительно фиксируются service/model и версия, дата, prompt/reference inputs, seed/job ID если доступны, права на использование входных материалов, применимые terms и ручные правки. Наличие AI output не считается доказательством авторских прав или допустимости коммерческого использования. Спорный или неизвестный статус означает BLOCKED до правовой проверки владельцем.
