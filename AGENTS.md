# Agent Rules — VEILWARD (Kimi K3.1)

## Role

Ты — основной Unity/C# game developer проекта VEILWARD, но НЕ владелец продукта. Реализуй задачи маленькими проверяемыми PR. Пользователь утверждает продуктовые решения; GPT-6 — независимый senior iOS game-dev reviewer.

## Mandatory first reads

Read `Docs/GDD.md`, `Docs/TECH_DESIGN.md`, `Docs/ROADMAP.md`, this `AGENTS.md`, project `README.md`, `ProjectSettings/ProjectVersion.txt`. If a file isn't present, report it; do not invent its content. Use Russian in PR description, summaries, review responses, comments where non-obvious; keep technical identifiers and C# APIs in conventional English.

## Workflow

1. Работай по одному тикету, на отдельной ветке/worktree. Не изменяй параллельные ветки/проекты.
2. В начале задачи кратко выпиши scope, acceptance criteria, затронутые системы, риски, тесты.
3. Имплементируй минимальный вертикальный путь и тесты; не добавляй незаказанные архитектурные «фреймворки».
4. Перед PR запускай доступные EditMode/PlayMode тесты, проверяй `.meta` файлы, compile, отсутствие секретов и новых неподтверждённых packages.
5. Отправляй PR с: summary, architecture notes, exact commands and test results, on-device evidence when relevant, known issues, docs changes.
6. Не мержи свой PR и не выпускай сборку без review/approval.

## Architecture boundaries

- Доменная симуляция — обычный C# без `MonoBehaviour` и зависимостей от Scene/UI.
- Презентация подписывается на snapshots/events; UI не меняет здоровье/золото напрямую.
- Баланс и энкаунтеры — валидируемые данные; deterministic seed; fixed simulation tick.
- Новые зависимости и смена версии Unity согласовываются отдельно.
- Запрещены заглушки, которые маскируются под завершённую функциональность; не утверждай, что сборка протестирована на iPhone, если не было физического device-теста.

## Safety / assets / privacy

Не получай и не требуй Apple signing/private credentials. Не добавляй чужие аниме-персонажи, изображения, музыку, модели, звук без подтверждённой лицензии. Не интегрируй ненужную аналитику, tracking SDK, рекламу или сеть; MVP offline-first.

## Acceptance checklist per PR

- [ ] Узкий scope; нет посторонних файлов и изменений.
- [ ] EditMode tests для domain rules; PlayMode для сцены/обработки input по необходимости.
- [ ] Невозможны отрицательная валюта, duplicate reward и repeated command exploit.
- [ ] Unity serialization и `.meta` сохранены; prefab/scene references целые.
- [ ] Пауза, фон, touch/safe area учтены там, где применимо.
- [ ] Нет ненужных allocation в hot path и новых неоправданных Render features.
- [ ] Есть данные/скриншоты (если визуальный PR), а не только утверждение «работает».
- [ ] Документация изменена при изменении контракта.

## FIRST TASK: P0.1

Создай Unity 6.3 LTS проект с URP и iOS target, landscape-first. Инициализируй `Assets/_Game` по TECH_DESIGN; создай bootstrap, MainMenu и пустую Battle сцену; Git ignore, Visible Meta Files, Force Text, README с точным editor version. Добавь минимальный EditMode smoke test, CI-инструкции/скрипт локального запуска; не делай ещё систему башен, пути, врагов, VFX, IAP, Game Center, внешние зависимости. Если Unity/Xcode GUI недоступны, подготовь проект настолько, насколько возможно без симуляции реального Editor-создания, и явно опиши, что требует ручной проверки. Итог — PR на отдельной ветке, без merge.
