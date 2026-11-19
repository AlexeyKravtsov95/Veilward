# ART.2a.1 — Shade, HP и огненный импульс (#73)

База `7783dd1`, merge #72 подтверждён 19.11.2026. Направление острова/палитры/фонаря принято владельцем в #41; эти три доработки уже разрешены. #43 остаётся отдельным physical iPhone gate.

## Scope / acceptance

Presentation-only. Исходная камера (8,11,8), rotation (44.1945,225,0), ortho8.5; scale1; прежние gameplay positions/path/spawns. Сравнение 1600×900 stills и 1280×720/30fps combat video при одинаковых параметрах. Не меняются Domain, content v2 600/5, schema4, saves, награды, способность, Blender/FBX/пивоты и материалы моделей.

1. Shade: малый occluded-body presentation pass сохраняет форму/маску на участке за фонарём, без сдвига модели или камеры. Только эталон ArtReference; production Battle пока graybox. Не является production art integration.
2. HP: deterministic screen-space вертикальное разнесение пересекающихся полос с тонкой связью к исходному якорю. Реальная доля HP и enemy association сохраняются. Буфер переиспользуется; нет поиска объектов на tick. Общий EnemyHealthBarView применяется в Battle и ArtReference, оба проверяются.
3. FLAME: источник вспыхивает в начале, разорванный сужающийся след виден лишь в первой фазе (до .07s), локальный impact и несколько угольков гаснут до .22s. Оранжево-красная масса, небольшой горячий акцент; гладкая длинная жёлтая дуга удаляется. Только настоящий AttackEvent/TargetPosition, урон остаётся мгновенным. Малый unlit overlay pass (ZTestAlways/ZWriteOff) сохраняет impact видимым внутри opaque dense Shade, не меняя world/projected anchors; силуэт/HP не перекрываются большим эффектом. Малый prewarmed pool, без dynamic lights/bloom/новых packages. Reusable отдельный класс для будущей FLAME-интеграции; generic production AttackBeamView пока прежний, другие стихии не перекрашиваются.

## Регрессионные сценарии до реализации

- ART21-01: before/after front/side/behind при прежних позициях ART.1.1 и camera/scale; occluded silhouette виден и не меняет открытый силуэт. Отдельный pixel-mask probe оценивает долю видимого тела, не заменяя кадры.
- ART21-02: synthetic12 Shade/.48m (4×3), реальный ratio .5/.6/.7/.8; после layout нет пересечений HP rectangles; повторный layout идентичен, изолированная полоса возвращается к якорю. Линия привязки заканчивается у исходного якоря. Sweep на нескольких resolutions.
- ART21-03: реальный saved combat с advancing frames/ticks/AttackEvents. HP от Domain, VFX origin из фонаря и end из события. Сравнимое video до/после, отдельно onset/impact/decay stills; normal-speed просмотр обязателен.
- ART21-04: Pause замораживает VFX/HP/clock; Resume без catch-up; expiry/restart/scene reload/Continue/death не оставляют полос/следов, не повторяют попадания. Pool count ограничен, после повторных циклов объектов не больше.
- ART21-05: общий HP view проверен в production Battle, включая Continue и удаление врагов. Layout не меняет fill ratio, состояние боя, rewards/event consumption.
- ART21-06: static38/38, полные доступные EditMode/PlayMode, compile/Console, diff, GUID/meta/import/reference/protected-source checks. Historical ART.1/1.1/2a неизменны; чистый checkout и tracked hashes нового пакета. Независимый source/visual review, PR Refs73 до полного acceptance; self-merge/release запрещены.

Риски: occluded pass может спорить с огнём; высокая стопка HP может терять связь с врагом; VFX может выглядеть линией даже с другим цветом. Оценка по игровому масштабу и нескольким атакам, synthetic probes явно отделены от настоящего боя. Editor не подтверждает iPhone readability/performance/G2/готовность полного VFX.1.
