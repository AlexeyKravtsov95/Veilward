# P2.4b — Domain способности (#36)

## Scope и source

Зависимость PR #65 подтверждена через GitHub connector: `merged=true`, merge SHA `a7d07f7710a7358659848248643fec8af16a3e73`. Затем `git fetch origin main`, новый managed worktree и ветка `codex/p2.4b-ability-domain` от этого свежего origin/main. Пользовательские изменения исходного checkout не затрагивались. D-01…D-07 приняты владельцем17.11.2026 как начальные правила.

Reviewed implementation: `2be5385fbe011452644ab30116b59d43e4168777`. Final Domain/test source: `512e783adcdddaf6caa5e0cb132a36f1d3105fd7`; последующий evidence commit меняет только docs. [SHA256 всех changed Assets и compressed/raw XML](gates.json).

Scope: immutable definition/ID, read-only state, Check/Cast, targeting/cooldown/attribution и общий death/reward/completion handler. Acceptance REG190–197. Затронуты Domain, новые EditMode tests, текущая документация и новый evidence. Риски: stale/double cast, lethal leak, repeated reward/pending/bonus, sequence текущего tick и historical compatibility.

## VERIFIED

| Gate | Фактический результат |
|---|---|
| `./Scripts/run-static-checks.sh` | **38/38**, project checks PASS |
| `./Scripts/run-edit-mode-tests.sh` | **832/832**; из них **71 новый ability case**, прежние761 сохранены |
| `./Scripts/run-play-mode-tests.sh` | **45/45** |
| Failed / inconclusive / skipped, оба финальных Unity suites | **0 / 0 / 0** |
| `git diff --check` | PASS |
| `python3 Scripts/check_unity_project.py` | PASS: version/packages, paired meta/GUID/assemblies, Domain noEngineReferences |
| `python3 Docs/review/P2.4b/audit.py` | **796** прежних файлов побайтово неизменны |
| Scope/secrets audit | Только разрешённые Domain/tests/docs/README; новых packages/serialized assets/signing/credentials нет |

Audit включает все прежние tests/assertions, исторический Docs/review, Assets Settings/Art/Configs/Prefabs/Scenes, Packages и ProjectSettings. SHA256 manifest: `9a43e646738c52443474974d62d8ec8e5660e5fdc3ed8ac1c05a7f3801f1f734`. [Воспроизводимый audit script](audit.py), [результат](historical-audit.json). Исторические damage/status/reaction/economy/replay assertions и trace writers не ослаблялись и не менялись.

## RED → GREEN

1. Baseline на fresh main: полный EditMode **761/761** ([XML gzip](baseline-edit.xml.gz)).
2. Сначала добавлены BattleAbilityDefinitionTests/BattleAbilityTests. Initial API RED: Unity compiler подтвердил отсутствующие AbilityDefinitionId/command/result API; suite не запускался, XML отсутствовал — **не PASS** ([compiler errors](red-api.log)). Промежуточные ошибки fixture (обязательный DamageIntervalTicks, полный reaction catalog для native towers) исправлены до принятого behavioral RED.
3. Компилируемый контракт с ещё отсутствующим cast поведением: полный suite **826 =780passed/46failed**, все46 — новые ability cases; прежние761 PASS ([RED XML](red-edit.xml.gz)). Definitions и неизменные PathPosition guards уже проходили.
4. Минимальная реализация: **826/826**, включая65 новых cases ([before-review XML](edit-mode-before-review.xml.gz)), PlayMode45/45 ([XML](play-mode-before-review.xml.gz)).
5. Независимое fresh-context review GPT-6 Astra: одно Important I-01 по bounds при CellSize≠1; Critical/Minor нет. Вердикт **With fixes** на `2be5385` сохранён в [review](review.md).
6. Добавлены шесть non-unit bounds cases: полный RED **832 =829passed/3failed**, все три — новые cases, ожидаемый InvalidTarget↔NoTargets mismatch ([review RED XML](review-red.xml.gz)). Формула исправлена буквально по P2.4a §3, GridMapDefinition не менялся.
7. Final full EditMode **832/832** ([XML](edit-mode.xml.gz)), static38/38 ([log](static.log)), PlayMode45/45 ([XML](play-mode.xml.gz)); все failed/inconclusive/skipped0. После EditMode изменены только язык комментариев/перенос строки test helper; gameplay и assertions неизменны, final PlayMode перекомпилировал assemblies. I-01 закрыт авторским RED→GREEN/full gates, **повторное независимое review не заявляется**.

## Команды и среда

Все команды из isolated worktree `/Users/alexeykravtsov/.codex/worktrees/p24b-ability-domain/Veilward`:

```bash
git fetch origin main
git switch -c codex/p2.4b-ability-domain
./Scripts/run-edit-mode-tests.sh
./Scripts/run-play-mode-tests.sh
./Scripts/run-static-checks.sh
python3 Scripts/check_unity_project.py
python3 Docs/review/P2.4b/audit.py
git diff --check
```

Точные counts/UTC run timestamps/хеши отчётов — gates.json. Unity6000.3.28f1 (`8f082d2fe476`), macOS27.0.1 (`26A434`) arm64, Editor Mono/target iOS, штатный auto→batch (`-batchmode -nographics -buildTarget iOS -runTests`, без `-quit`), без resident Editor. Это Editor tests, не iOS player build.

Static runner self-tests очищают TestResults; raw XML архивировались отдельно. До финального fix PlayMode повторялся для сохранения утраченного raw отчёта; до/после45/45. После каждого Unity suite проверялся и восстанавливался только serialization drift этой isolated worktree. Финальный byte audit подтверждает сохранение serialized baseline. Новых Unity source/test файлов8, все с уникальными `.meta`; существующие GUID/ссылки не менялись.

## Architecture notes

Check/Cast имеют один validation path и синхронную границу между Steps. Failures не меняют state/grid/IDs/army/progression/ability/statuses/events/counters; тесты сравнивают полное содержимое четырёх **непустых** buffers. Target capture/EnemyId order/captured positions до damage; inclusive radius; finite half-open center по Origin+scaled extent, без grid snap/occupancy. Reused buffer выделен заранее по validated mission spawn budget.

Damage120/radius1.5/cooldown600CombatSteps, initially Ready; без Gold/charges. Armor/Crack/min1/HP clamp и long total, без Gust/status/reaction effects. Cast не advance tick и не меняет tower cooldown. Cooldown уменьшается перед Spawn/Burn каждого Combat Step и переносится между волнами. Common death/reward/completion сохраняет прежний leak-before-tower порядок и Defeat priority, saturated one-time rewards/bonus, pending3/6 и completion в текущем tick только после исчерпания schedule.

DamageOrigin.Ability appended, optional source/correlation fields не меняют старые call sites/AttackEvent/trace writer; Ability/Reaction/Damage используют общий sequence, Clear не reset sequence. Нет fake tower source/AttackEvent.

## NOT VERIFIED / BLOCKED и следующие задачи

- **Production/public factories остаются ability-disabled.** Новая способность проверена только explicit internal synthetic Domain fixture; это не пользовательский enabled content.
- Enabled ExportCheckpoint, Restore и validation явно unsupported до #37: schema3 не может потерять cooldown/sequence молча. **Schema4/enabled saves/rollback не реализованы**; старые saves продолжают прежние правила.
- Ability UI/input/preview/Confirm/Pause dispatch/four-stream consume — #38. Domain не знает Pause; существующая orchestration/Pause regression проходит в полных suites. Новая ability UI/device acceptance не заявляется.
- **Boss, новая wave8/content и natural ability/boss replay не реализованы** — #39/#40. Прежняя волна8/баланс сохранены.
- Physical iPhone, touch/safe area/background/force quit, iOS player/IL2CPP/Metal/profiling/thermal, self-hosted Unity CI, final balance/G2 и ART approval не проверялись.
- Новый визуальный результат отсутствует; Play Mode screenshots для этого pure Domain PR неприменимы. UI/boss PR потребуют реальные кадры. Merge/release/TestFlight не выполнялись; worktree сохранён для review.
