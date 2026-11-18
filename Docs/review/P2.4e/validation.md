# P2.4e / #39 — частичная реализация, acceptance BLOCKED

Дата: 18.11.2026, Asia/Novosibirsk. База `65d104e19a5e81a4cc91edd2fc2cbbf548ea3674`: GitHub PR #68 `merged=true`, merge SHA совпадает, fresh main подтверждён перед отдельной веткой `codex/39-rift-warden`. Изолированный worktree `/Users/alexeykravtsov/Desktop/workspace/Veilward-39`; исходный пользовательский checkout с локальными изменениями не трогался.

**Проверенный source: `b970003d0d65146d8230e7142321c92bf2905eeb`.** Последующие commits — docs/evidence. [Scope/сценарии](../../P2.4E_RIFT_WARDEN.md), [матрица REG209–213 и final-ID REG198–203](matrix.md), [gates/raw XML/SHA256/команды/среда](gates.json), [source/control/historical hashes](source-sha256.json), [scope audit](scope-audit.json), [независимое review](review.md).

## VERIFIED

| Проверка | Фактический результат |
|---|---|
| Baseline main | Static **38/38**, EditMode **922/922**, PlayMode **54/54** |
| Initial RED | EditMode **922 passed / 3 failed / 925 total**; PlayMode **54 passed / 1 failed / 55 total**. Отсутствовали schedule API, final wrapper и boss HUD |
| Final static | **38/38 PASS**; Unity version/revision, frozen packages/lock, paired `.meta`, GUID, asmdefs |
| Final full EditMode | **1008 passed / 2 failed / 1010 total — FAIL** |
| Final full PlayMode | **56 passed / 2 failed / 58 total — FAIL** |
| Final graphical | **1 passed / 2 failed / 3 total — FAIL**, 26 реальных PNG |
| Optional bounded research | **4/4 завершённых research cases**, без natural Victory; это не acceptance PASS |
| Console | **0 gameplay errors/exceptions/asserts**, **12 Editor rendering warnings** `Ignoring depth surface ... memoryless` |
| Compile / serialization | Оба полных Unity suites скомпилированы и реально выполнены. Builder/reproducibility/refs и `.meta`/GUID assertions выполнены |
| Scope | TestWave/EightWave bytes, tower prices, waves1…7, packages/version и **625** исторических evidence файлов неизменны; `git diff --check` PASS |

Во всех reports inconclusive/skipped **0/0**. Full suite не объявляется PASS: штатные runners и XML validator вернули FAIL, поскольку оба обязательных natural Victory assertions остались красными. Два preactivation negative cases изменены при активации final schema4; схемы1…3 по-прежнему rejected. Historical test/menu factories выбраны явно; outcomes/goldens не переписаны.

Runtime: bounded precomputed schedule, один rift-warden в8, прежний общий death/reward/completion pipeline, final content/native/enabled schema4, независимые old/new slot factories, whole Combat8 rollback, boss notice/HP/armor/inspector. Synthetic boundaries проверяют четыре lethal origins, one-time reward50/bonus20, leak20/clamp0/Defeat priority, Burn/cast boundary, Steam/Storm primary/secondary, обычные Wet/Gust/Crack. Они не подменяют natural boss kill.

## Natural outcomes — обязательный acceptance не достигнут

Все scripts начинают реальным production New Game с **150 Gold / 20 HP**, без edits DTO/HP/Gold/spawns/definitions. Выбор start FLAME/GALE, затем STONE после3 и TIDE после6. Покупки/Upgrade/Sell/Start/Next/Choose/Cast/Step — public commands, receipts сохраняются. Ability policy: при Ready выбирать enemy center с максимальным ожидаемым суммарным HP loss; до boss8 способность резервируется до его появления. Cast между Steps; точный tick/center/sequence/remaining и бюджеты записаны в receipts. PlayMode использует настоящие MainMenu Buttons, production controllers и Ability Buttons; только simulation clock ускорен.

| Сценарий | HP в Preparation8 | Фактический outcome | Boss HP перед leak | Ability HP loss / по boss | Kill boss |
|---|---:|---|---:|---:|---:|
| [FLAME commands](flame.commands.json), [receipts](flame-victory.json), [полные safe snapshots](flame-victory.waves.jsonl) | 13 | **Defeat8**, tick3956, Gold57, Kills60 | **337** | 1193 / **110** | 0 |
| [GALE commands](gale.commands.json), [receipts](gale-victory.json), [safe snapshots](gale-victory.waves.jsonl) | 15 | **Defeat8**, tick3784, Gold62, Kills62 | **256** | 1389 / **110** | 0 |
| [Отдельный legal no-cast8 control](flame-leak.json), [safe snapshots](flame-leak.waves.jsonl) | 13 | **Boss-leak Defeat8**, tick3956, Gold57, Kills60 | **447** | positive до8 / **0 в boss8** | 0 |

Имя `*-victory` означает цель сценария; результат внутри — **Defeat**, не победа. Leak control сознательно оставляет обычную оборону для Shade и **не кастует в8**; удаление живого boss с HP447 и clamp13→0 проверены отдельно. Полная продажа обороны при HP13 могла бы привести к более раннему Shade Defeat, поэтому этот script этого не делает. Boss kill reward и wave bonus не получены. Synthetic20→0 — отдельный unit boundary.

Оба старта законно достигают Preparation8; interrupted Combat8 содержит accepted cast+positive ability damage+kill+positive reaction и живого boss. Dirty/fresh rollback и два одинаковых prefix возвращают всю Preparation8, включая economy, IDs, tower cooldown, progression, ability remaining/sequence. Rejected Restore сохраняет dirty state/events.

Повторяемый [bounded beam research](research/search-economy/) и [912 greedy candidates](research/greedy/) на том же source не нашли Victory. Бюджеты/отсечения/скрипты/итоги сохранены; это ограниченные эвристики, **не доказательство глобальной непроходимости**. До финального source также были exploratory варианты поиска; они не выдаются за точный финальный replay. Принятые D-числа и баланс не менялись ради тестов.

## Реальные Play Mode кадры

| Состояние | Wide FLAME1600×900 | Narrow GALE1280×960 |
|---|---|---|
| Preparation8 / notice | [кадр](screenshots/flame-victory-1600x900/01-preparation8-notice.png) | [кадр](screenshots/gale-victory-1280x960/01-preparation8-notice.png) |
| Injected44px safe-area layout | [кадр](screenshots/flame-victory-1600x900/01b-injected-safe-area.png) | [кадр](screenshots/gale-victory-1280x960/01b-injected-safe-area.png) |
| Чистый реальный MainMenu Continue8 | [кадр](screenshots/flame-victory-1600x900/02-clean-continue8.png) | [кадр](screenshots/gale-victory-1280x960/02-clean-continue8.png) |
| Ожидание spawn330 | [кадр](screenshots/flame-victory-1600x900/03-waiting-spawn330.png) | [кадр](screenshots/gale-victory-1280x960/03-waiting-spawn330.png) |
| Spawn / HP / armor | [кадр](screenshots/flame-victory-1600x900/04-boss-hp-armor.png) | [кадр](screenshots/gale-victory-1280x960/04-boss-hp-armor.png) |
| Ability110 / геометрия bar | [кадр](screenshots/flame-victory-1600x900/05-boss-ability-hit.png) | [кадр](screenshots/gale-victory-1280x960/05-boss-ability-hit.png) |
| Status / inspector | [кадр](screenshots/flame-victory-1600x900/06-boss-status-inspector.png) | [кадр](screenshots/gale-victory-1280x960/06-boss-status-inspector.png) |
| Pause | [кадр](screenshots/flame-victory-1600x900/07-boss-pause.png) | [кадр](screenshots/gale-victory-1280x960/07-boss-pause.png) |
| Фактический Defeat | [кадр](screenshots/flame-victory-1600x900/08-defeat.png) | [кадр](screenshots/gale-victory-1280x960/08-defeat.png) |

Отдельный [legal boss-leak Defeat](screenshots/flame-leak-1600x900/08-defeat.png), [telemetry](screenshots/flame-leak-1600x900/telemetry.txt). [Console audit/frame proof](console-audit.json); полный Console/telemetry каждого сценария рядом с PNG. Все26 PNG — настоящие `ScreenCapture.CaptureScreenshotAsTexture` из graphical Editor с меняющимися Time.frameCount, без generated/mock images. Boss kill/Victory кадры **BLOCKED**, поскольку события не произошли.

На кадрах явно написано **PRODUCTION ... public-command test clock / Editor Play Mode**. Battle Update отключён для быстрого synchronous Step script, кадры реально рендерятся. Из-за ускоренного burst накоплены beams/VFX прежних ticks: это видно, в частности, в первой Preparation8; чистый Continue8 отдельно показывает сброс transients. Такие кадры не подтверждают realtime VFX clock, performance или устройство. Injected44px insets — синтетическая геометрическая проверка Editor, не реальные вырезы iPhone.

Первый graphical run source1fc5324 показал наложение reaction summary на boss notice и post-cast panel на inspector; после просмотра исправлено в02a6ada и проверено новыми layout/visibility assertions. Финальные26кадров пересняты на **b970003**, первые кадры не используются как финальное evidence. Shader placeholders на финальных representative PNG не обнаружены; просмотрены notice/insets/status/ability/Defeat.

## NOT VERIFIED / BLOCKED

- **REG213 natural Victory FLAME и GALE, natural boss kill и соответствующие кадры**. Оба обязательных теста FAIL в EditMode, PlayMode и graphical, не skipped и не ослаблены.
- Final-content natural Victory snapshot и boss-kill completion с IO fault не получены; generic synthetic boundaries не подменяют их. Полная #40 double-trace/file/menu matrix — следующий отдельный этап.
- Physical iPhone/touch/реальные safe area/background/force quit, built-player/IL2CPP/Metal, device profiling/thermal, Simulator, signing, Unity self-hosted CI, human balance/G2 и final ART не проверены.
- Новый production path находится в **непринятой ветке**. Только draft Refs#39/Refs#40; issue не закрывается, merge/release/TestFlight не выполнялись. Projects field отдельно не подтверждён: доступного GitHub Projects API нет.

## Повторная проверка

`python3 Docs/review/P2.4e/verify-evidence.py` сверяет raw XML/числа/точный список failed cases, source/config/historical/media hashes, размеры26PNG, natural outcomes и frame proof. Он подтверждает согласованность пакета и **не делает gameplay gates зелёными**. Полные команды/environment/source SHA находятся в gates.json; XML gzipped без изменения raw bytes. Unity import/builder serialization drift возвращён только в собственной worktree; GUID/meta и source assets совпадают с проверенным commit.

Независимый audit обнаружил накопление Console/telemetry между NUnit cases в первом fixture. В b970003 Setup очищает оба списка и сбрасывает output/capture; после этого все полные gates/research/26PNG повторены на новом source. Финальные logs независимы: по4warnings на сценарий, всего12, не24cumulativeentries.
