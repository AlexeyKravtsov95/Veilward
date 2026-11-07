# PR checklist

## Scope и спецификация

- [ ] PR решает один тикет и не содержит неоговорённых изменений соседних систем.
- [ ] Указаны acceptance criteria и ссылки на specification/ADR.
- [ ] Каждая новая функция сопровождается regression tests; gameplay изменения перечислены явно.
- [ ] Описаны architecture notes, docs changes и known issues.

## Обязательные проверки

- [ ] `./Scripts/run-static-checks.sh` — положительный полный PASS.
- [ ] `./Scripts/run-edit-mode-tests.sh` — указаны Total/Passed/Failed/Inconclusive/Skipped.
- [ ] `./Scripts/run-play-mode-tests.sh` — указаны Total/Passed/Failed/Inconclusive/Skipped либо честный BLOCKED с причиной.
- [ ] `git diff --check` и compile выполнены; нет secrets или неподтверждённых packages.
- [ ] Для всех новых/перемещённых Unity assets сохранены `.meta`; duplicate/orphan GUID отсутствуют.
- [ ] Проверены prefab/scene/config/material references и отсутствие Missing Scripts.

## Gameplay и presentation

- [ ] Невозможны отрицательная валюта, duplicate reward и repeated command exploit.
- [ ] Пауза, фон, touch/safe area и hot-path allocations рассмотрены там, где применимо.
- [ ] Для визуальных изменений приложены реальные Play Mode screenshots; Editor-only tests не используются как их замена.
- [ ] Для device/performance claims приложены физический iPhone model, OS и измерения.

## VERIFIED

Перечислить точные команды, окружение, positive totals и ссылки на evidence.

## NOT VERIFIED / BLOCKED

Перечислить всё незапущенное и недоступное: hosted CI, device, signing, screenshots, performance. Пустой/skipped suite не записывать как PASS.

## Review

- [ ] PR готов к независимому review.
- [ ] Self-merge и release без review/approval не выполняются.
