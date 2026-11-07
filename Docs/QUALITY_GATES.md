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

`run-edit-mode-tests.sh` и `run-play-mode-tests.sh` используют общий `run-unity-tests.sh EditMode|PlayMode [auto|batch]`; default — `auto`.

- `auto` при доступном Unity CLI выполняет tests в уже открытом проекте, сохраняет JSON summary и строго проверяет counts. Этот локальный CLI path не обещает NUnit XML.
- Если CLI недоступен, но существует live Editor descriptor, `auto` завершается кодом **2**: второй Editor для того же проекта запрещён.
- Явный `batch` обязателен для self-hosted CI. Он не использует Unity CLI test result contract и создаёт `TestResults/<Mode>.xml` и `<Mode>.log` для обоих режимов. Resident Editor через CLI status либо live descriptor завершает `batch` кодом **2** до запуска executable.
- В clean checkout требуется executable `UNITY_EDITOR_PATH`; на macOS также проверяется Hub path для закреплённой версии. Batch command содержит `-batchmode -nographics -buildTarget iOS -runTests -testResults -logFile -quit`.
- Перед запуском удаляются stale XML, JSON и log выбранного режима. `python3 Scripts/verify_unity_test_results.py <xml-or-directory>` независимо принимает NUnit `<test-run>` или JUnit `<testsuite>/<testsuites>` и отклоняет missing/malformed/empty/failed/inconclusive/skipped reports. Nonzero Unity exit также остаётся ошибкой после проверки report.

`TestResults` находится в `.gitignore`. Unity CI workflow публикует NUnit XML и diagnostic logs как artifacts через `if: always()`; static workflow публикует свой diagnostic log.

### Scene & Asset Validation

`BattleAssetValidator` работает в `Veilward.Editor` и только читает сохранённые assets. Additive scene закрывается только если её открыл сам validator. Проверяются controller references, config creation, exact prefab paths, missing scripts, materials и shaders.

Reproducibility test сохраняет originals и `.meta` в памяти и гарантированно восстанавливает их в `finally`. Два Build сравниваются побайтово для config/prefabs/materials/`.meta`; scene YAML сравнивается после canonicalization автоматически переназначаемых Unity local fileIDs и их local references. GUID references и остальной serialized content не игнорируются. После tests нужен `git diff --exit-code -- Assets Packages ProjectSettings` в чистом tracked tree.

## GitHub Actions

| Workflow / job | Триггер и runner | Статус |
|---|---|---|
| `Quality Gates` / `static-checks` | PR в `main`, push в `main`, manual dispatch; `ubuntu-latest` | Включён; не зависит от Unity license |
| `Unity Personal Tests` / `unity-tests` matrix EditMode/PlayMode | Только manual dispatch доверенного `main`; `[self-hosted, macOS, veilward-unity-personal]` | **NOT VERIFIED / BLOCKED** до регистрации runner и полного manual PASS |

`Quality Gates` остаётся отдельным hosted static workflow. Он не запускает Unity и не обращается к self-hosted runner.

`Unity Personal Tests` находится в `.github/workflows/unity-personal-tests.yml` и имеет только `workflow_dispatch`. Job дополнительно проверяет repository, event и `refs/heads/main`. Checkout закреплён на `github.sha` инициированного dispatch, а step сравнивает фактический `git rev-parse HEAD` с ожидаемым SHA; плавающий `main` не используется. Manual dispatch с другой веткой не является Unity PASS. Matrix выполняет EditMode и PlayMode последовательно (`max-parallel: 1`) с `contents: read`, protected environment `unity-personal-ci`, явным `batch` backend и независимым NUnit XML validator. Пустой, missing, malformed, failed, inconclusive или skipped report завершает gate ошибкой.

Workflow **не активирует лицензию** и не содержит `UNITY_LICENSE`, Unity account credentials, serial, GameCI или ALF/ULF. Он использует Unity **6000.3.28f1**, установленный через Unity Hub по стандартному macOS path; при необходимости локальный runner поддерживает `UNITY_EDITOR_PATH`. Для Personal активация выполняется владельцем через Unity Hub до запуска CI. При истечении или потере активации владелец повторяет её в Hub; агент не запрашивает и не получает пароль Unity.

## Подготовка изолированного macOS runner — действия владельца

Репозиторий публичный. GitHub предупреждает, что self-hosted runner не является ephemeral-окружением и может быть устойчиво скомпрометирован непроверенным кодом. Поэтому runner нельзя подключать к автоматическим PR/push jobs, `pull_request_target`, `workflow_run` с PR artifacts или другим триггерам, способным передать ему непроверенный код. Официальные рекомендации: [GitHub secure use reference](https://docs.github.com/en/actions/reference/security/secure-use), [Unity Hub license management](https://docs.unity.com/en-us/hub/manage-license).

### 1. Защитить GitHub configuration до регистрации

1. В **Settings → Actions → General** для fork pull request workflows выбрать наиболее строгий режим **Require approval for all outside collaborators**. Не одобрять запуск PR workflow, который добавляет или изменяет `runs-on` для self-hosted runner, без отдельного security review.
2. Создать environment **`unity-personal-ci`**, ограничить deployment веткой **`main`** и назначить владельца required reviewer. Workflow не должен использовать environment secrets.
3. Runner зарегистрировать только для `AlexeyKravtsov95/Veilward`, не делать его общим organization runner. Обязательный custom label: **`veilward-unity-personal`**. Регистрационный token вводит только владелец в GitHub/terminal; агенту он не передаётся.
4. Не добавлять `pull_request` или `push` в `Unity Personal Tests`. Даже после успешного manual run автоматическое выполнение на self-hosted runner не включается.

### 2. Изолировать macOS и активировать Unity Personal

1. Использовать отдельный Mac, VM либо выделенную непривилегированную macOS user account только для этого runner. На машине не должно быть личных файлов, SSH keys, Apple signing credentials, production access или passwordless `sudo`; ограничить доступ runner к внутренней сети.
2. В графической сессии этой учётной записи владелец устанавливает через Unity Hub точную версию **6000.3.28f1** и **iOS Build Support**. Смена Unity version или dependencies отдельно не разрешена этим тикетом.
3. Владелец сам входит в Unity Hub и активирует **Unity Personal** через **Settings → Licenses**. Пароль, license files и registration tokens не записываются в репозиторий, workflow, artifacts или logs.
4. GitHub Actions runner запускается под той же выделенной macOS account, где активирован Hub, без `sudo`. Не запускать второй Unity Editor для уже открытого проекта; общий runner сам распознаёт live Editor descriptor и завершится кодом 2.

### 3. Зарегистрировать runner и подтвердить полный PASS

1. Владелец регистрирует runner с label `veilward-unity-personal` и проверяет, что он online только для этого репозитория. До этого момента Unity CI остаётся **NOT VERIFIED / BLOCKED**.
2. В **Actions → Unity Personal Tests → Run workflow** выбрать **`main`**, запустить workflow и подтвердить protected environment. Offline/отсутствующий runner, queued job или skipped matrix не считаются PASS.
3. Открыть оба job и artifacts. Требуются одновременно:
   - EditMode: `Total > 0`, `Passed == Total`, `Failed == Inconclusive == Skipped == 0`;
   - PlayMode: те же условия;
   - отсутствие license и compilation errors в diagnostic logs;
   - оба job завершены успешно, artifacts содержат XML и logs.
4. Только после такого полного manual PASS владелец может изменить статус в этой документации на **VERIFIED**, указав run URL, commit SHA и точные totals. Повторный запуск обязателен после изменения Unity, test runner, license state или workflow security contract.

## Зафиксированные результаты

### P0.3 — предыдущий baseline

- Static self-tests: **23/23 PASS**; project static checks PASS.
- EditMode: **158/158 PASS**, 0 failed/inconclusive/skipped.
- PlayMode: **1/1 PASS**, 0 failed/inconclusive/skipped.
- Generated tracked outputs после reproducibility suite: без diff.
- GitHub Actions `Static Unity project checks`: **PASS**, run `42368103741`; отдельный clean Git archive check также PASS.

### P0.3.1 — Unity Personal CI Adaptation

- Static self-tests, включая `REG-CI-001…004`: **32/32 PASS**; project static checks PASS.
- Fake batch runner: EditMode/PlayMode NUnit XML и logs **PASS**; missing/stale, failed, skipped, inconclusive, empty, nonzero exit и open Editor rejection **PASS**. Локальный fake CLI async path также **PASS**.
- GitHub Actions `Static Unity project checks` в PR #7: **PASS**, run `42377053685`.
- YAML syntax обоих workflows: **PASS**; `git diff --check`: **PASS**; tracked `Assets`, `Packages`, `ProjectSettings`: без diff.
- Локальные EditMode/PlayMode: **NOT VERIFIED / BLOCKED**, оба runner завершились кодом **2** из-за live Editor descriptor при недоступном Unity CLI; второй Editor не запускался.
- GitHub self-hosted Unity EditMode/PlayMode: **NOT VERIFIED / BLOCKED** — runner не зарегистрирован, manual run не выполнялся.
- Gameplay, Unity version/revision и dependencies не изменялись.

## NOT VERIFIED / BLOCKED

- GitHub self-hosted Unity EditMode/PlayMode execution — **BLOCKED** до действий владельца и полного manual PASS по процедуре выше.
- Локальные EditMode/PlayMode и direct batch fallback — **BLOCKED** текущим live Editor descriptor при недоступном Unity CLI; реальный Unity CLI resident path также **NOT VERIFIED**. Fake CLI contract tests выполнены отдельно.
- Физический iPhone, touch/safe area, thermal/performance и signing — **NOT VERIFIED** этим тикетом.
