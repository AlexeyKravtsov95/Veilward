# VFX.1a / #90 — evidence

## VERIFIED

Runtime/tests проверены на `98cb553868a86a23e65b72f46c2077be4a546faf`, от main merge89 `45efa5676ac5c27ee70d4a13b1fbe2622d3f82f6`. После этого менялись только evidence tools/docs/media; runtime/test bytes закреплены manifest и Git. Unity6000.3.28f1, macOS Editor, Metal, Mobile,1600×900. Никаких новых worktree/clone/projectcopy/iPhone.

- `python3 ArtSource/Review/run_vfx1a_gates.py` последовательно запускает `./Scripts/run-static-checks.sh`, `./Scripts/run-edit-mode-tests.sh`, `./Scripts/run-play-mode-tests.sh`: **38/38,1252/1252,96/96**,0failed/skipped/inconclusive. Raw full per-case reports рядом. Все736 входов Assets/Packages/ProjectSettings восстановлены exact после generated builder bytes, unexpected/newInputs0, Editor detach/refresh PASS. Это новые runs, не исторические totals.
- `git diff --check`; final compile completed/failed:false; meta/GUID/serialization/material references и full builder reproduction проверены full suites. Domain/config/balance/content/schema/saves/ART3–5/packages/shaders не меняются; protected Git blobs сверяются verifier.
- Свежие полные FLAME/GALE `AbilityBossReplayTests` до/после: whole JSON и распакованные event/state traces совпадают побайтно, natural Victory8/REG214 сохранены. Ни одного HP/gold/tick setter в earned proof.
- Первый regression RED1/1 обнаружил generic FLAME beam; focused Edit14/14 и Play3/3, затем final Edit включает15 новых случаев. Saturation RED обнаружил одинаковые timestamps и переиспользование только slot0; consumption sequence исправил oldest policy, final saturation/reuse/reset PASS.
- 8 prewarmed views ×13 renderers/slots =104,2shared material assets FlameImpulse, без новых material instances/GO/LineRenderer/AddComponent/Destroy на shot/Advance. Actual loaded inputs/dependencies/ref hashes сохранены в actual-render-inputs.json. FLAME интервалы L1/L2/L3:30/27/25ticks (1/.9/.833s), effect lifetime.367s. Для до8 синхронных FLAME одного slot/tower достаточно; при произвольной плотности oldest заменяется немедленно, без очереди и изменений Domain. Synthetic18shots в одном кадре сохраняют последние8 при стабильных object/material IDs. Video active pool максимум1; это не device budget.
- [Видео](../VFX.1a/editor-flow.mp4):639 actual composited GameView frames,22.331s capture/22.281s encoded timeline, variable original PTS, speed1/splices0. Production Update, Time.timeScale1, без test-clock/captureFramerate. Fresh NewGame150/20 →FLAME L1→pause frozen geometry→Resume без catch-up→ability→earned L2(105gold)→Combat2→menu/Continue whole snapshot+liveGrid→Sell. L1/L2 launch/flight/impact PNG скопированы exact source SHA. Видеозапись overhead не benchmark.
- Отдельно earned script FLAME→TIDE→GALE проходит public commands/ability до exact Victory8; dense5/boss HP/status/inspect/ability/death и L3 — actual end-of-frame composited GameView. Явный test clock fixed30Hz/≤120ticks/render. Для L3 полностью восстановлен earned Preparation8, обычные Sell остальных башен и Upgrade105+140 оставляют67gold; сняты launch/.1s intermediate/.21s impact при неизменном Domain snapshot. Никаких grants/редактирования checkpoint DTO. Editor-only runInBackground override для unfocused probes восстановлен false, captureFramerate0, Play Mode остановлен.
- Pause/background/resume actual geometry/expiry, lethal/removed source/target, authoritative ID/level/anchor/fallback, multiStep/repeated consumption, capacity/reuse/overflow, reset/restore/menu/unload — meaningful tests. Existing earned Continue/Combat7–8/Retry/UI/file suites дополнены проверкой чистого FLAME pool.
- Все7 исходных чужих файлов сохранили SHA;4rendering files не закоммичены. Default delivery требует actual4, private TXT/pycache проверяются только explicit preservation.

## Сохранённые неудачные/заменённые proof attempts

`attempt-1`: ошибочная гипотеза бюджета для earned L3 к волне3, честный FAIL. `attempt-2`: completed video, но наложение старого task recorder label; заменено свежей записью с cleanup. `attempt-3-probe`: harness вызвал safe-only Wire в Combat, исправлено на full Snapshot. `attempt-4-offscreen`: functional PASS, immediate Camera.Render дал stale URP geometry; визуальные кадры заменены реальным end-of-frame GameView. `attempt-5-warmup`: capture до Battle.Start; добавлен wait. `attempt-6-ui-refresh`: functional PASS, stale UI overlay на первом dense кадре; перед capture добавлен обычный render frame для UI Update. Runtime при этих исправлениях не менялся; старые receipts/кадры не выдаются за final proof. Raw frame directories task-owned, ignored TestResults; projectcopy не создавался.

Final new-error cursor после corrected probes не содержит новых errors. Старые expected suite errors и перечисленные harness failures сохранены; затем Console очищена и финальная ground-truth0 зафиксирована отдельно. Это не скрытый пересчёт старых ошибок как PASS.

## NOT VERIFIED / DEFERRED

Device Deferred to prerelease QA #53/#54/#55: физический iPhone, player Metal/IL2CPP, реальный touch/background/safe area, thermal/memory/frame pacing. Нет export/signing/native benchmark/TestFlight. Human ART/G2/release и остальные три пакета #47 отдельно. Parent47 остаётся0/4 до owner merge. Никакого self-merge/release.

Independent code/visual review и финальный committed read-only delivery audit публикуются отдельно.
