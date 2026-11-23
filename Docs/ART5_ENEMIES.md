# ART.5 — Shade и Страж Разрыва / #46

База cb9101c (merge88). Существующий checkout/Library, отдельная ветка codex/art5-enemies, один владелец записей/Unity. Исходные7 carry-in files сохраняются; личные TXT/pyc не являются delivery dependencies.

## Scope и выбранная форма

Shade: парящий узкий индиго плащ со светлой холодной кромкой, ivory маской, раздвоенным венцом и двумя независимыми лоскутами. Высота примерно1.35m/ширина.7m. Страж: самостоятельная широкая форма с броневыми наплечниками, крупной маской, разомкнутым ритуальным венцом и парными подвесами; высота примерно1.8m/ширина1.4m. Различие силуэта не зависит от масштаба/цвета. Shared ART1 palette/Surface/Glow; никаких новых textures/packages/features/lights. Исторический ART1 source/showcase неизменен.

Редактируемые original Blender sources в ART5; rigid multipart articulation уместна для плавающих духов/сегментированной брони, skinning не нужен. Пять содержательных clips Idle/Move/Hit/Death/Leak на модель; FBX baked transforms,5 Unity legacy clips, runtime explicit SampleAnimation от paused presentation clock. Source meters/ground pivot/UV/normals/export axes проверяются реальным readback. Shade≤5000tri, boss≤15000tri,≤2уникальных shared slots/model; masks отдельно учтены.

## Runtime contract

EnemyDefinitionId==rift-warden выбирает optional bossPrefab; unknown/shade/missing ref используют прежний enemyPrefab. Authored boss root scale1, только legacy fallback boss получает1.4. Shade prefab GUID/HP/status refs прежние. Capsule заменена authored child, runtime root неизменно Y=.6; authored orientation компенсирует root scale: Shade localY=-1/scale1.666667, boss localY=-.6/scale1; world base offset=-.6 и actual feet hover задаются mesh. Вращается только authored orientation parent по path tangent, UI/root не вращаются. Root motion/physics отсутствуют.

Per-step damage consumer до ClearDamageEvents отражает все Direct/Burn/Steam/Storm/Ability, несколько hits одного кадра схлопываются в один краткий Hit без наград/Domain mutation. Per-step snapshot synchronization не теряет lethal/leak при8Steps/frame. Removal немедленно убирает selectable/HP/status view; только authored child продолжает Death/Leak до.65s, максимум16tails, без блокировки Victory. Pause замораживает clocks/tails; успешный restore/restart/menu/unload очищает их синхронно.

Каждая rigid mesh часть имеет один shared cue, parented к animated transform. Mask повторяет любую pose, не bind pose. Build/Bind идемпотентны: body не выбирается по первому MeshFilter HP. HPanchor2.0/status2/inspector/reaction/boss HUD сохранены.

Domain/config/content/schema4/economy/spawns/IDs/cooldowns/ability неизменны: production600/5, historical v1 1000/5, legacy routing. Exact FLAME/GALE before/after full traces.

## Regression scenarios до реализации

| ID | Acceptance |
|---|---|
| ART5-01 |2editable sources/2FBX/10clips, geometry/UV/normals/materials/axes/pivots/animation totals и actual reexport/readback exact; original provenance.|
| ART5-02 |Saved authored refs до Build; repeated Import/Build/Bind: один model/cue/owner, Shade GUID/controller/UI прежние, все18tower refs/ART4 сохранены.|
| ART5-03 |ID-based boss selector, known/unknown/missing/legacy fallback, нет double scale; root Y=.6, animation/tangent только child.|
| ART5-04 |Direct/Burn/Steam/Storm/Ability hit/lethal feedback, multi-hit/tick, spawn→kill/frame; reward/kill ровно1, leaks/Defeat/Victory не задерживаются.|
| ART5-05 |Idle/Move/Hit/Death/Leak pose changes; pause/resume clocks;≤16tails≤.65active seconds; restore/restart/menu/unload безghost/UI/old clocks.|
| ART5-06 |Animated multipart masks соответствуют pose; projected HP2.0/status2/ratio/inspection работают обоим; production camera singles/pair/dense/behind towers/gate/heart.|
| ART5-07 |Earned NewGame→boss8→ability/death, public commands/file/menu Continue/full snapshot+Grid Combat rollback; exact FLAME/GALE traces до/после.|
| ART5-08 |Новые full Static/Edit/Play/compile/meta/GUID/import/diff; реальные PlayMode PNG и непрерывное production Update video, actual loaded inputs; strict read-only delivery/малые controls; independent review.|

Риски: multipart pass count/HP overlap/tangent180/import coordinate bake/deferred Destroy. Доказательства разделяют synthetic probes/test clock/production Update. Device Deferred to prerelease QA #53/#54/#55. #47VFX/#48UI/G2/merge/release отдельно. VERIFIED/NOT VERIFIED фиксируются только по реальным runs.
