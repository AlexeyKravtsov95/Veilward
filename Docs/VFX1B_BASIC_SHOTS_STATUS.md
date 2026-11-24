# VFX.1b / #91 — три basic attacks и status cues

База: origin/main `8a9e3cb172ab6abe55efafbbf1232a2a5b53912d` (merge94), существующий checkout/Library. Продуктовый scope и самостоятельный выбор композиции утверждены #91; без новых worktree/clone/projectcopy/iPhone, без изменения gameplay.

## Спецификация до implementation

TIDE: летящая замкнутая капля, малый всплеск и расширяющаяся тонкая рябь. GALE: движущийся поворачивающийся серп с коротким вторым росчерком, impact из расходящихся воздушных дуг. STONE: вращающийся угловатый осколок с коротким следом, impact из трёх расходящихся сколов. Никаких source→target beams для basic types. Полёт .20/.14/.24s, impact .16/.14/.16s; только косметика после authoritative damage. FLAME/pool8/тайминги/арт не меняются; STEAM/STORM/legacy остаются прежними.

Итоговая композиция STONE (24.11.2026): вместо запланированного короткого следа — внутренняя грань летящего осколка; impact имеет два расходящихся скола. Это самостоятельный минимальный вариант в пределах #91, без дополнительного renderer/шлейфа. Исходный замысел выше сохранён как история спецификации.

`BasicProjectilePool`: общие 12 prewarmed slots для трёх новых типов, 4 LineRenderer на slot (48 renderers/material slots), один существующий shared ReactionEffect material с PropertyBlock; first-free/oldest overflow без очереди. Captured AttackEvent/type/level/source/target не держит Transform. Routing только authoritative TowerId/definition/level; finite bounded ShotSource правильного binding или cell+.85m. Потребление после каждого Step до finally clear четырёх streams; periodic Burn/ability/reaction не создают shot.

Burn/Wet/Gust/Crack: маленькие animated угольки / капля с рябью / вращающиеся серпы / угловатая трещина внутри существующих двух slots. Два prebuilt uGUI graphics на EnemyStatusView, без world/body overlay и новых badges. Один owner — уже существующий live EnemyId/view, capacity ровно2 на owner, без очереди/словаря истории. Общее количество graphics =2×live enemy views, ограничено validated spawn budget текущей волны; новые statuses сверх двух не рисуются согласно прежнему Describe. Text/inspector/ready-pair/reaction cue priority сохраняются; graphic не занимает slot реакции. Layout включает bounds графики. Refresh/expiry/replacement меняют графику немедленно по snapshot, arrival shot на неё не влияет. При kill/leak/reset исчезает вместе с owner, ID reuse создаёт чистый owner. Без Instantiate/material instances/AddComponent на refresh.

Clock — существующий active presentation time/delta: Pause/background/skip-next-delta freeze всей geometry, resume без burst; вне Combat shots затухают. Restore/Retry/Continue/menu/unload соблюдают existing ART5 hit/death/tails16/.6s lifecycle. Никаких изменений Domain/config/balance/schema/saves/camera/18models/ART4/ART5/packages/render features.

## Acceptance / regressions

| ID | Проверка |
|---|---|
| VFX1B-01 | TIDE/GALE/STONE L1–L3: authoritative routing/anchor/fallback, отсутствие basic beam, unchanged FLAME/legacy/hybrids. |
| VFX1B-02 | Immediate HP/status/reward; captured lethal/removed enemy/source, multiStep/repeated consumption, no shot от Burn/ability/reaction-only damage. |
| VFX1B-03 | Различные moving shapes/impact, cap12/48 renderers/1material, saturation/reuse/expiry/reset metadata и стабильные object/material IDs. |
| VFX1B-04 | Четыре формы по live snapshot; apply/refresh/expiry, все4 synthetic и пары, ready/reaction priority, максимум2slots, inspector unchanged, kill/leak/IDreset. |
| VFX1B-05 | Actual production Update motion/impact/expiry; Pause/background freezes actual geometry, skip/resume, Continue/file/Retry/reset/menu/unload без ghosts. |
| VFX1B-06 | Полные fresh FLAME/GALE traces до/после побайтно равны; earned NewGame/unlock/place/upgrade/ability/Continue/Sell без setters HP/gold/position. |
| VFX1B-07 | Actual PlayMode PNG launch/intermediate/impact всех3,4cues/priority/mixed/dense/boss/HP/pause/Continue. Непрерывное production Update видео всех3/cadence/status transitions. Test clock/synthetic отдельно. |
| VFX1B-08 | Static/fullEdit/fullPlay/compile/meta/GUID/refs/fullbuilder/diff; actual loaded inputs, source/media heads; strict read-only verifier/малые negative controls; PR и independent code/visual review. |

## План реализации и проверки

1. Зафиксировать fresh before traces и carry-in SHA; добавить regression tests routing до нового consumer (RED).
2. Добавить узкий BasicProjectilePool/View и typed slot graphics; подключить active clock/reset/consumer и builder/validator. Сохранить GUID/legacy serialized fields. Импортировать через текущий Editor.
3. Focused Edit/Play tests: captured state, cap/reuse, формы/priority/clock/cleanup; заменить только superseded basic beam expectation. Сверить fresh after traces.
4. Полные gates последовательно с exact restoration только task-generated builder bytes и сохранением carry-in; собрать continuous Update video и дополнительные явно подписанные earned/synthetic PNG.
5. Зафиксировать manifests/verifier/controls, VERIFIED/NOT VERIFIED; создать PR Closes91/Refs47, независимый code/visual review, исправить замечания без merge.

Риски: saturation может скрыть старую косметику; графика может пересечь label/HP; неверный anchor при Upgrade; ghost owner после Continue; ложная realtime cadence от test clock. Эти границы проверяются тестами и actual media. Device Deferred QA53/54/55; parent47 до owner merge остаётся1/4, #92/#93/G2/M0/release отдельно.
