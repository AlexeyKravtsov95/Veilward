# R2 — результаты исправления #76 / PR77

Reviewed input `31369a208109d1c0eddf16ffb3c62d21e0d6a0b1`. Tested source `f890a51c9df2f506a46bbe3f10f5f2607368e319`. Оба native exports сделаны из `ff6a333940e707366bdf6b8df42a12f311d3e143`; последующие commits меняют только этот evidence package. Runtime inventory Assets/Packages/ProjectSettings:580 файлов, одинаковые bytes в tested source, обоих exports и текущей поставке. Historical R1 остаётся неизменным, кроме ссылки README и входа verifier на R2.

## VERIFIED — source и Editor

L2 получил открытую раму поперёк игровой камеры и две крупные боковые чаши. L3 наследует раму и сохраняет собственный венец/раздвоенное ядро. Это geometry delta, без uniform scale, расширения исходной крыши, новых textures/material palette/packages/lights. L1 .blend/FBX и ART1 bytes сохранены. Все708 исходных vertices/крыша сохранены в derived geometry; ground0, XY±.47m. В средней полосе высоты .8…1.65m проекция ширины L1=.93338m, L2/L3=1.25158m; этот regression guard дополняет реальные кадры, художественное approval не заменяет.

Source triangles1275/1599/1755; imported nondegenerate1256/1580/1736. Blender/Unity одинаково сохраняют19 inherited zero-area tessellation triangles до отбрасывания importer; это отражено в geometry manifest. Каждый model mesh2material slots, budget≤5000. Mask — существующий дополнительный renderer, structural2renderers/4slots на prefab; это не measured draw calls.

Реальный reexport всех трёх .blend даёт равные before/after geometry, GUID/local IDs, UV/normals/material dependencies, prefab/scene hashes. После full suites восстановлены только task-generated serialization deltas; final files вновь сверены с roundtrip snapshots. Domain/Infrastructure/configs/scenes/packages/project settings/ART1 и прежние review evidence защищены verifier относительно baseline7dfdf0e. Binder/gameplay/экономика/saves/preview/selection не менялись в R2.

Полные финальные suites: **EditMode1126/1126, PlayMode77/77**, failed/skipped/inconclusive0. Применён штатный строгий JSON validator из run-unity-tests.sh к completed JSON обоих suites. Static38/38 и project checks PASS; compile0errors; diff whitespace PASS. Historical unused variable CS0219 в BossGreedySearchTests при полной compilation не связан с R2. Первый Edit run обнаружил ошибку новой test fixture (unsaved additive scene); fixture заменена NewPreviewScene и весь suite запущен заново. Ранний неуспешный run не считается PASS.

[PlayMode](playmode/) — actual1600×900 captures. Singles/lineup/front/side/behind подписаны SYNTHETIC. Natural test flow использует public commands/earned budgets; его ускоренный test clock явно подписан и не выдан за iPhone real time. [Trace](reports/natural-trace.txt), [checkpoint](reports/natural-L3-checkpoint.json), Console0unexpected.

## VERIFIED — физический iPhone / production77

iPhone16 Pro (iPhone17,1), iOS27.0.1, landscape2622×1206. Unity6000.3.28f1/URP17.3.0, Unity Development OFF. Xcode Debug/local development signing, codesign strict/deep и install exit0. Production receipt содержит только Bootstrap/MainMenu/Battle; art processor срабатывает только для iOS ArtReference export и не меняет saved scene/production route. No archive/distribution/TestFlight.

Перед replay восстановлен **побайтно неизменённый earned L1 checkpoint R1**, SHA256 `117a0fe9dfa346aaff177776235a48039cd30b435fee6af69311b26a7411cfef`. Он получен прежним реальным прохождением, содержит wave7 Preparation/tick2815/HP7/kills32/Gold282/TowerId1,cell(2,1),L1/invested70. Readback после copy совпал. Это replay настоящего checkpoint; новый полный fresh run1…6 на77 не заявляется. Live Gold/HP/DTO не редактировались. Предварительная попытка backup primary после install77 вернула CoreDevice7000/file node; listing Documents был пуст. Успешный backup до первого restore не заявляется; исходный R1 earned checkpoint и прежнее final state257 сохранены в истории.

[Непрерывное native видео Upgrade](device/upgrade-native.mp4): L1→UI Upgrade105→L2Gold177/invested175→UI Upgrade140→L3Gold37/invested315, затем Start Wave7 и advancing бой. TowerId/cell/cooldown сохранены. **Одинокая L3 проигрывает wave7:** итог tick3334/HP0/Gold47/kills34; это наблюдаемый terminal outcome, не успешная стратегия. Ещё одна ранняя попытка ограничить бой паузой тоже дошла до Defeat из-за задержки между tool calls; её terminal/paused PNG не используется как доказательство advancing dense combat.

После terminal Defeat app был завершён; для отдельного повтора из локальной read-only копии `upgrade-L3-inspect.json` восстановлен **неизменённый earlier-earned L3 checkpoint самого77** (tick2815/HP7/Gold37). Обратное копирование подтвердило полное byte equality. Это явно подготовленный replay: обычный Continue после terminal Defeat не выдаётся за возврат к живому HP7. Затем был cold launch и обычный Continue. [Второе непрерывное видео](device/combat-rollback-native.mp4) показывает actual advancing wave7 →8active enemies/HP7/Gold42/kills33, Burn badge, HP bars и открытый L3 Inspect/MAX/range3.2. [Кадр из native video](device/combat-L3-inspect-video-frame.png) — tick3059; live UI также наблюдал tick3075, Pause зафиксировал3082. Затем Resume→Menu→Continue вернул точный Preparation checkpoint2815/HP7/Gold37/kills32/L3. UI Sell confirm начислил220: Gold257/towers0; следующий Continue сохранил тот же checksum. [Шесть native наблюдений/JSON](device/phone-flow.json) относятся к safe checkpoints, а не live combat; live ticks доказаны видео/HUD.

Обе записи получены native iOS Screen Recording, импортированы через Image Capture. R1 transfer blocker восстановлен; старый R1 MP4 не используется для новой L2. Публичные clips непрерывные1×, без splices/подмены игровых pixels. Удалены только временные OS lead-in/tail; notification в начале второй raw recording исключён trim. Source/output hashes, trim commands, metadata, frame counts/PTS — [video proof](device/video-proof.json). CoreDevice PNG не редактируются; video frame подписан отдельно. Share/recording overhead этих visual clips не используется как perf baseline.

## VERIFIED — matched art78

[Методика и результаты](performance.md), [raw comparison](device/matched/comparison.json). Четыре fresh запуска одной signed art78, selector ART1/L1/L2/L3, одинаковые camera/quality/dense12/enemy HP/projectile/sampling. Screen Sharing/recording/xctrace OFF во время каждого5s+30s×3. Отдельный Activity Monitor20s после timing windows для native memory; extractor публикует только numeric samples одного game PID. Partial visual runs не подмешиваются в completed timing runs.

[Native front/side/behind каждого уровня](device/occlusion/capture-settings.json) — **явно SYNTHETIC**: назначенные позиции/HP, frozen Domain. Dense12 — тот же controlled workload, не production wave. Actual Battle8enemy/Burn/Inspect в production77 описан отдельно выше.

## NOT VERIFIED / отдельные gates

Утверждение художественного результата владельцем и повторная PR приёмка остаются открыты. Локальная iPhone model/readability/economy/perf проверка не закрывает всю #44 ART direction, QA.1/#54, broad device matrix, long-session thermal, human multi-touch или release. Generic Battle beam остаётся до#47. Player-loop execution, actual draw-call/overdraw/VRAM counters не выводятся из structural renderer/slot counts, frame pacing или Activity Monitor. Недоступные counters перечислены в performance.md; это не zero-cost PASS.

После art78/visual probes production77 вновь установлена/запущена; readback заработанного final checkpoint совпал с sold-continue.json (Gold257/towers0).

Оставить #76/#44 открытыми, PR77 Draft/Refs до повторной приёмки; self-merge/release не выполняются.
