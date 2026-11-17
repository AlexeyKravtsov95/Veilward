# P2.3e — native EightWave: фактический результат

**VERIFIED:** test-only проверка production factory/public commands/реальных menu/controllers/file slots. **P2.3 НЕ DONE:** playable/proc acceptance после6 и Combat7 **FAIL/BLOCKED**, review/merge ожидаются. Gameplay, баланс, конфигурации, packages и арт не изменены.

База: merged PR #30, `c1546c3282d9b5ce4bf67aa0465ac9138330c8cf`. Проверенный source commit: **`79bf582b469e9b41c937bf4948154d3278a0a878`**; последующие изменения только docs/evidence. Unity6000.3.28f1 (8f082d2fe476), macOS27.0.1/arm64, Editor Mono, active buildTarget iOS. Player/IL2CPP/Metal/device не запускались. Rules `wave-unlocks-v1`, content `eight-wave-unlocks-v1`; fixed30Hz, RNG отсутствует. [Config/package hashes](replay-summary.json), [точные команды/totals/report hashes](gates.json).

## Реальные забеги

[A script](A.commands.json), [B script](B.commands.json), [Stone script](Stone.commands.json). Каждый исполнен дважды до естественного terminal; `.first.trace.gz`/`.second.trace.gz` содержат полный несжатый trace после `gzip -dc`. Сравниваются все tick/command/results/events/statuses/tower/Grid/IDs/progression, а не только terminal. Committed summary/hash проверяются golden assertion, отчёт не переписывается автоматически. [Полный JSON](replay-summary.json).

| Маршрут | Natural outcome | Tick | HP | Gold | Kills | Достигнутый выбор |
|---|---|---:|---:|---:|---:|---|
| A: FLAME→TIDE→GALE | Defeat, волна5 |2246|0|34|13| start FLAME, after3 TIDE; after6 не достигнут |
| B: GALE→TIDE→FLAME | Defeat, волна5 |2246|0|21|13| start GALE, after3 TIDE; after6 не достигнут |
| Stone: FLAME→STONE→TIDE | Defeat, волна5 |2205|0|60|12| start FLAME, after3 STONE; after6 не достигнут |

Никаких доплат, HP/wave edits, force Victory или internal simulation mutations. В A две FLAME покупаются сразу; в B вторая GALE — после1. После3 продаётся только tower2 с обычным refund49/56; покупается собственный гибрид110. TIDE60 покупается в Preparation5. Все исполненные route-покупки легальны и успешны. Команды7/8 остаются в scripts как **неисполненный суффикс**; волны6…8/их награды не созданы.

[Поиск ранней расстановки](placement-probe.txt) и [его test source](placement-probe.cs.txt): 180 комбинаций десяти legal cells, две FLAME сразу либо GALE+вторая после1, первые3 волны через public commands. Это ограниченный поиск, **не доказательство глобальной непроходимости**. Первые pilot scripts откладывали гибрид до5 и получали InsufficientGold; финальные scripts используют обычную продажу и раннюю покупку. Исторические evidence не заменены этими измерениями.

## Per-wave состояния

Phase enum в JSON: Preparation0, Combat1, Victory2, Defeat3, WaveComplete4. Все terminal-of-wave snapshots включают следующий Enemy/Tower ID, полную армию/investment/cooldown, unlocked/history/earned/spent/pending.

| Route | Wave | Phase | Tick | HP | Gold | Kills | Pair Steam/Storm | Intrinsic Steam/Storm | Earned/Spent/Pending |
|---|---:|---|---:|---:|---:|---:|---|---|---|
|A|1|WaveComplete|399|17|40|2|[0, 0]|[0, 0]|0/0/none|
|A|2|WaveComplete|828|13|70|4|[0, 0]|[0, 0]|0/0/none|
|A|3|WaveComplete|1287|8|100|6|[0, 0]|[0, 0]|1/0/after-wave-3|
|A|4|WaveComplete|1776|3|74|9|[0, 0]|[6, 0]|1/1/none|
|A|5|Defeat|2246|0|34|13|[3, 0]|[5, 0]|1/1/none|
|B|1|WaveComplete|399|16|95|1|[0, 0]|[0, 0]|0/0/none|
|B|2|WaveComplete|828|13|50|4|[0, 0]|[0, 0]|0/0/none|
|B|3|WaveComplete|1287|9|85|7|[0, 0]|[0, 0]|1/0/after-wave-3|
|B|4|WaveComplete|1776|3|61|9|[0, 0]|[0, 7]|1/1/none|
|B|5|Defeat|2246|0|21|13|[0, 2]|[0, 6]|1/1/none|
|Stone|1|WaveComplete|399|17|40|2|[0, 0]|[0, 0]|0/0/none|
|Stone|2|WaveComplete|828|13|70|4|[0, 0]|[0, 0]|0/0/none|
|Stone|3|WaveComplete|1287|8|100|6|[0, 0]|[0, 0]|1/0/after-wave-3|
|Stone|4|WaveComplete|1776|3|45|9|[0, 0]|[0, 0]|1/1/none|
|Stone|5|Defeat|2205|0|60|12|[0, 0]|[0, 0]|1/1/none|

WaveComplete3 snapshot снят **до Confirm**, поэтому earned/spent1/0 и pending after-wave-3; confirmed snapshot и Preparation4 находятся в файловых audits. После Confirm1/1/pending none, Gold/HP/Kills неизменны. Второго credit в этих забегах нет.

## Экономика и реакции

Receipts в `A/B/Stone.summary.json` и full traces фиксируют Gold before/after, price, tower/cell/ID, investment и refund для каждой команды. Дополнительный естественный economy branch от WaveComplete3: [accepted Upgrade/Sell audit](economy-upgrade-sell.audit.txt): sell70→refund49; upgrade105 переводит investment70→175 и Gold149→44; sell175→refund122 и Gold44→166; repeated Sell отклонён без мутации. Branch не выдаёт ресурсов control replay и не используется как playable/proc evidence.

| Route / reaction | Право | Покупка | Первый фактический proc | Всего procs / targets / Applied HP |
|---|---|---|---|---|
| A Steam intrinsic | Confirm after3, WaveComplete3/tick1287 | Preparation4/tick1287, Gold149→39 | Combat4/tick1323, tower3 |11 /13 /234|
| A Steam status pair | тот же Confirm; FLAME+TIDE | TIDE в Preparation5, Gold74→14 | Combat5/tick2020, tower1 |3 /3 /32|
| B Storm intrinsic | Confirm after3, WaveComplete3/tick1287 | Preparation4/tick1287, Gold141→31 | Combat4/tick1330, tower3 |13 /23 /138|
| B Storm status pair | тот же Confirm; TIDE+GALE | TIDE в Preparation5, Gold61→1 | Combat5/tick2003, tower4 |2 /4 /28|

[Проверка соответствия каждого proc реальным damage hits и положительной потере HP](reaction-damage-audit.json), [stdlib verifier](verify-evidence.py). До4 procs нет, каждое событие проверяет требования Domain. Закрытые покупки проверяются в каждой Preparation; Next до Confirm3, повторные/stale Confirm и repeated Next атомарны. В Stone отсутствуют complementary elements и procs до Defeat.

**FAIL/BLOCKED:** A Storm и B Steam не имеют позднего права/покупки/proc. Нулевой требуемый proc не PASS. Не выживший до6 deterministic Defeat проходит тест повторяемости, но не заменяет playable coverage и обе синергии в одном забеге.

## Файловое восстановление и меню

[A file audit](A.file-audit.txt), [B file audit](B.file-audit.txt), [A continued full trace audit](A.continued-audit.txt), [B](B.continued-audit.txt). Temporary isolated slots удаляются в TearDown; real persistentDataPath не используется.

- Whole-snapshot Load дважды на каждой достигнутой safe границе: экономика, IDs, army, openings/history/credits/pending восстанавливаются вместе. Primary/backup bytes на Load неизменны. Safe restore внутри полного replay совпадает с uninterrupted control trace.
- Exit/Continue до и после Confirm3 через настоящий MainMenu: preview не восстанавливается, confirmed выбор не выдаётся снова. Failed Confirm save сохраняет live success и старый disk pending; новый запуск получает весь durable pending. Retry только сохраняет; повтор Next/Confirm не начисляет награды.
- Combat4: A prefix75 entries, B85, после реального kill и reaction proc. Две попытки совпадают, dirty/fresh restore возвращает всю Preparation4 с её Gold/IDs/army/unlocks; disk не сохраняет Combat. [A prefix](A.rollback4.txt), [B](B.rollback4.txt).
- Реальное меню сравнивает каждый итог волны с Domain golden snapshot и после Combat rollback проверяет отсутствие events/summary/reaction pool/attack beams/inspector/placement/choice preview. [Batch menu trace](playmode-batch/RegBattle187_ProductionMenuNaturalFlameContinue.menu-trace.txt), [graphical A](playmode-visual/RegBattle187_ProductionMenuNaturalFlameContinue.menu-trace.txt), [graphical B](playmode-visual/RegBattle187_ProductionMenuNaturalGaleContinue.menu-trace.txt).
- [Schema1 audit](schema1.migration-audit.txt), [schema2 audit](schema2.migration-audit.txt): явно **synthetic historical migration inputs**, legacy test-tower либо Steam+Storm army с Upgrade; file production compatibility routing и целый backup в направлениях corrupt native→legacy / corrupt legacy→native. Это не деньги/армия production replay. Существующие REG181…183 дополнительно проверяют все типы/уровни/фазы и следующий schema3 Save.

**BLOCKED:** native natural pending/confirmed6, failed-save restart6 и Combat7 rollback. Отдельные existing synthetic REG174/178/179/180/185 проходят эти границы, все8 волн и caps, но не доказывают достижимость production.

## Реальные Play Mode кадры / Console

[Все14 кадров](screenshots) — 1920×1080, composited Game View с overlay UI. Production saved combat data, публичные команды и настоящие scene/controllers; **synthetic Game State в этих кадрах нет**. Тестовый clock ускоряет public Step (до120 ticks/frame), штатные consumers рисуют events; кадры не подтверждают realtime VFX cadence/beam density/performance. Time.frameCount меняется в trace, это выполненный Play Mode, а не Editor-only render/assertion.

- [A restored pending3](screenshots/A-pending3.png), [failed save/restart](screenshots/A-failed-confirm-restart3.png), [confirmed](screenshots/A-confirmed3.png), [Combat4 rollback](screenshots/A-rollback4.png).
- [Steam intrinsic](screenshots/A-production-Steam-Intrinsic.png), [Steam pair](screenshots/A-production-Steam-StatusPair.png), [A natural Defeat](screenshots/A-natural-outcome.png).
- [B restored pending3](screenshots/B-pending3.png), [failed save/restart](screenshots/B-failed-confirm-restart3.png), [confirmed](screenshots/B-confirmed3.png), [Combat4 rollback](screenshots/B-rollback4.png).
- [Storm intrinsic](screenshots/B-production-Storm-Intrinsic.png), [Storm pair](screenshots/B-production-Storm-StatusPair.png), [B natural Defeat](screenshots/B-natural-outcome.png).

Final graphical report **2/2 PASS**. Scenario Console audit: **0 unexpected errors**, A8/B16 Editor warnings: `Ignoring depth surface load/store action as it is memoryless`. Обе точные warning строки разрешены только test audit и сохранены полностью; другие warnings/errors отклоняют тест. По одному ожидаемому injected save error на маршрут записано отдельно в menu trace и проверено LogAssert. [Console A](playmode-visual/RegBattle187_ProductionMenuNaturalFlameContinue.console.txt), [B](playmode-visual/RegBattle187_ProductionMenuNaturalGaleContinue.console.txt). Первоначальный graphical run **0/2** из-за слишком строгого empty-warning assertion сохранён в [initial audit](initial-visual-audit/Visual.xml); это не gameplay fix и не скрытый PASS. Rendering configuration не менялась.

## Gates и source audit

| Gate | Результат | Raw report SHA256 |
|---|---|---|
|Static|38/38 + project checks PASS|`94dc14a5b2ab3b8c56c4f78a750771a8796df8ce5310f7ee5ddfa0061793cf6c`|
|EditMode|751/751; failed/inconclusive/skipped0/0/0|`9863ac5c1da1b1f17e403498e59305b63a732e48b112c11136b2aff81fe9b2b8`|
|PlayMode|43/43; failed/inconclusive/skipped0/0/0|`a09f5e3b1558f5a6db1298561687be5b010a43b024e0d74693930b6380412aea`|
|Visual|2/2; failed/inconclusive/skipped0/0/0|`38e101e5d2f16dd9b5bd0913c709f8763169607bf540b44d9df588742768eecd`|

[Raw reports](reports), [commands/settings/hashes](gates.json). Static перед Unity запущен последовательно: его fake-backend self-tests используют тот же TestResults и не должны идти одновременно с реальным runner. Финальный EditMode меняет generated assets через existing builder tests; drift удалён **только в этой worktree**, затем полный PlayMode и graphical run выполнены на committed scenes/configs. Generated material drift после Editor также удалён. Финальный diff --check PASS; runtime/config/package/art/исторические evidence не входят в diff. `.meta`/GUID/asmdef/version/packages проверены static; compile подтверждён положительными полными suites. Signing/private credentials не использовались.

## Acceptance matrix

| Критерий | Статус | Доказательство / причина |
|---|---|---|
|real-budget-public-production|**PASS**|Saved assets; initial150/20; A/B/Stone receipts and unchanged config/package hashes.|
|double-full-natural-trace|**PASS**|A/B/Stone first/second decompressed bytes SHA256 equal; snapshots/events/commands/results/IDs/Grid/progression.|
|natural-playable-through-unlock6|**FAIL**|A and B naturally Defeat5/tick2246; HP0/Kills13, Gold34/21. Limited strategy search is not proof of global impossibility.|
|economy-place-upgrade-sell|**PASS**|Per-command receipts; accepted Upgrade105 investment70->175; refund122; repeated Sell rejected. economy-upgrade-sell.audit.txt.|
|stone-route|**PASS**|Natural Defeat5/tick2205/Gold60/Kills12; Stone bought4, Tide6 suffix unexecuted. No unauthorized Steam/Storm proc.|
|locked-and-next-before-confirm|**PASS**|Every Preparation probes closed purchases; Next pending3 and repeated/stale start/after3 Confirm reject atomically.|
|A-steam-pair-and-intrinsic|**PASS**|Entitled WaveComplete3/tick1287; purchase Preparation4/tick1287; first intrinsic4/tick1323, pair5/tick2020.|
|B-storm-pair-and-intrinsic|**PASS**|Entitled WaveComplete3/tick1287; purchase Preparation4/tick1287; first intrinsic4/tick1330, pair5/tick2003.|
|A-storm-and-B-steam-in-same-run|**BLOCKED**|No after6 right, purchase or proc in either natural script. Zero required proc is not coverage PASS.|
|no-premature-purchase-or-proc|**PASS**|Per-tick requirements assertions and closed command rejection; before4 no procs; actual entitlement/purchase/first proc separated.|
|files-pending-confirmed3|**PASS**|Whole snapshot twice; failed Confirm/restart returns durable pending, Retry only saves, repeated Load/Next no reward. Actual MainMenu Continue.|
|files-pending-confirmed6|**BLOCKED**|Native natural runs terminate5. Existing synthetic REG174/178/185 tests separate, not production acceptance.|
|combat4-kill-proc-rollback|**PASS**|A prefix75 entries/B85, twice identical; full dirty/fresh snapshot restore, actual menu replay, no events/summary/beam/VFX/selection left.|
|combat7-kill-proc-rollback|**BLOCKED**|No native natural Preparation7. Existing synthetic REG179/180 separate.|
|load-read-only-retry-only-save-no-duplicate|**PASS**|Whole continued trace equals uninterrupted trace; bytes checked on Load/menu; Retry snapshot identity; repeated Next/Confirm/Sell atomic.|
|native-legacy-backup-schema1-schema2|**PASS**|Production file routing; corrupt primary of each mode to opposite complete backup. Synthetic historical migration inputs incl Steam+Storm army and level2, no production survival claim.|
|historical-fixtures-evidence|**PASS**|Full suites retain P2.1e Empty and P2.2f explicit historical factories; no historical evidence edits.|
|synthetic-eight-wave-boundaries-caps|**PASS**|Existing full-suite REG164..185, explicitly synthetic; not native playable/proc coverage.|
|visual-production-console|**PASS**|14 actual Play Mode Game View captures1920x1080; final Visual2/2, zero unexpected errors, rendering warnings preserved in playmode-visual/ and gates.json.|
|quality-gates|**PASS**|Static38/38; complete EditMode751/751; PlayMode43/43; Visual2/2; failed/inconclusive/skipped0. Exact commands, raw reports and SHA256 in gates.json. Final diff check included.|
|independent-review-merge|**BLOCKED**|Review pending; self-merge prohibited; P2.3 not DONE.|
|physical-iPhone-device-G2-balance-art-TestFlight|**BLOCKED**|Out of this verification PR; no device/player/signing/TestFlight run.|

## Отдельный узкий follow-up

Записанные scripts не доживают до второго открытия: перед Combat5 у A/B HP3, на wave5 они получают lethal leaks; Gold до покупки TIDE74/61, после14/1. Отсутствующая after6 — следствие natural Defeat, а не потеря earned credit. В этом scope gameplay bug не установлен; это playable/balance limitation конкретных scripts и ограниченного поиска.

Предлагается отдельный тикет **«Ранняя оборона и бюджет до unlock6»**: расширить legal strategy search (включая Upgrade timing), измерить HP/kill/budget до3/6 для обоих стартов; если viable route не найдётся, представить владельцу одно узкое изменение раннего combat/economy с новым regression scenario и review. Числа и продуктовый вариант здесь не утверждаются и не реализуются. Warning rendering audit при необходимости исследуется отдельным graphics ticket.

**NOT VERIFIED / BLOCKED:** native позднее playable/proc coverage; review/merge и P2.3 DONE; physical iPhone, real touch/safe area/background/force quit, IL2CPP/Metal player, performance/allocations/thermal, hosted Unity CI, Device QA/G2, balance/ART approval и TestFlight. Исторические P2.1e/P2.2f/ART evidence неизменны и проверяются своими historical fixtures. Self-merge/release не выполнялись.
