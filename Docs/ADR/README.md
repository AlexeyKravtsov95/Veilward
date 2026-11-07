# Architecture Decision Records

ADR фиксирует значимое архитектурное решение, а не ежедневный progress. Номер не переиспользуется; статус меняется отдельной записью при supersede. Описание и rationale — на русском, технические идентификаторы — на английском.

Имя файла: `NNNN-short-decision-title.md`.

```markdown
# NNNN. Краткое название решения

- Дата: YYYY-MM-DD
- Статус: Proposed | Accepted | Deprecated | Superseded by NNNN

## Контекст

Какая проблема, ограничения, альтернативы и evidence требуют решения?

## Решение

Что именно выбрано и каковы границы решения?

## Последствия

Положительные и отрицательные последствия, migration/rollback, влияние на tests, CI, serialization и производительность.

## Проверка

Команды, результаты, ссылки на evidence; отдельно VERIFIED и NOT VERIFIED / BLOCKED.
```
