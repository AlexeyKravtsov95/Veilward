# R3 — решение по разрешённому набору

Разрешение владельца18.11.2026: только1000/10 control,900/10,800/10,1000/8,1000/5; одно поле относительно контроля. Повторное согласование не требуется. Source matrix1cb8702, дополнительные F/T/G layoutsfc485e2 и rebuild0344960. Это не final human/G2 balance.

| HP / armor | FLAME Stone | GALE Stone | F/T/G Steam/Storm pair/intrinsic | R3 eligibility |
|---|---|---|---|---|
|1000/10|Defeat, boss154|Defeat, boss68|4positive cells обоих starts; Defeat|нет|
|900/10|Defeat, boss54|Victory, kill relative782|4positive cells обоих starts; Defeat|нет|
|800/10|Victory, kill645, base10|Victory, kill720, base14|4positive cells обоих starts; Defeat|да|
|1000/8|Defeat, boss102|Defeat, boss8|4positive cells обоих starts; Defeat|нет|
|1000/5|Victory, kill705, base10|Victory, kill722, base14|4positive cells обоих starts; Defeat|**выбран**|

R3 §2 отдельно требует Victory/boss kill/positive ability обоих natural starts и четыре positive reaction cells на дополнительных законных F/T/G routes. Coverage Victory не названа обязательной: точная сверка независимым reviewer выявила и исправила избыточное условие первоначальной локальной specification. Обязательные исходные natural Victory assertions не менялись. Дополнительный поиск coverage Victory полезен, но не является новым acceptance blocker: оба ограниченных passes30000+30000 не нашли её, это честно опубликованный предел, не доказательство общей непроходимости. Полная двойная trace/file/menu matrix остаётся #40.

Внутри HP оси900 недостаточно для FLAME, поэтому800 — ближайший прошедший из разрешённых. Внутри armor оси8 недостаточно, поэтому5 — ближайший прошедший. Между осями выбран1000/5: сохраняет1000HP и более позднее FLAME-убийство705 против645 при800/10. Контрольный leak был749: остаётся только44fixed ticks (1.47s при30Hz) запаса. Для GALE убийства722/720 практически одинаковы; base HP10/14 совпадает. Ability117 по boss против112 у800/10; она не убивает босса сама. Опасность leak20 и все остальные правила сохраняются.

Armor5 также меньше подавляет быстрые F/T/G атаки. В дополнительном layout-поиске лучшие четырёхклеточные coverage attempts оставляют393/424HP из1000, против477/482 из800 при armor10; это60.7%/57.6% vs40.4%/39.75% HP loss. Это объясняет выбор оси наряду с kill timing, а не утверждает готовность всех сборок или математическую оптимальность. Прежние P2.3e.1 coverage routes и новые лучше расположенные routes имеют реальные положительные Steam/Storm × StatusPair/Intrinsic counters; их Defeat остаётся виден.

Применяется только `boss.armor:10→5` в новом EightWaveAbilityBoss wrapper с согласованной validation/builder default. TestWave/EightWave, башни/цены, экономика, waves1–7, ability120/1.5/600, old content/factories/saves и R1/R2 evidence не меняются. Synthetic1000/10 unit boundaries сохраняют свою явную definition.

Окончательный acceptance зависит от выбранного production replay, persistence/Retry/UI и полных зелёных gates; candidate research PASS их не заменяет.
