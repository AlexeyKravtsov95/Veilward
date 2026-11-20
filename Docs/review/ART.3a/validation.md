# ART.3a — результаты

20.11.2026. База PR75 `7dfdf0ec1bc1ef63e6df76810cfd08ad6de5181f`. Exact tested source фиксируется в manifest и verifier. Unity6000.3.28f1/8f082d2fe476, URP17.3.0, Pipeline0.10.0-exp.1, UnityCLI1.0.1, Blender5.3.0/02f0899d78e3, macOS/Apple Silicon; Editor target iOS, render Metal. Ни версия Unity, ни packages не менялись.

## VERIFIED

- Static self-tests **38/38** и Unity project checks PASS; `git diff --check` PASS. [Полный static output](reports/static.txt).
- Полный EditMode **1123/1123** и PlayMode **75/75**, ноль failed/skipped/inconclusive; [case reports](reports/). PlayMode запущен напрямую тем же Unity Test Runner после startup-проблемы shell preflight; полученный completed JSON проверен точным Python validator из штатного runner. Пустые async startup responses не засчитаны. Новые12 EditMode и1 PlayMode cases покрывают import/budgets/palette/fallback/scale/occlusion/level/lifecycle.
- Compile без errors; исторический CS0219 в BossGreedySearchTests раскрыт в [compile report](reports/compile.json). [Scoped natural scenario Console](reports/console.txt) — без unexpected ошибок; [последовательность real-time](reports/motion-console.json) после явного Console clear:0errors/0warnings. Это не утверждение о пустой глобальной Console за весь suite: старые fault-injection tests намеренно логируют ожидаемые ошибки.
- Blender source checks:708 исходных vertices, включая151 roof vertices, сохранены точно; ground0, footprint.94×.94, applied transforms, packed shared palette, два slots. Source-only zero-area faces у концов исходного огня удалены. Ещё19 нулевых tessellation triangles исходных n-gon caps последовательно отбрасывает Unity importer: они отдельно посчитаны, source/import counts не смешиваются.

| Уровень | Blender tessellation tris | Ненулевые / Unity imported tris | Slots | Высота |
|---|---:|---:|---:|---:|
| L1 |1275|1256|2|2.285m|
| L2 |1463|1444|2|2.285m|
| L3 |1619|1600|2|2.505m|

- Все counts ниже5000; крыша/основание не расширены. [Source geometry manifest](../../../ArtSource/Blender/ART3/geometry-manifest.json). Все3 сохранённых `.blend` действительно повторно экспортированы проверенным preset; Unity GUID/local IDs, vertices/normals/UV/indices, material slots/dependencies и prefab/scene bytes совпали до/после: [before](reports/reexport-before.json), [after](reports/reexport-after.json). Финальный roundtrip выполнен после нормализации serialization whitespace.
- TestTower по authoritative ID/level подключает FLAME child; повторный Apply не создаёт root, старый child отключается до deferred Destroy. Корневые position/scale/collider, TowerId, selection/range прежние. Shared palette не получает type-tint; остальные6 типов/legacy и graybox preview сохраняют fallback. Builder повторяет binding и проходит строгий two-consecutive-build byte check.
- Локальный stencil cue из #73 сохраняет корпус и HP за FLAME; HP child повторяет настоящий fill transform/ratio/цвет. Дополнительный mask renderer имеет два submeshes и может требовать два draw submissions; body/HP добавляют3 cue renderers на врага. Новые shaders/features/packages/lights/bloom отсутствуют. Это структурный audit, не измеренный draw-call/performance PASS.
- Natural proof: New Game150Gold/20HP, существующий маршрут P2.4f первых6 волн,41kills/16HP/145Gold после волны6. Законный Sell башен2–5 даёт327Gold; Upgrade105→222Gold и Upgrade140→82Gold. Repeated Continue каждого уровня, interrupted Combat→Preparation tick2589/Gold82/HP16, rejected UpgradeMAX/repeated Sell, ResetPresentation и New Game без дублей проверены. Никаких правок live Gold/HP/DTO. Ускоренный test clock явно подписан; старые presentation effects очищены ResetPresentation перед уровневым comparison.
- Отдельно обычный Battle.Update из неизменённого file checkpoint:12captures за7.7109s, ticks2590→2822, kills41→42, gold82→87, sample10 содержит beam. Capture overhead и unconstrained Editor frame rate не выдаются за performance. [Camera/light settings](playmode/settings.json) одинаковы для synthetic comparison;1600×900, position(8,11,8), ortho8.5, scale1. Реальные UI/camera frames не являются generated mockups.
- Domain/Infrastructure/configs/content600/armor5/schema4/saves/packages/ProjectSettings/ART1/history не изменены. Собственные автоматические import/builder changes сцен/материалов восстановлены; `.meta` создавались Unity, GUID не генерировались вручную. У whitespace-normalization нет семантического delta.

## NOT VERIFIED / BLOCKED

- **Physical iPhone**: [inventory](reports/device-availability.json) показывает paired16Pro/iOS27.0.1, tunnel unavailable, DDI false. Нового export/signed build/install/device frames/recording не было. Editor и прежний ART2b player не являются device proof этих моделей. Human touch/safe area/lifecycle matrix, другие устройства, IL2CPP compilation нового source также не проверены.
- Новые CPU/GPU/frame intervals/memory/drawcalls/overdraw/thermal measurements отсутствуют. Принятые [ART2b guardrails](../ART.2b/validation.md) относятся только к прежнему одинаковому microbenchmark: их нельзя объявить cap всего Battle или PASS для новой линейки. Сопоставимого device performance run для L1–L3 нет; структурные budgets выше его не заменяют.
- Нет normal-speed continuous видео этой линейки;12PNG подтверждают advancing state, но не плавность. Полная dense/status visual matrix и device различимость L1/L2 ещё требуют проверки. Reviewer отметил более слабое различие L2 от L1 как рекомендацию для iPhone gate.
- Финальное художественное approval L1–L3 владельцем не получено. Только FLAME model integration; final VFX/projectile integration #47 и пять других семейств #44, карта/враги/UI/G2/release отдельно. Draft/Refs76, без Closes76/44, self-merge/release/TestFlight не выполнялись.

## Исправленные локальные итерации

Первый natural test helper попытался экспортировать Combat checkpoint (1failed), после исправления полный PlayMode75/75. Первый полный EditMode1119/1123 выявил unstable prefab localIDs от одинаковых cue names, два устаревших FLAME brightness assertions и слишком строгий Color equality; затем1122/1123 оставил один float compare. Уникальные names устранили реальный serialization defect без изменения strict builder test. FLAME assertions теперь проверяют тип/level mesh/palette, остальные типы сохраняют прежние brightness checks; экономические/range/UI/golden assertions не ослаблены. Допуск Color1e-6 учитывает serialization precision. Последний полный run1123/1123. Startup preflight failures и неудачные compilation/codec вызовы capture helper не считаются test PASS; final capture helper реально скомпилирован/исполнен. Cyan shader placeholders и stale trails первых проб в финальный пакет не включены.
