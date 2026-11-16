# P2.3b — Domain element unlocks evidence

Проверено 2026-11-17 (Asia/Novosibirsk), база `b82222b` — merged PR #26. Unity **6000.3.28f1 (8f082d2fe476)**; macOS **27.0.1 (26A434), arm64**. Проверенный source commit: `d1fa17004b732786fb1c61bdded8ede7c516886a` (следующий commit содержит только evidence). Изолированный worktree, ветка `codex/p2-3b-domain-element-unlocks`.

## VERIFIED

- Native progression доступна через internal `BattleScenarioConfig.CreateSimulation(ElementUnlockRules)` и полные Domain catalog constructors с optional rules. Factory проверяет Flame/Gale, 3/6 и 8 волн и использует существующие validated saved combat data. Public production/historical factories сохраняют all-open.
- Confirm публикует новый immutable progression после phase → mode → pending → mismatch → option → already unlocked → credit. До старта обязателен Flame/Gale; wave completion 3/6 атомарно начисляет bonus/earned/pending; повторный Step не начисляет ничего, Defeat имеет приоритет.
- Requirements — явные immutable данные шести архетипов для всех L1–L3; права не зависят от карточек/статусов/компонентов на поле/порядка открытий. Place и availability используют один путь checks; Start/Next используют свои read-only queries. Upgrade сохраняет ID/cell/cooldown/investment semantics, Sell — прежний 70% refund без возврата progression.
- Native checkpoint export бросает `NotSupportedException`; validation/restore отклоняются целиком до mutation. DTO/schema/assets/UI не изменены.
- REG-164…172 Domain автоматизированы: **43 новых test cases**, включая все **12 путей** выбора, оба стартовых бюджета, Defeat на 3/6, stale/double Confirm, атомарность, точную таблицу requirements и query/command parity. [Полный список имён и hashes](gates.json), [независимое review и закрытое замечание](review.md).

## Команды и результаты

Команды запускались из корня worktree:

```sh
./Scripts/run-static-checks.sh
./Scripts/run-edit-mode-tests.sh
./Scripts/run-play-mode-tests.sh
git diff --check
```

| Run | Total | Passed | Failed | Skipped | Inconclusive | Evidence |
|---|---:|---:|---:|---:|---:|---|
| Начальный RED до production-кода | 549 | 547 | 2 | 0 | 0 | [XML](initial-red.xml) |
| GREEN начальной реализации | 584 | 584 | 0 | 0 | 0 | локальный промежуточный run |
| RED проверки состава requirements после review | 590 | 584 | 6 | 0 | 0 | [XML](requirements-mutation-red.xml) |
| **Финальный EditMode** | **590** | **590** | **0** | **0** | **0** | [XML](EditMode.xml) |
| **Финальный PlayMode** | **30** | **30** | **0** | **0** | **0** | [XML](PlayMode.xml) |
| **Static self-tests** | **38** | **38** | **0** | **0** | **0** | [stdout](static-checks.txt) |

Static project checks (Unity version/revision, package baseline/lock, `.meta`, GUID, asmdef) — PASS. `git diff --check` — PASS. Domain остаётся `noEngineReferences: true`; packages/serialized configs и боевые данные не менялись, прежние synthetic/legacy assertions не ослаблялись. Generated scene/material/settings drift от existing builder suites восстановлен к базе только в изолированном worktree.

Начальный RED использовал два временных compile-safe reflection probe: `RegBattle165_NativeFactory_RequiresExplicitRules` и `RegBattle164_Requirements_AreExplicitArchetypeData`. Оба упали ожидаемо с `Expected: not null / But was: null`; прежние 547 тестов прошли. После появления API probes заменены полноценными behavior/definition tests в двух итоговых suites. В первом expanded run обнаружен compile typo нового fixture (пропущен обязательный `EnemyDefinition.reward`), XML не создан, этот запуск **не PASS**; аргумент исправлен до GREEN.

После review в шесть definition cases добавлен неверный состав при правильном количестве requirements. Для проверки чувствительности тестов временно отключено только сравнение значений в native validator: все шесть ожидаемо упали (`Expected ArgumentException / But was null`), остальные 584 прошли. Затем исходный reviewed validator восстановлен и обе полные suites повторены. Mutation в PR не входит.

Runners использовали доступный Unity Hub executable с `-batchmode -nographics -buildTarget iOS -runTests -testPlatform EditMode|PlayMode`, без `-quit`; каждый реальный NUnit report принят repository XML validator. XML сохранены в отдельной папке до следующего static run, поскольку существующие fake-backend self-tests перезаписывают корневой `TestResults`. Это не подмена Unity результата fake summary.

## NOT VERIFIED / следующие этапы

- P2.3c: schema 3, persistence, reload-часть REG-169, миграция и файловый rollback native progression. Native export/import сейчас намеренно unsupported.
- P2.3d/e: production activation, UI/locked cards/choice modal/Pause flow, natural eight-wave replay/баланс с обоими стартами. Новый режим сейчас не включён в обычный матч; synthetic wave fixtures доказывают Domain-контракт, а не проходимость saved EightWave.
- Physical iPhone, touch/safe area/background/force quit, allocations/thermal/performance, iOS player build/signing/TestFlight и hosted Unity CI этого head не проверены. Apple credentials не использовались.
- Визуальных изменений нет; новые screenshots не снимались. Старое evidence сохранено. Независимое техническое code review — PASS; пользовательское PR review/approval, merge и release не выполнены.
