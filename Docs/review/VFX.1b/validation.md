# VFX.1b / #91 — evidence

## VERIFIED

База `8a9e3cb172ab6abe55efafbbf1232a2a5b53912d` (merge94); tested runtime/tests `923ed7c63b6a828b32954a489c2d2f49d01b8b5b`. Существующий checkout/Library, один владелец Unity runs; новых worktree/clone/projectcopy нет. Unity6000.3.28f1, macOS Editor/Metal/Mobile. Domain/config/balance/content/schema/saves/camera/packages/ART3–5 model sources не меняются; два enemy prefabs получают только ссылки/дочерние UI graphics существующих slots.

- `python3 ArtSource/Review/run_vfx1b_gates.py` последовательно запускает `./Scripts/run-static-checks.sh`, `./Scripts/run-edit-mode-tests.sh`, `./Scripts/run-play-mode-tests.sh`: **38/38,1283/1283,98/98**, failed/skipped/inconclusive0. Raw command/per-case reports — `reports/`. Full builder дважды воспроизводит refs/content. Audit748inputs: unexpected0/newInputs0, exact restoration всех исходных bytes, detach/refresh PASS. Четыре rendering carry-in файла не коммитились и не переавторены.
- Fresh FLAME/GALE production full traces до/после равны побайтно (2371375/2078626 raw bytes), whole JSON reports одинаковы, оба Victory8/REG214. SHA и сравнение — `reports/replay-equality.json`. Это новые runs, старые totals не копировались.
- Focused Edit34/34 и Play2/2. Новый routing regression RED3/3 обнаружил старые beams; status RED2/2 обнаружил отсутствие графики. Тесты всех3типов×L1–L3: captured authoritative ID/level/source/target, bounded fallback, lethal removal/reward once, source removal, несколько Steps/repeated consumption, finally clear streams, Burn-only без shot; stable objects/materials/cap/oldest reuse/expiry/reset.
- Реальный Update двигает и завершает все3семейства. Pause/background замораживает реальные vertices LineRenderer и CanvasRenderer status mesh; первый resumed delta пропущен, затем motion/impact/expiry продолжаются. Continue/Retry/file/menu/restore очищают shots/owner IDs, существующие ART5 feedback/tails tests сохранены. Старый FLAME suite сохранён; заменены только3superseded generic-beam cases.
- Status графика: typed выбранные kinds из прежнего Describe; apply/refresh/expiry, все6пар, all4, ready Steam/Storm и transient reaction priority; text/inspector/layout без добавления badges. Обычный refresh не создаёт GameObject/component/material. Один owner — существующий EnemyId/view, ровно2prebuilt graphic meshes на owner, без новой очереди/словаря истории; общее число2×live views ограничено validated finite wave spawn budget. При saturation статусов работает прежний выбор двух slots. UI материал shared UI/Default; shapes≤84vertices/graphic.
- Новые атаки: общий pool12×4renderers/slots=48,1existing shared ReactionEffect material с PropertyBlock; first-free/oldest overflow без очереди и без изменения событий/награды. FLAME8×13=104/2materials не меняется. Lifetime TIDE.36s/GALE.28s/STONE.40s; flights.20/.14/.24s. Capture не держит mutable Transform. Нет Instantiate/AddComponent/Destroy/material instances на Play/Advance.

Видео:458actual composited GameView frames,21.232s encoded PTS, timeScale1/captureFramerate0, без склеек/изменения скорости. Максимум4одновременных новых shots и5live status owners. Earned GALE→STONE→TIDE file Continue8, combat/pause/resume/rollback/Sell; whole state+Grid и durable file bytes совпали. Дополнительный earned FLAME→TIDE→GALE test-clock run достигает exact Victory8, dense6/boss/ability/death и public consolidation UpgradeL3. Synthetic all4/pairs подписаны отдельно; actual input snapshot48new+104FLAME renderers,8graphics/4liveowners,115loaded dependencies,URP Unlit7available shader passes (не7draws/effect), никаких device cost claims.

[Actual media и условия](gallery.md), [provenance/параметры](provenance.md). Exact media/harness heads, loaded shader/material/package dependencies и timestamps фиксируются manifest/reports; read-only verifier сверяет Git/protected/source/artifact bytes, полные suites, replay, video PTS и source PNG attribution. Малые in-memory controls не создают project copy. Личные TXT/pycache не prerequisite; сохранность проверяется отдельным `--check-local-preservation`.

## Сохранённые неудачные attempts

Диагностические ранние compile/filter reports сохранены в `reports/`: исправление signature тестового fixture, qualify Text, неподдержанный OR-filter с Total0 (не PASS).

`attempt-1-gates`:1282/1283, единственный fail полного builder — одинаковые имена дочерних cue. Исправлено уникальными slot names, assertion не ослаблен; новый полный run выше зелёный. Точные исходные bytes восстановлены и после failed run.

`attempt-2-wire`:0frames; media harness использовал диагностический JsonUtility JSON, потеряв immutable ability. Production validator отклонил checkpoint; harness переведён на штатный Store/wire без DTO edits.

`attempt-3-rollback`: кадры реального Update, но слишком поздний Continue после завершения wave7 не совпал с Preparation7. `attempt-4-terminal`: wave8 успела победить до запланированного прерывания. Эти clips не выдаются за успешный rollback proof; raw receipts сохранены, исправлено окно capture. Runtime при media corrections не менялся.

## NOT VERIFIED / DEFERRED

Physical iPhone Deferred to prerelease QA #53/#54/#55. Player IL2CPP/Metal, native input/background/safe area, device frame pacing/thermal/memory, native video/export/signing/TestFlight не запускались. Запись Editor с overhead не является performance benchmark. Human ART/G2/balance/release approval не заявляются. Parent47 остаётся1/4 до owner merge; #92/#93/#48/M0/release отдельно. Self-merge не выполняется.
