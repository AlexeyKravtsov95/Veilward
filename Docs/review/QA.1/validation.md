# QA.1 / #42 — частичный Draft, device acceptance BLOCKED

19.11.2026. [Draft PR71](https://github.com/AlexeyKravtsov95/Veilward/pull/71), **Refs #42**. Base main `de8b9b58f26ca86bfbc352eb214eaee7f7a6992c`, reviewed PR70 head4c09210 входит в main. Tested source `ab9c17ad0f4f26e92f9a09851200de098f808412`; последующие коммиты — только docs/evidence. [Контракт/owner handoff](../../QA1_IOS_BUILD.md), [фактические команды](commands.md), [инвентаризация](inventory.txt).

## Изменение и архитектура

Новый Editor-only IosBuild вызывает BuildPipeline для точных Bootstrap→MainMenu→Battle, проверяет saved production v2 и существующий identifier; временно закрепляет version/build/IL2CPP/ARM64/Metal/device SDK с restore в finally. Python stdlib exporter требует explicit version/build, clean committed source, отдельный stage и disposable exact-SHA worktree. Исходный checkout не открывается Unity; original source hash/status сверяются, source/import audit различают committed и derived inputs. Previous successful output заменяется только после BuildReport/receipt/project checks. Signing, CI и runtime не добавлены.

Баланс boss600/armor5, production `eight-wave-ability-boss-v2`, historical v1/native/all-open/schema4, сцены/GUID/art/packages/settings **не изменены**. Source audit содержит542 hashes Assets/Packages/ProjectSettings/Scripts и сверяется с tested commit и delivered files. Host gates могут сохранять regenerated assets;19 таких файлов восстановлены в исходно clean task worktree, их delta не включена в PR. Exporter решает эту проблему изоляцией, но настоящий iOS import audit ещё не получен.

## VERIFIED

| Проверка | Результат | Evidence |
|---|---|---|
| Свежий origin/main/отдельная ветка | de8b9b58; codex/qa1-ios-device | inventory.txt, git ancestry |
| Static gate |38/38,0fail, exit0; version/packages/meta/GUID/asmdefs PASS| reports/static.txt |
| Python tooling regressions |13/13,0fail,exit0; **fake Unity** orchestration| reports/tooling.txt |
| Host compile и EditMode |1110/1110,0failed/skipped/inconclusive,exit0| reports/EditMode.xml |
| Host PlayMode |60/60,0failed/skipped/inconclusive,exit0| reports/PlayMode.xml |
| Historical revision3 verifier |426artifacts/102traces/1301history unchanged| reports/historical-verifier.txt |
| Реальная export command с clean tested checkout |exit2, missing iOSSupport; исходный status clean| inventory.txt |
| Connected physical device inventory |iPhone16Pro, iOS27.0.1, paired/connected, DeveloperMode enabled, DDI available| inventory.txt |
| Source/delivery/diff audit |542source hashes, manifest tracked/hashes, clean-checkout verifier и diff check| source-audit.json, manifest.json, reports/delivery.txt |

Unity suites выполнены на **StandaloneOSX host**, не iOS. Сначала диагностическая компиляция обнаружила namespace/type ошибки нового Editor helper; они исправлены **до tested source**. Затем полный host1110/60 PASS; gates повторены в свежем detached checkout точного tested SHA. Два новых .meta созданы Unity, GUID вручную не генерировались. Full suites включают10 новых EditMode cases; PlayMode/runtime не менялись. Research100000 не запускался; historical evidence не переписывался.

XML — sanitized copies реальных результатов, counts/assertions не менялись. Sanitized full host logs в reports/EditMode.txt и PlayMode.txt; raw originals остались локально в ignored TestResults. Удалены private paths/device identifiers и служебные licensing machine/license/session/correlation IDs, username IPC channel; redactions обозначены. Console не назван «0 errors»: logs содержат Licensing access-token unavailable, debugger-agent listen warning и usbmuxd Error. Compiler errors отсутствуют в успешных runs; эти host сообщения не являются device Console audit. Логи Xcode/device отсутствуют, потому что эти шаги не запускались.

## NOT VERIFIED / BLOCKED

- Для Unity6000.3.28f1 установлен только MacStandaloneSupport. iOS Build Support отсутствует. Команда export остановилась **до** Unity build; настоящих Xcode project/BuildReport/IL2CPP binary/двойного iOS export нет.
- Xcode27.1 и SDK27.1 найдены, совместимость текущего slice с ними фактическим iOS build не проверена.
- iPhone подключён, но приложение не было собрано/установлено/запущено. Модель/OS inventory не доказывают device gameplay.
- Team/profile в проекте пустые; найдены две signing identities, но подходящая team/provisioning не выбраны/не подтверждены. Никакие Apple credentials/team/profile/device identifiers не запрашивались и не коммитились.
- Physical NewGame FLAME/GALE, placement/wave progress, ability preview/Confirm/Cancel/Pause и checkpoint/relaunch/Continue smoke, screenshots/video и device Console/Xcode unexpected errors — **не выполнены**. Прежние Editor кадры не приложены как device evidence.
- Настоящие iOS PlayerSettings restore/повторный export и post-import derived inputs проверены только tooling mocks/host contract, не iOS. Непустой future import audit требуется отдельно review перед device acceptance.
- Полная touch/safe-area/lifecycle/save матрица остаётся #53, profiling/memory/thermal #54. ART approval #41 и device art proof #43, G2, Unity hosted CI и release этим PR не закрываются.

Следующий точный шаг владельца: добавить **iOS Build Support для6000.3.28f1** через Hub, локально выбрать существующие Xcode team/profile для identifier проекта, затем из pristine checkout дважды выполнить export и пройти Xcode→install→launch и QA1-07 по [инструкции](../../QA1_IOS_BUILD.md). Не присылать private credentials. Если compatible provisioning/SDK недоступны — сохранить BLOCKED, issue42 open. Полное acceptance требует нового device evidence и review; текущий PR остаётся Draft/Refs. Self-merge/release/TestFlight не выполнялись.

[Независимое source/evidence review](review.md). Все manifest artifacts должны быть tracked; `python3 Docs/review/QA.1/verify-evidence.py` проверяет hashes/XML/source и сознательно не подтверждает device acceptance.
