# P2.3c — schema 3, legacy compatibility и safe snapshots

Проверено 2026-11-17 (Asia/Novosibirsk). База — merged PR #27, `59f07e27a566ef2c6f5b5fa28c3453bf9394453b`. Проверенный финальный source commit — `6e10486e438c64b2ba21e161b9ed033d5d35ff63` (исходная реализация `14432c689addb9c5a0f8e98ccfb6fb5dcee24254`); следующий evidence commit не меняет runtime/tests. Изолированный worktree `p23c-checkpoints`, ветка `codex/p2.3c-checkpoints`. Unity **6000.3.28f1 (8f082d2fe476)**, macOS **27.0.1 (26A434), arm64**. Backend: Hub executable, **batchmode / -nographics / buildTarget iOS**, Editor Mono; это не iOS player/device run.

## VERIFIED

- Native Export/Restore round trip: initial pending, оба старта и все 12 routes, pending/confirmed3/6, Preparation4/7, Victory/Defeat, mixed unlocked army. Export/Restore копируют mutable DTO; validation завершается до State/Grid/events mutation. Negative cases проверяют полный dirty Combat state и события.
- Strict JSON shape до JsonUtility: обязательные zero-valued поля, strings/arrays/objects, integer ranges/types и duplicate keys. Точный unlocked set, ordered history prefix, credits от completed waves, pending/phase/wave, native tower requirements и запрет test-tower; прежние scalar/grid/investment/ID constraints сохранены.
- Explicit whitelist normalization: schema1 → прежний deep test-tower mapping → legacy; schema2 сохраняет семь типов × L1–L3 и safe phase/wave matrix, включая оба гибрида. Legacy всегда all-four / 0 earned / 0 spent / empty history / none, без backfill. Следующий Save schema3 сохраняет old content ID. Single-wave не становится EightWave.
- Каждый слот независимо проходит decode → normalize → factory → full Domain validation. Native↔legacy fallback восстанавливает целый snapshot; repeated Load не пишет bytes и не начисляет награды. Source Normalize не мутируется. Factory throws/null дают failure слота и независимый backup attempt.
- Save проверяет source, temp/read-back и previous primary; damaged primary не вытесняет valid backup, а valid previous primary другого mode сохраняется. Старые raw fixtures и failed New Game recovery tests сохранены.
- Initial pending, completion с pending и successful Confirm сохраняются явно, включая неизменную WaveComplete. Failed save оставляет live Domain success, блокирует Start/Next до successful SaveCurrentState. Retry не повторяет Choose/Next/Step. Eligibility query выполняется до filesystem; Next сохраняет WaveComplete → переходит → сохраняет Preparation.
- Native Combat4/7 rollback после **положительных kill и reaction proc** совпадает в dirty/fresh instance; два interrupted prefix идентичны. Реальный PlayMode MainMenu → Battle проверяет native checkpoint routing и legacy hybrid backup под временным native test config. Direct Battle использует in-memory storage, тестовые слоты изолированы в TestResults, реальный пользовательский persistentDataPath не читается/не пишется.
- Public production New Game остаётся all-open. Configs, balance/combat data, packages, сцены, арт и историческое evidence не менялись. Исторические combat assertions сохранены: только schema-version expectations обновлены; P2.2f trace helper продолжает сериализовать прежний schema2-shaped combat checkpoint без нового progression поля. Новый progression сравнивается отдельными suites.

## Команды и totals

Из корня worktree:

```sh
./Scripts/run-edit-mode-tests.sh
./Scripts/run-play-mode-tests.sh
./Scripts/run-static-checks.sh
git diff --check
```

| Run | Total | Passed | Failed | Skipped | Inconclusive | Отчёт |
|---|---:|---:|---:|---:|---:|---|
| Начальный RED до реализации | 591 | 589 | 2 | 0 | 0 | [XML](red-initial.xml.gz) |
| Presence sensitivity RED + exact-ID probe | 702 | 689 | 13 | 0 | 0 | [XML](red-presence.xml) |
| RED исправления review | 717 | 715 | 2 | 0 | 0 | [XML gzip](red-save-retry.xml.gz) |
| **Полный EditMode** | **717** | **717** | **0** | **0** | **0** | [XML](EditMode.xml) |
| **Полный PlayMode** | **32** | **32** | **0** | **0** | **0** | [XML](PlayMode.xml) |
| **Static self-tests** | **38** | **38** | **0** | **0** | **0** | [stdout](static-checks.txt) |

Static project checks: PASS (Unity version/revision, frozen package baseline/lock, paired `.meta`, GUID, asmdef). `git diff --check`: PASS после удаления только generated builder drift в этом изолированном worktree. JSON с SHA256 всех отчётов и фактическими test names: [gates.json](gates.json). Repository XML validator принял оба непустых полных финальных suite. Static fake-backend self-tests перезаписывают TestResults, поэтому реальные Unity XML скопированы сюда **до** static run.

Начальный RED: `RegBattle174_NativeInitialCheckpoint_RoundTripsProgression` упал на предусмотренном P2.3b `NotSupportedException`, `RegBattle178_NextSaveFailure_PreservesWaveCompleteBeforeTransition` — `Continued` вместо rejection. Затем реализован контракт. Дополнительно временно отключена **только** read-shape validation: 12 probes стали принимать missing zero fields, numeric overflow/coercion и duplicate key; ещё один новый probe выявил cross-content bypass старого validator overload. Shape validator восстановлен, exact-ID bypass исправлен, полный GREEN повторён. Mutation не входит в PR.

Промежуточные runs не считались PASS: один compile run без XML выявил недоступный из Presentation constructor результата выбора (исправлен); расширенные suites выявляли устаревшие schema2 expectations, тестовую сериализацию combat-only state, JsonUtility materialization отсутствующего nested object и неверную тестовую клетку на path. Исправлены fixtures/helpers и source normalization, raw migration fixtures и combat outcomes не ослаблялись.

## Независимое review и fix

[Read-only review](review.md) исходного PR head нашло 1 Important и 0 Critical/Minor: LastSaveSucceeded запрещал новую попытку safe save после transient IO. Два новых regression cases дали ожидаемый RED **715/717** ([исходный XML gzip](red-save-retry.xml.gz)). Early latch удалён: eligibility остаётся первым, Start/Next снова могут попытаться сохранить текущий snapshot и переходят только после успешной записи. Отдельный Retry SaveCurrentState по-прежнему не вызывает Domain-команду. FaultWriter теперь сохраняет ongoing write failure до явного восстановления, поэтому отрицательные failure tests не становятся ложноположительными на следующей попытке.

Также добавлены положительные raw schema1/2 SingleWave controls без progression key: валидный файл читается как одна волна и отдельно отклоняется requested EightWave. Финальный полный GREEN после fix: **717/717 EditMode**, PlayMode и static — в таблице выше. Исходный reviewed head имел 713/713 EditMode; fix проверен автором через RED→GREEN и полные gates, повторное независимое approval не заявляется.

## Architecture notes

- Domain остаётся pure C# / noEngineReferences. Progression restore строит новое immutable state после проверки всего DTO.
- Production Store/menu/session/scene/controller используют compatibility + simulation factory до публикации. Старый validator overload остаётся exact-ID synthetic/legacy path и не используется для native→legacy routing.
- Internal `IWaveCheckpointWriter` отделяет durable temp write/atomic commit; runtime использует FileStream.Flush(true)/File.Replace, тестовые fault writers вызывают тот же реальный writer и вводят IO errors/read-back corruption на конкретном save. Это не новый package или gameplay framework.
- UI выбора не добавлен. `BattleSceneController.ChooseElement` — guarded command/save entry point для следующего этапа. Retry использует существующий SaveCurrentState.

## NOT VERIFIED / вне scope

- Production unlock activation, новый choice UI/modal и полный Pause/touch flow выбора — P2.3d. Текущий public New Game all-open.
- Natural eight-wave unlock replay и balance approval с обоими стартами — P2.3e. Synthetic progression fixtures и rollback с реальными saved combat data не доказывают проходимость.
- Физический iPhone, built-player force quit/background/touch/safe area, performance/allocations/thermal, iOS player export/signing/TestFlight и hosted Unity CI этого head не проверены. Apple credentials не использовались.
- Изменений визуального оформления нет, новые screenshots не создавались. PlayMode reports доказывают исполнение сцен, не device readability. G2 и ART approval не закрываются.
- Независимое техническое review исходного PR head: WITH FIXES, 1 Important исправлен с RED→GREEN; повторное независимое approval не заявляется. Пользовательское PR approval/merge/release не выполнялись.

Начальный RED XML сохранён gzip без изменения исходных bytes (stack-trace Unity содержит trailing spaces). gates.json содержит hashes compressed и исходного XML.
