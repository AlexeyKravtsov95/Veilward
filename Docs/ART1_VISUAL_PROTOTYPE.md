# ART.1 — визуальная гипотеза для просмотра владельцем

База: origin/main `a506bed`, PR #23 merged. Стиль не утверждён. G2, баланс, device QA и TestFlight открыты.

## Scope / решения прототипа

Парящее святилище: тёплый светлый камень, угольное дерево, бронзовые кромки; огонь — коралл/золото, пустота — индиго с холодным разрывом лица. Крупные фаски и плоскости дают рисованный характер без нового shader framework. Единственная FLAME — открытый фонарь с четырьмя опорами, изогнутой крышей и видимым пламенем. Shade — парящий разорванный плащ, раздвоенная корона и светлая маска с направленным разрезом; лицо задаёт forward.

Отдельная ArtReference использует точные transform/projection/orthographic size текущей Battle Camera и saved TestWave simulation через public commands. Малый повторяемый combat showcase не является новой картой/режимом. Domain, Battle/MainMenu, баланс, checkpoint, Build Settings и packages не меняются. Нет новой UI, массового производства, P2.3, релиза.

## Acceptance criteria

- Blender sources вне Assets, units meters; применённые transforms; base pivots; +Y up/+Z forward в Unity; documented FBX preset.
- Фрагмент острова, поворот пути, различимые площадки, согласованные декорации; FLAME, Shade и attack видны совместно.
- Не более 5000 triangles и двух material slots на обычную модель, textures <=1024; фактические цифры в manifest.
- Повторный экспорт из сохранённого .blend в тот же FBX сохраняет GUID, model subasset IDs и prefab/scene references.
- Реальные Play Mode кадры: общий вид, FLAME/ Shade closeups, атака, светлый/тёмный фон; видео при доступном capture. Closeups явно отделены от основного масштаба.
- Static, EditMode, PlayMode, diff check; Console audit. Editor verification не подтверждает iPhone/performance/style approval.

## Regression scenarios (спецификация до реализации)

- ART1-01: saved scene загружается, camera совпадает с Battle, links/materials/shaders valid, release scenes/configs не изменены.
- ART1-02: imported meshes укладываются в budgets, основание tower/enemy на Y=0, face +Z; без negative scale и missing slots.
- ART1-03: reexport каждого .blend → FBX, force reimport: GUID/local IDs и prefab dependencies прежние, serialized links не missing.
- ART1-04: Play Mode использует saved simulation: FLAME покупается public command за saved price, enemies двигаются по saved path, атака/HP/kills происходят от настоящего combat; VFX запускается только по AttackEvent.
- ART1-05: остановка showcase замораживает simulation и VFX; resume без catch-up; повторный цикл не накапливает views/effects.
- ART1-06: main camera overview и attack frame сохраняют читаемость дороги, Shade, HP и свободных build pads; кадры и Console прилагаются для человеческой оценки.

## Риски и тесты

Мелкие элементы могут теряться при ortho 8.5; геометрия/контраст важнее детализации. Не добавляем bloom, realtime point lights или прозрачные оболочки. Pool ограничен; material assets общие. EditMode проверяет import/scene/reference contract, PlayMode — combat/pause/pool. Физический iPhone требуется для touch/safe area, читаемости, ASTC качества, memory/frame time/thermal. Правовая/продуктовая оценка оригинальных AI-assisted assets остаётся владельцу.
