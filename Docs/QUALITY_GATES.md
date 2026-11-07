# VEILWARD — Quality Gates

## Обязательные локальные проверки

Запуск из корня репозитория:

```bash
./Scripts/run-static-checks.sh
./Scripts/run-edit-mode-tests.sh
./Scripts/run-play-mode-tests.sh
git diff --check
```

Перед PR требуется положительный полный результат каждого применимого suite. Пустой, missing, malformed, failed, inconclusive или skipped result не является PASS.

### Static checks — без Unity license

`Scripts/run-static-checks.sh` сначала запускает stdlib unittest suite для самих validators, затем проверяет проект:

- точные Unity version/revision из `Scripts/unity-project-baseline.json`;
- frozen direct dependencies и соответствующие lock entries;
- paired/missing/orphan `.meta`, 32-hex GUID и duplicate GUID;
- JSON Assembly Definitions, уникальные имена и локальные references;
- `Veilward.Domain.noEngineReferences == true` и Editor-only constraints для Editor/EditMode assemblies.

Любая ошибка даёт ненулевой exit code. Изменение baseline является отдельным согласуемым изменением, а не способом скрыть drift.

### Unity runners и строгая проверка results

`run-edit-mode-tests.sh` и `run-play-mode-tests.sh` используют общий `run-unity-tests.sh`.

- При доступном Unity CLI команды выполняются в уже открытом проекте; результат дополнительно проверяется по counts.
- Если CLI недоступен, но существует live Editor descriptor, runner завершается кодом **2**: второй Editor для того же проекта запрещён.
- В clean checkout требуется executable `UNITY_EDITOR_PATH`; на macOS также проверяется Hub path для закреплённой версии. Batch command содержит `-batchmode -nographics -buildTarget iOS -runTests -testResults -logFile -quit`.
- `python3 Scripts/verify_unity_test_results.py <xml-or-directory>` независимо принимает NUnit `<test-run>` или JUnit `<testsuite>/<testsuites>` и отклоняет missing/malformed/empty/incomplete reports.

`TestResults` находится в `.gitignore`. Hosted workflow публикует XML и diagnostic logs как artifacts через `if: always()`.

### Scene & Asset Validation

`BattleAssetValidator` работает в `Veilward.Editor` и только читает сохранённые assets. Additive scene закрывается только если её открыл сам validator. Проверяются controller references, config creation, exact prefab paths, missing scripts, materials и shaders.

Reproducibility test сохраняет originals и `.meta` в памяти и гарантированно восстанавливает их в `finally`. Два Build сравниваются побайтово для config/prefabs/materials/`.meta`; scene YAML сравнивается после canonicalization автоматически переназначаемых Unity local fileIDs и их local references. GUID references и остальной serialized content не игнорируются. После tests нужен `git diff --exit-code -- Assets Packages ProjectSettings` в чистом tracked tree.

## GitHub Actions

Workflow: `.github/workflows/quality-gates.yml`.

| Job | Триггер | Статус |
|---|---|---|
| `static-checks` | PR в `main`, push в `main`, manual dispatch | Включён; не зависит от Unity license |
| `unity-tests` matrix EditMode/PlayMode | Manual dispatch либо `VEILWARD_UNITY_CI=enabled` | **BLOCKED** до license setup |

Job `unity-tests` использует `game-ci/unity-test-runner@v4`, `max-parallel: 1`, artifacts и отдельный строгий XML validator. При automatic run отсутствие полного secret set является ошибкой, а не success skip.

## Включение hosted Unity CI — действия владельца

Сейчас hosted Unity tests **BLOCKED**: лицензия и secrets в репозиторий не добавлялись, hosted Unity PASS отсутствует.

1. В GitHub repository secrets добавить один полный набор GameCI:
   - Personal: `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`; либо
   - Professional: `UNITY_SERIAL`, `UNITY_EMAIL`, `UNITY_PASSWORD`.
2. Не передавать значения агенту и не записывать их в файлы/logs. Запустить `Quality Gates` через **Run workflow** (manual dispatch). Manual run допускается без repository variable, но требует secrets.
3. Открыть оба matrix job и artifacts. Убедиться, что EditMode и PlayMode имеют `Total > 0`, `Passed == Total`, нулевые failed/inconclusive/skipped, а diagnostic logs не содержат license/compilation errors.
4. Только после успешного manual run установить repository variable `VEILWARD_UNITY_CI=enabled`. После этого matrix обязателен на PR/push в `main`.

Для альтернативного локального macOS runner нужны установленные Unity **6000.3.28f1**, iOS Build Support и активная лицензия; затем используются те же scripts. Этот вариант не заменяет проверку hosted workflow.

## Зафиксированные локальные результаты P0.3

- Static self-tests: **23/23 PASS**; project static checks PASS.
- EditMode: **158/158 PASS**, 0 failed/inconclusive/skipped.
- PlayMode: **1/1 PASS**, 0 failed/inconclusive/skipped.
- Generated tracked outputs после reproducibility suite: без diff.
- GitHub Actions `Static Unity project checks`: **PASS**, run `42368103741`; отдельный clean Git archive check также PASS.

## NOT VERIFIED / BLOCKED

- Hosted GitHub Unity EditMode/PlayMode execution — **BLOCKED** до действий владельца выше.
- Direct batch fallback и реальный Unity CLI resident path — **NOT VERIFIED** в текущем окружении; fake CLI contract tests и local Pipeline suites выполнены отдельно.
- Физический iPhone, touch/safe area, thermal/performance и signing — **NOT VERIFIED** этим тикетом.
