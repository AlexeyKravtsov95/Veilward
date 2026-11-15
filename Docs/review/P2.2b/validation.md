# P2.2b — definitions и additive event contracts

Только **Task 1** [плана](../../P2.2A_IMPLEMENTATION_PLAN.md), по прямому запросу владельца. База — `7f60377`, свежий `origin/main` с merged PR #18. Ветка — `feature/p2-2b-reaction-definitions`, отдельный managed worktree. Все обязательные исходные документы присутствуют и прочитаны.

Scope: шесть новых Domain files с `.meta`, `TowerDefinition`, `TowerArchetypeDefinition`, `BattleReactionDefinitionTests`, regression-таблица и этот evidence. Acceptance: immutable/copy semantics, matrix ниже, additive source compatibility, payload событий строго spec §5. Риски: Steam total vs Storm additional cap, потеря distinctions damage/HP, несовместимость прежних constructors.

## Архитектура и контракт

- `ReactionPayload`/`ReactionDefinition` — sealed immutable reference types, только get-properties. `ReactionCatalog` копирует входной список и публикует `Array.AsReadOnly`; lookup dictionary приватен. Immutable entries можно безопасно разделять между каталогами.
- `Definitions` сохраняет порядок входа; `Get` бросает `KeyNotFoundException` для отсутствующего вида, `TryGet` возвращает false/null. `Empty` — явный пустой каталог. Null/невалидный input не превращается в Empty.
- Общие constructors допускают любые положительные cooldown/percent/finite range в пределах int/float и валидные caps; production examples не зашиты как единственные допустимые числа. Priority уникален в каталоге; точные production priorities 0/1 проверит следующий config-этап.
- `TowerDefinition` добавляет только последний optional `intrinsicReaction = null`, запрещает status+intrinsic. Публичный трёхуровневый `TowerArchetypeDefinition` проверяет matching Steam/Storm payload каждого уровня; базовые status requirements и прежний compatibility path сохранены.
- `ReactionEvent` и `DamageResolvedEvent` — readonly structs, snapshot positions/IDs/sequence, без Scene/Unity references. Events здесь только данные, они **ещё не публикуются**. Спецификация остаётся источником правил producer на следующем этапе; event constructors не исполняют damage/resolver и не проверяют допустимость боевой последовательности.

## Invalid-input matrix / REG-BATTLE-128…130

Все имена ниже имеют префикс `BattleReactionDefinitionTests.`; полная привязка есть в [REGRESSION.md](../../REGRESSION.md).

| Input | Ожидание | Фактический test |
|---|---|---|
| Kind -1, 2, int.MaxValue | `ArgumentOutOfRangeException` | `RegBattle129_Payload_RejectsUnknownKind` |
| null catalog input / null payload definition | `ArgumentNullException` | `RegBattle128_CatalogAndDefinition_RejectNullAndDuplicateData` |
| null entry, duplicate Kind с разными priorities, duplicate priority с разными Kind | `ArgumentException` | `RegBattle128_CatalogAndDefinition_RejectNullAndDuplicateData` |
| Cooldown int.MinValue, -1, 0 | `ArgumentOutOfRangeException` | `RegBattle129_Definition_RejectsNonpositiveCooldown` |
| Percent int.MinValue, -1, 0 — оба Kind | `ArgumentOutOfRangeException` | `RegBattle129_Payload_RejectsNonpositivePercent` |
| Range float.MinValue, -1, -0, 0, NaN, -Infinity, +Infinity — оба Kind | `ArgumentOutOfRangeException` | `RegBattle129_Payload_RejectsNonpositiveAndNonfiniteRange` |
| Steam limit int.MinValue, -1, 0, 5, int.MaxValue | `ArgumentOutOfRangeException` | `RegBattle129_Payload_RejectsInvalidTargetLimit` |
| Storm limit int.MinValue, -1, 0, 3, int.MaxValue | `ArgumentOutOfRangeException` | `RegBattle129_Payload_RejectsInvalidTargetLimit` |
| Steam limits 1/2/3/4 total; Storm 1/2 additional | ACCEPT | `RegBattle129_ValidBoundaries_DoNotHardcodeProductionValues` |
| Percent/cooldown 1 и int.MaxValue; range float.Epsilon и float.MaxValue; priorities 7/42 | ACCEPT, значения сохранены | `RegBattle129_ValidBoundaries_DoNotHardcodeProductionValues` |
| Steam 60/100/1.25/4 priority 0; Storm 60/75/2.0/2 priority 1 | ACCEPT, каждое поле сохранено | `RegBattle128_GlobalExamples_PreserveEveryConfiguredValue` |
| source list replace/clear после constructor; изменение read-only view | Каталог неизменен; view mutation бросает `NotSupportedException` | `RegBattle128_Catalog_CopiesDefinitionsAndSupportsLookupAndEmpty` |
| hybrid L1/L2/L3: payload отсутствует, wrong Kind или status вместо intrinsic — оба типа | `ArgumentException` на каждом уровне | `RegBattle130_HybridArchetype_RequiresMatchingPayloadOnEveryLevel` |
| Каждый StatusKind одновременно с каждым intrinsic Kind | `ArgumentException` | `RegBattle130_Tower_RejectsStatusAndIntrinsicTogether` |
| Basic archetype без статуса / с intrinsic вместо статуса | `ArgumentException`; обычный status принимается | `RegBattle130_BasicArchetypes_StillRequireSingleStatus` |

Дополнительная проверка чувствительности: временно убрана только верхняя граница targetLimit. Реальный EditMode run: **373 total / 369 passed / 4 failed / 0 skipped/inconclusive**; упали именно Steam 5/int.MaxValue и Storm 3/int.MaxValue в `RegBattle129_Payload_RejectsInvalidTargetLimit`. Ограничение восстановлено до финальных gates. Это отрицательный контроль теста, а не состояние PR. Первоначальный RED до definitions завершился отсутствующими типами и отсутствующим XML; он не считается исполнением runtime assertions.

## Compatibility evidence / REG-BATTLE-130…131

`RegBattle130_LegacyConstructors_PreserveStatusPenetrationAndInterval` компилирует прежние positional calls из 5, 6 и 7 аргументов: legacy 60/3/2/25 остаётся без статуса/penetration, interval 15; status-only сохраняет исходный объект и interval 30; full status+penetration 7 сохраняет объект и rounded interval 38 при 0.8 fireRate. Во всех случаях `IntrinsicReaction == null`.

`RegBattle130_HybridArchetype_RequiresMatchingPayloadOnEveryLevel` принимает три разных payload levels каждого гибрида, очищает исходный levels list и проверяет неизменность архетипа/недоступность записи через IList. `RegBattle130_BasicArchetypes_StillRequireSingleStatus` сохраняет требования четырёх basic IDs.

`RegBattle131_ReactionEvent_PreservesCauseSourceGeometryAndSequence` проверяет Tick вплоть до long.MaxValue, общий sequence, вид/trigger/cause, source/primary IDs, range/limit и captured position. `RegBattle131_DamageEvent_PreservesDistinctDamageStagesAndCapturedPositions` проверяет Direct/Burn/Steam/оба Storm hops, nullable reaction correlation и все damage stages для lethal/nonlethal. `RegBattle131_DefinitionsAndEvents_ExposeOnlyImmutableData` проверяет отсутствие setters и readonly backing fields у пяти новых data types.

`RegBattle131_LegacyCombat_KeepsConfiguredAttackDamageAndDirectLethal` реально исполняет прежнюю симуляцию: configured 25, armor 10, penetration 3 → потеря 18 HP; при HP 100 остаётся 82 и WasLethal false, при HP 3 цель удаляется и WasLethal true. `AttackEvent.Damage` остаётся 25 в обоих случаях, captured position сохраняется после removal. Все прежние 317 EditMode tests также сохранены.

## VERIFIED

**38/38 static self-tests + project checks PASS; 378/378 EditMode (61 новых cases), 15/15 PlayMode; failed/inconclusive/skipped = 0; `git diff --check` PASS.**

Точные результаты, SHA256 XML и SHA256 проверенных исходников — [gates.json](gates.json). Локальные XML/log находятся в ignored `TestResults/` изолированного worktree.

Окружение: macOS 27.0.1 (26A434), arm64; Unity **6000.3.28f1 (8f082d2fe476)**, runner `auto` выбрал fresh batch Editor без resident instance, `-batchmode -nographics -buildTarget iOS`. Проект нацелен на iOS 17.0 / IL2CPP / ARM64; это Editor tests для iOS target, не IL2CPP player build/device run.

Команды из корня worktree:

```bash
./Scripts/run-static-checks.sh
./Scripts/run-edit-mode-tests.sh
./Scripts/run-play-mode-tests.sh
git diff --check
```

Побочная пересериализация после Unity suites восстанавливается только в этом isolated worktree. В PR нет изменений config assets, сцен, prefabs, материалов, render settings, packages или версии Unity. Пользовательские изменения основного checkout сохранены.

## NOT VERIFIED / BLOCKED

**Runtime реакций ещё не реализован.** Resolver, enemy cooldowns, targeting/sequence producer, damage arithmetic saturation при int.MaxValue, новые event buffers/publication, production configs/гибриды, saves integration, VFX/UI относятся к P2.2c–f. Эти результаты их не подтверждают; REG-132…163 остаются SPECIFIED.

Новый player/iOS build, physical iPhone, touch/safe area/background, force quit/relaunch, performance/allocations/thermal, Unity CI, внешние playtests и balance approval не выполнялись. Числа остаются гипотезами. Визуальных изменений нет; новые screenshots для этого definitions-only PR неприменимы. Self-merge/release не выполняются; требуется независимое review.
