# Матрица P2.4e

Весь acceptance **BLOCKED**: обязательные natural Victory FLAME/GALE не достигнуты; соответствующие assertions остаются FAIL. Таблица отделяет проверенное поведение от полного принятия #39.

| REG / контракт | Проверка | Статус |
|---|---|---|
| 209 | BossWaveTests: exact11Shade0…300+boss330, stable group/order/IDs; legacy adapter; count bounds, null, missing/unknown serialized IDs, max EnemyId и absolute last-spawn overflow до phase mutation; пустое поле до future spawn | Assertions VERIFIED |
| 210 | BossWaveTests: lethal Direct/Burn/Reaction/Ability, reward50/kill1/bonus20 один раз; ability110/Crack114 без Gust/status/proc; Wet движение, Gust+Crack direct36; Steam/Storm primary/secondary с positive damage и reservation | Synthetic boundaries VERIFIED; не natural boss kill |
| 211 | BossWaveTests: synthetic HP1/13/20→0/leak20, Defeat priority, прочие kills того же Step сохраняют reward; Burn/cast before Movement предотвращают leak, cast после Defeat rejected; boss dead/other enemy/future schedule остаётся Combat | Assertions VERIFIED |
| 212 / 198 | BossPersistenceTests: native initial pending/confirmed; reached WaveComplete1…7, pending/confirmed3/6, Preparation2…8, terminal Defeat, deep exact wire restore | VERIFIED; final-content natural Victory snapshot BLOCKED |
| 212 / 199–200 | Все malformed ability cases повторены на final-ID: missing/null/duplicate/unknown/types/fraction/overflow/ranges; bad rules/disabled; final schemas1…3 reject. Old content сохраняет ID/disabled/12Shade8, single отдельно | VERIFIED |
| 212 / 201 | new/old-native/all-open primary↔backup; каждый normalize→своя factory→whole validation. no-write/idempotence; corrupt primary fallback; invalid primary не вытесняет backup; Save old→new-primary сохраняет валидный предыдущий primary | VERIFIED |
| 212 / 202 | Final save temp-write/readback/commit faults, live confirmed выбор сохраняется, Retry только пишет; reset с ошибкой BackupPath восстанавливает primary | VERIFIED; final natural boss-kill completion failure BLOCKED |
| 212 / 203 | Natural Preparation8; dirty Combat8 prefix содержит cast+positive ability HP loss+kill+positive reaction+живого boss; два prefix одинаковы, rejected restore атомарен; dirty/fresh whole rollback точен, events/enemies очищены | VERIFIED |
| 213 UI | saved builder/refs/GUID/meta/reproducibility, notice/HP/armor/reward/leak, действительная геометрия HP bar; обычные два slots/inspector, Pause panel hidden; production real menu Continue8 и safe-area injected44px layout | Фактический gate/кадры/Console в validation; Editor test clock |
| 213 natural | production150Gold/20HP/public commands/Step, receipts/decisions/cast boundaries/per-wave stats; FLAME и GALE до boss8 с positive ability damage; no-cast8 сознательный boss leak из живой Preparation8 | Defeat outcomes VERIFIED; обе требуемые Victory/boss kill BLOCKED |
| Кадры boss kill/Victory | Natural boss kill/Victory не произошли | BLOCKED; synthetic unit Victory не подменяет эти кадры |
| #40 | Full double tick/event traces и вся integration matrix | Отдельный scope, здесь не заявлены |
| Device/G2/ART | iPhone/IL2CPP/Metal/touch/реальные safe area/background/force quit/performance/human balance/final art/release | NOT VERIFIED / BLOCKED отдельными gates |

Исторические TestWave/EightWave, tower prices, waves1…7, packages/version и прежние evidence/goldens сохраняются. Два preactivation negative cases обновлены при разрешении final-ID schema4; старые schema1…3 отказы сохранены. Технические menu tests используют явный historical source перед Start, не переписывают outcomes. Ни один natural FAIL не удалён, не skipped и не заменён проверкой лишь детерминизма.
