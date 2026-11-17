# Независимое review P2.4b

Reviewer: отдельный агент GPT-6 Astra, fresh context, read-only senior Unity/iOS review. Диапазон `a7d07f7710a7358659848248643fec8af16a3e73..2be5385fbe011452644ab30116b59d43e4168777`, новые tests и доступные working-tree docs/evidence. Checkout/index/HEAD не изменялись; Unity/static reviewer не запускал. Это technical review, не GitHub self-approval и не разрешение merge/release.

## Сильные стороны

Узкий pure Domain scope; production constructors ability-disabled; enabled persistence явно unsupported до #37. Check/Cast имеют единый порядок validation и атомарные отказы с full-state/counter/four nonempty buffers assertions. Полный capture до damage, EnemyId order, сохранение lethal positions, переиспользуемый target buffer по validated spawn budget. Общие armor/Crack и death/reward/completion; без Gust/status/reaction effects/fake AttackEvent. Точные600CombatSteps, stale/double/sentinel, pending3/6/future spawn/lethal boundaries и shared sequence покрыты.

## Findings

- Critical: нет.
- Important I-01: `BattleSimulation.cs:367–370` на reviewed SHA умножал Origin на CellSize. Принятый P2.4a §3 задаёт `[Origin, Origin+Width*CellSize)`. При OriginX=-6/Width12/CellSize2 код давал[-12,12) вместо[-6,18): X=-7 ошибочно допустим, X=13 ошибочно вне bounds. На production CellSize1 не проявлялось; прежние новые tests проверяли только1. Исправить только ability bounds и добавить Check/Cast non-unit regressions, не менять GridMapDefinition.
- Minor: нет.

**Вердикт reviewer: With fixes на `2be5385`.** Других actionable findings нет. Повторный review после fix pass не выполнялся; закрытие I-01 доказывается исполнителем через RED→GREEN и полные gates ниже/в validation.

## Независимо VERIFIED reviewer

Прочитаны mandatory docs/spec/этап#36/REG190–197 и все новые C# files. Разобраны raw XML: baseline761/761; RED826=780passed/46failed (все46 новые); GREEN826/826 (65 новых); PlayMode45/45. GREEN failed/skipped/inconclusive0. Read-only audit.py:796 файлов baseline побайтово совпали, manifest SHA256 `9a43e646738c52443474974d62d8ec8e5660e5fdc3ed8ac1c05a7f3801f1f734`. Diff check PASS.

## Declined to judge — решения исполнителя

1. Schema4/enabled saves/rollback/content routing — #37, текущий explicit unsupported сохраняется; риск до реализации исключён disabled production и отказом enabled persistence.
2. UI ownership/preview/Confirm/touch/Pause orchestration/four-stream consume — #38; production ability-disabled, Domain не знает UI/Pause. Прежние Pause suites остаются обязательными; ability UI/device acceptance не объявляется выполненным.
3. Boss/wave8/new production content — #39, старые serialized assets сохранены; риск баланса новой миссии остаётся за следующими задачами.
4. Natural ability/boss replay, balance/G2 — #40 и product acceptance; synthetic #36 не доказывает проходимость/баланс.
5. Physical iPhone/IL2CPP/Metal/allocations/frame time/thermal — отдельное device evidence, здесь NOT VERIFIED.
6. Existing GridMapDefinition geometry — соседний контракт не меняется; I-01 исправляется буквально по ability spec. При ошибке последующего mapping требуется отдельная задача, не скрытое изменение grid.
7. Последующий fix pass — новый reviewer pass не выполняется; авторская проверка обязана содержать RED→GREEN и full gates и не выдаётся за повторное независимое review.

## Закрытие I-01 исполнителем

**CLOSED by implementation + tests** на `512e783adcdddaf6caa5e0cb132a36f1d3105fd7`: шесть `RegBattle192_NonUnitCellSizeUsesSpecifiedOriginPlusScaledExtent` cases, из них три RED на исходной формуле (829passed/3failed из832) →832/832 GREEN после исправления. Финальные static38/38, PlayMode45/45, diff/meta/GUID/packages/history audit PASS. Raw XML/hashes в [gates.json](gates.json), команды/ограничения в [validation](validation.md). Независимый verdict выше сохранён как получен; это закрытие замечания исполнителем, не повторное review.
