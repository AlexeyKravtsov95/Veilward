# P2.4c — schema/content/phase/IO matrix

Production New Game прежний, ability-disabled. Enabled persistence только internal exact-ID `synthetic-p24-ability-v1`; не production mission и не boss acceptance.

| Вход | Результат / проверки |
|---|---|
| schema1/2 eight-wave-v1, legacy-single-wave-v1 | Source shape/scalars/army validation; schema1 test-tower, schema2 typed army; schema4/тот же ID/all-open/disabled. Single-wave отдельно. |
| schema3 прежние legacy IDs | Точная legacy progression; schema4/тот же ID/disabled. |
| schema3 eight-wave-unlocks-v1 | Native pending/history/army сохранены, schema4/тот же ID/disabled/прежняя8. |
| schema4 три production IDs | Обязательные exact disabled rules/none/0/1; прежний native/all-open mode. |
| schema4 synthetic-p24-ability-v1 | Только exact validator fixture: native progression, rift-seal-v1/rift-seal, remaining0…600, sequence1…MaxValue, remaining>0→sequence>=2. |
| schema1/2 native, schema1/2/3 enabled synthetic | Отказ. |
| Любая schema final eight-wave-ability-boss-v1 или unknown ID | Отказ. Final factory/перестановки нового final native — #39. |
| Missing/null/duplicate/unknown fields, bool/fraction/exponent/overflow | До JsonUtility; отсутствующий0 не становится default. Повреждённая непустая source progression не стирается миграцией. |
| Initial pending | Строго ability0/1 и все прежние начальные ограничения. |
| Pending/confirmed start/3/6, Prep4/7/8, Victory | Deep round-trip enabled/disabled; перенос army/IDs/progression. |
| Defeat | Round-trip при HP0; terminal валиден для валидации, Continue выключен. |
| Primary native / backup all-open и обратный порядок | Каждый slot независимо normalize→factory→Domain validate. Валидный backup остаётся доступен; source bytes неизменны. |
| Synthetic corrupt primary / valid backup | Exact validator path; два Load не пишут bytes. Save не заменяет backup повреждённым primary. |
| Save при previous valid primary другого production mode | Полный предыдущий валидный primary попадает в backup; оба порядка. |
| Write / read-back / commit failure | Primary/backup bytes неизменны, live ability/economy success сохранён; Retry пишет whole snapshot. |
| Completion от Step или Cast без следующего Step | Whole safe save. LateUpdate замечает command completion, даже если Combat не наблюдался промежуточным кадром. |
| Failed completion / Choose / второй Next save | Live result сохранён; два Retry не повторяют команду/reward/cast. |
| Start / Next | Read-only eligibility до IO, durable текущий safe checkpoint до перехода. Существующие controller/IO regressions сохранены. |
| Combat Export / pause/background | Combat export rejected; нет отдельного ability save. Прежний durable Preparation остаётся recovery point. |
| Dirty rejected restore | State/Grid/progression/ability/IDs/counters и четыре непустых buffers неизменны. |
| Successful dirty restore | Весь snapshot заменён; enemies/statuses/cursors/event streams/sequence и существующие Presentation transients очищены. |
| Cast+kill+positive proc rollback | Pre-Start remaining421/sequence3, экономика/IDs/army/progression восстановлены; dirty и fresh попытки совпали с control; full continued trace до Victory совпал. Synthetic150/20 balance proof не заявляется (fixture10000 gold/1000HP enemies). |
| Recovery/reset | Прежние physical filesystem reset/recovery/backup-directory failure tests в полных suites; filenames прежние. |

Точные новые cases и результаты извлекаются из final raw XML в `gates.json`. Старые tests менялись только в schema4/wire-проекции и для замены временного unsupported-контракта #36. Их outcome assertions не ослаблены. `audit.py` сохраняет все historical evidence/goldens и остальные tests побайтово.
