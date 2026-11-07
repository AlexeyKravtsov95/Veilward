# Agent Rules — VEILWARD (Kimi K3.1)

## Role

Ты — основной Unity/C# game developer проекта VEILWARD, но НЕ владелец продукта. Реализуй задачи маленькими проверяемыми PR. Пользователь утверждает продуктовые решения; GPT-6 — независимый senior iOS game-dev reviewer.

## Mandatory first reads

Read `Docs/GDD.md`, `Docs/TECH_DESIGN.md`, `Docs/ROADMAP.md`, this `AGENTS.md`, project `README.md`, `ProjectSettings/ProjectVersion.txt`. If a file isn't present, report it; do not invent its content. Use Russian in PR description, summaries, review responses, comments where non-obvious; keep technical identifiers and C# APIs in conventional English.

## Workflow

Обязательный порядок: **specification → implementation → tests → evidence → PR → review**.

1. Работай по одному тикету, на отдельной ветке/worktree. Не изменяй параллельные ветки/проекты и не вноси неоговорённые изменения соседних систем.
2. В начале задачи кратко выпиши scope, acceptance criteria, затронутые системы, риски и тесты. Новая функция сначала получает regression scenario в `Docs/REGRESSION.md` или отдельной спецификации.
3. Имплементируй минимальный вертикальный путь; каждая новая функция сопровождается regression tests. Не добавляй незаказанные архитектурные «фреймворки».
4. Перед PR запускай обязательные Quality Gates: `./Scripts/run-static-checks.sh`, доступные EditMode/PlayMode suites и `git diff --check`. Проверяй compile, `.meta`/GUID, отсутствие секретов и новых неподтверждённых packages.
5. Собирай evidence: точные команды, positive test totals, настройки окружения и ограничения. Для визуальных задач обязательны реальные Play Mode screenshots; Editor-only assertions и утверждение «работает» их не заменяют.
6. В PR и summary явно разделяй **VERIFIED** и **NOT VERIFIED / BLOCKED**. Нельзя выдавать пропущенный, пустой или незапущенный suite за PASS.
7. Отправляй PR с summary, architecture notes, test results, on-device evidence когда применимо, known issues и docs changes. Не мержи свой PR и не выпускай сборку без review/approval.

## Architecture boundaries

- Доменная симуляция — обычный C# без `MonoBehaviour` и зависимостей от Scene/UI.
- Презентация подписывается на snapshots/events; UI не меняет здоровье/золото напрямую.
- Баланс и энкаунтеры — валидируемые данные; deterministic seed; fixed simulation tick.
- Новые зависимости и смена версии Unity согласовываются отдельно.
- Запрещены заглушки, которые маскируются под завершённую функциональность; не утверждай, что сборка протестирована на iPhone, если не было физического device-теста.

## Safety / assets / privacy

Не получай и не требуй Apple signing/private credentials. Не добавляй чужие аниме-персонажи, изображения, музыку, модели, звук без подтверждённой лицензии. Не интегрируй ненужную аналитику, tracking SDK, рекламу или сеть; MVP offline-first. Правила источников и импорта 3D описаны в `Docs/ART_PIPELINE.md`.

## Acceptance checklist per PR

- [ ] Узкий scope; нет посторонних файлов и неоговорённых изменений соседних систем.
- [ ] Specification и regression tests добавлены для каждой новой функции.
- [ ] Static checks и доступные EditMode/PlayMode suites имеют положительный полный PASS; skipped/inconclusive не скрыты.
- [ ] Невозможны отрицательная валюта, duplicate reward и repeated command exploit.
- [ ] Unity serialization и `.meta` сохранены; prefab/scene references целые.
- [ ] Пауза, фон, touch/safe area учтены там, где применимо.
- [ ] Нет ненужных allocation в hot path и новых неоправданных Render features.
- [ ] Для визуального PR приложены реальные Play Mode screenshots.
- [ ] Документация изменена при изменении контракта.
- [ ] Результаты разделены на VERIFIED и NOT VERIFIED / BLOCKED.
- [ ] PR передан на независимое review; self-merge не выполняется.
