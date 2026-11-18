# PR #69 / #39 — R2: расширенный поиск, BLOCKED

Продолжение существующей ветки `codex/39-rift-warden`, без новой feature-ветки. **Acceptance не достигнут; PR остаётся draft, Refs #39 / Refs #40.** Баланс, runtime, factories, конфиги, packages, версия Unity и Victory assertions не менялись. Исходный пользовательский checkout не изменялся.

Native tested source: `fd22bb24a32bcbd365a8b08bd12f02cccc900f95`. Grid и первоначальный длинный research: `a46a9cc`; повтор timing/repair и архивные seeds: `c3620fa`. Различия между источниками — test-only поиск, его timeout/отчётность и зафиксированные команды. Точные source/report/config/input/media SHA256, команды, среда и raw XML — [gates.json](gates.json), [manifest](source-sha256.json). После tested source меняются только docs/evidence.

## VERIFIED

| Gate | Фактический итог |
|---|---|
| Static | **38/38 PASS**, version/packages/meta/GUID/asmdefs PASS |
| Полный EditMode | **1016 passed / 2 failed / 1018 total — FAIL** |
| Полный PlayMode | **56 passed / 2 failed / 58 total — FAIL** |
| Graphical | **1 passed / 2 failed / 3 total — FAIL**, 26 новых реальных PNG |
| Повтор расширенного timing/repair | **4/4 PASS** как завершение research, Victory0 |
| Отдельный archived-seed timing/repair | **4/4 PASS** как завершение research, Victory0 |
| Console трёх graphical cases | **0 unexpected errors/exceptions/asserts**, 12 rendering warnings |
| Scope | Runtime/configs/packages/version unchanged; все **695** прежних evidence файлов unchanged; `git diff --check` PASS |

В каждом NUnit report skipped/inconclusive **0/0**. Единственные failures финальных полных suites и graphical — два прежних обязательных natural Victory assertions. Runner/XML validator вернули FAIL, это не зелёные suites. Новый lethal-Cast regression проверяет исходный relative tick до terminal reset и replay; ranking regression сохраняет приоритет Victory при равном bossHP.

Первоначальный R2 optional run на a46a9cc: **6 passed / 2 failed / 8 total**, оба repair FAIL по стандартному timeout180000ms. Методы записали полные budgets207200/218400, но это не PASS. [Raw report](reports/Research-initial-timeout.xml.gz) и [initial-a46 candidates](initial-a46-research.tar.gz) с [raw member SHA256](initial-a46-sha256.json) сохранены отдельным архивом. Timeout увеличен до600000 только для длинного research test; затем timing/repair повторён полностью, без изменения gameplay assertions. Старый [R1 FAIL пакет](../validation.md) сохранён побайтно.

## Конкретные лучшие попытки

Оба script replay начинаются настоящим production New Game **150 Gold / 20 HP**, без загрузки подготовленного boss fixture. Shared `BossMissionScript` задаёт одинаковые ordered choices, покупки/Upgrade/Sell и wave-relative Cast ticks/centers для Domain и настоящих PlayMode controllers/Buttons. Receipts содержат результаты команд, HP/Gold/Kills/cooldown/sequence, восемь safe snapshots и per-wave outcomes.

| Старт / choices3→6 | Preparation8 HP / Gold после покупок | Outcome / tick / Gold / Kills | Boss HP / kill | Direct / Burn / Reaction / Ability по boss |
|---|---:|---|---:|---|
| FLAME / STONE→GALE | 10 / 10 | **Defeat8 / 3799 / 65 / 57** | **154 / 0** | 718 / 16 / 0 / **112** |
| GALE / STONE→TIDE | 14 / 16 | **Defeat8 / 3602 / 71 / 61** | **68 / 0** | 820 / 0 / 0 / **112** |
| Отдельный FLAME no-cast8 control / STONE→TIDE | 13 / 2 | **Defeat8 / 3956 / 57 / 60** | **447 / 0** | Ability по boss **0** |

У лучших A/B все11Shade восьмой волны убиты, затем живой boss наносит leak20 и clamp HP→0. Ни reward50 за boss, ни bonus20 волны не получены. No-cast8 control сохраняет прежние команды и касты1–7, отдельно проверяет boss-leak Defeat и не подменяет попытку Victory.

- FLAME: [script](flame.commands.json), [receipts](natural/flame-victory.json), [все safe snapshots](natural/flame-victory.waves.jsonl). Армия: Flame1(-2,-1), Stone1(-1,0)/(1,0)/(2,1)/(-1,-1), Gale1(-2,0). Последний boss Cast: relative450; фактический суммарный ability HP loss1258, по boss112.
- GALE: [script](gale.commands.json), [receipts](natural/gale-victory.json), [snapshots](natural/gale-victory.waves.jsonl). Армия: Gale1(-2,0), Stone1(-1,0)/(1,0)/(2,1)/(1,1), Tide1(2,0). Есть законная продажа/замена перед8; последний boss Cast relative570. Суммарный ability HP loss1476, по boss112.
- [No-cast8 receipts](natural/flame-leak.json), [snapshots](natural/flame-leak.waves.jsonl). Команды — неизменённый [R1 input](../flame.commands.json), Cast receipts — [R1 leak](../flame-leak.json); `Original()` читает только команды/tick/center, не загружает state.

Имена `*-victory` обозначают цель теста. Фактический outcome внутри — **Defeat**. По сравнению с R1 остатки337/256 снижены до154/68; победой это не является.

## Охват и ограничения поиска

[Все кандидаты/profiles/limits в архиве](research.tar.gz), [SHA256 каждого raw member](research-sha256.json), [summary с damage/economy/best per history](search-summary.json), [конкретные лучшие истории](best/). Командные archived seeds находятся в [research-seeds](research-seeds/); wrapper не содержит checkpoint/HP/Gold/state.

| Финальные passes | FLAME | GALE | Что считается |
|---|---:|---:|---|
| Grid + исторические P2.3e.1 survival/coverage seeds | 24012 | 24012 | Полный New Game, включая ранние Defeat |
| Независимый timing | 960 | 960 | Полный New Game; IllegalSeedSuffix232/152 учтены отдельно |
| Preparation8 repair | 218400 | 218400 | Полный бой8 из публично достигнутого safe checkpoint |
| Archived-seed timing | 192 | 192 | Полный New Game, все Defeat |
| Archived-seed repair | 44800 | 44800 | Полный бой8, все Defeat |

**576728 candidate outcomes** в этих пяти passes; это не число уникальных стратегий и не все New Game. Не включены повторные префиксы1–7 при восстановлении roots, smoke/gates или ранние несохранённые exploratory runs. Initial-a46 retry history дополнительно содержит1872 timing и425600 wave8 outcomes; они не добавлены повторно к финальному coverage. Ни одна исследованная Victory не найдена.

Grid: все6 ordered after3/after6 histories каждого старта, все81 доступные стартовые клетки; по4000 детерминированных plans на history плюс12 исторических seeds на старт. Permutation stride7919; пространство параметров1259712 на history, **не исчерпано**. Layout3, support order3, role priorities4, начало дополнительных закупок4/5/6/7, starter upgrade0/2/3, дальнейшие upgrade0/4/6, один/два starter, sell before8, thresholds100/200/300. Boss delay в grid связан с layout0/90; независимый pass затем проверяет0/60/120/240, early cast8 и hold cast7 отдельно. Центры — enemy positions и pair midpoints; непрерывное пространство tick/center не исчерпано.

Сохраняются все найденные Victory до ranking/pruning (отдельно eligible boss kill+positive ability), но их0. Ranking: actual Victory прежде Defeat, затем bossHP, baseHP, Gold. Grid dedup отсутствует. Retained100 type/level/count profiles на каждый старт, в том числе52/50 с upgraded towers. На history сохраняются16 лучших плюс до8 upgraded с удалением совпадений. Позиции и cooldown не входят в profile key, поэтому другие армии того же type/level/count могут быть отброшены. Initial-a46 timing/repair мог дублировать best как diverse; c3620fa выбирает отдельный profile. Timing/repair берут ограниченное число roots, а не все retained100.

Repair:20 input roots ×4 hold7/early8 варианта на старт,80 attempted/78 reached/2 skipped; причины пропуска классифицированы только как rejected suffix или earlier terminal, см. prefix-failures. Seed repair:4 roots ×4, все16 reached. Копии используют только Export/Restore публично достигнутой Preparation8, без edits DTO/HP/Gold/IDs/cooldown/progression. Depth4,700 evaluations/depth/root-variant, максимум2800, beam20 (8 best + type/level/count profiles), full-wire dedup только внутри root-variant. Рассматриваются Hold, Upgrade, Place и Sell1/Sell2+Place. Перебор Catalog/IDs/cells и budget/beam ограничивают доступные комбинации; это не полный перебор всех перестроек. Потомки худших текущих результатов могут быть пропущены. Новая политика Cast может сделать suffix старого script недоступным по Gold: это `IllegalSeedSuffix`, а не исправление экономики; CSV содержит фактические HP/Gold/Kills. `rejectedCommand` вычисляется в Measured, но отдельный JSON каждого отказа не публикуется и операция не включена в CSV; это ограничение отчётности.

### Фактический coverage полных grid матчей

Steam/Storm positive — AppliedHpLoss>0 за весь матч, не обязательно по boss. Нули в истории без нужной пары elements ожидаемы. Boss damage по источникам каждого retained/best находится в raw JSON; ни один proxy BossPower не используется как доказательство Victory.

| Start / after3 / after6 | Attempts / reached Preparation8 | Steam>0 / Storm>0 | Лучший bossHP после всех passes |
|---|---:|---:|---:|
| flame/tide/gale | 4012 / 1071 | 1565 / 859 | 597 |
| flame/tide/stone | 4000 / 1015 | 1551 / 0 | 279 |
| flame/gale/tide | 4000 / 1325 | 1190 / 1146 | 621 |
| flame/gale/stone | 4000 / 1288 | 0 / 0 | 312 |
| flame/stone/tide | 4000 / 823 | 685 / 0 | 278 |
| flame/stone/gale | 4000 / 806 | 0 / 0 | 154 |
| gale/tide/flame | 4012 / 1005 | 296 / 1828 | 565 |
| gale/flame/tide | 4000 / 1380 | 315 / 1054 | 616 |
| gale/flame/stone | 4000 / 1351 | 0 / 0 | 231 |
| gale/tide/stone | 4000 / 1020 | 0 / 1828 | 236 |
| gale/stone/flame | 4000 / 1290 | 0 / 0 | 154 |
| gale/stone/tide | 4000 / 1323 | 0 / 1097 | 68 |

## Реальные Play Mode кадры и Console

Все26 PNG пересняты на native tested sourcefd22bb2 через `ScreenCapture.CaptureScreenshotAsTexture`; telemetry содержит меняющиеся frameCount и реальные wave/tick/HP/Gold. Это graphical Editor, production factory/controllers/Buttons, synchronous public-command test clock. `Battle.Update` выключен для ускоренного Step script; накопленные beams/VFX не являются realtime/performance evidence. Injected44px safe area — Editor geometry, не физический iPhone. Console scope — от Setup до Cleanup каждого case:4/4/4 warnings `Ignoring depth surface ... memoryless`, unexpected errors0; [audit](console-audit.json). Это не утверждение об отсутствии всех инфраструктурных сообщений вне case scope.

| Состояние | FLAME wide1600×900 | GALE narrow1280×960 |
|---|---|---|
| Preparation8 | [кадр](screenshots/flame-victory-1600x900/01-preparation8-notice.png) | [кадр](screenshots/gale-victory-1280x960/01-preparation8-notice.png) |
| Чистый MainMenu Continue8 | [кадр](screenshots/flame-victory-1600x900/02-clean-continue8.png) | [кадр](screenshots/gale-victory-1280x960/02-clean-continue8.png) |
| Boss HP/armor | [кадр](screenshots/flame-victory-1600x900/04-boss-hp-armor.png) | [кадр](screenshots/gale-victory-1280x960/04-boss-hp-armor.png) |
| Ability112 по boss | [кадр](screenshots/flame-victory-1600x900/05-boss-ability-hit.png) | [кадр](screenshots/gale-victory-1280x960/05-boss-ability-hit.png) |
| Фактический Defeat | [кадр](screenshots/flame-victory-1600x900/08-defeat.png) | [кадр](screenshots/gale-victory-1280x960/08-defeat.png) |

Отдельный [no-cast8 boss-leak Defeat](screenshots/flame-leak-1600x900/08-defeat.png). Safe-area/status/inspector/Pause кадры и полные telemetry/Console лежат рядом с каждым набором. Просмотрены representative ability/Defeat/Continue/insets кадры; HUD читаем, magenta shader placeholders не обнаружены в просмотренных кадрах. Boss-kill/Victory кадры не созданы, потому что события не произошли.

## NOT VERIFIED / BLOCKED

- **REG213 natural Victory8 и реальный boss kill обоих стартов.** Обязательные assertions остаются FAIL. Общая непроходимость не доказана.
- Final-content natural Victory whole checkpoint/restore и natural boss-kill completion-save failure/Retry без duplicate reward. Эти недостающие сценарии не добавлены как пройденные; после natural Victory их ещё требуется реализовать и снять соответствующие кадры. Существующие compatibility/rollback/старые goldens сохранены и проходят в full suite.
- Полные зелёные suites и boss-kill/Victory/terminal Continue evidence не получены.
- Physical iPhone/IL2CPP/Metal/touch/lifecycle/performance/G2/ART, полная отдельная #40 matrix не проверялись. Merge/release/TestFlight не выполнялись. Projects field не подтверждён доступным API.

## Проверка пакета и review

`python3 Docs/review/P2.4e/revision-2/verify-evidence.py` сверяет raw XML/числа/failed cases, source/control/input/history/media SHA256, archive members и реальные outcomes. **PASS согласованности не делает gameplay gates зелёными.** Старый R1 verifier привязан к рабочему дереву b970003; R2 verifier проверяет прежние bytes по Git b970003 и не переписывает старый FAIL пакет. Все695 прежних evidence файлов (включая625 до #39) неизменны.

[Независимое review](review.md) проверяет финальный source/evidence отдельно; reviewer Unity самостоятельно не запускает. [PR #69](https://github.com/AlexeyKravtsov95/Veilward/pull/69) остаётся draft/Refs #39/Refs #40; без Closes/Done/self-merge.
