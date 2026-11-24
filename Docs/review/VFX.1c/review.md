# Независимая проверка PR #96

Отдельная сессия Codex CLI проверила точный HEAD в режиме read-only в существующем checkout. Рецензент получил оригинальные PNG и кадры, декодированные из опубликованного MP4.

## VERDICT APPROVED

Независимое Code + Visual ревью PR #96 для #92. Проверил HEAD `c9a0463bf4ee4a831aab0631c0a10ac2c5ad8f00` относительно базы `4dac946a142e281adc739aeb05ffb9aba136f402`. **P1 — 0, P2 — 0.**

### VERIFIED

- **Direct VFX.** STEAM показывает паровой импульс и расходящиеся завитки, STORM — движущийся сегментированный разряд и искры. Оба запускаются от `AttackEvent`, используют уровень башни и её `ShotSource` с ограниченным fallback. Положение цели берётся из события и сохраняется после lethal. Регрессии покрывают L1–L3, cooldown, отсутствие соседей, Sell и Upgrade. [Подключение к Battle](../../../Assets/_Game/Scripts/Presentation/BattleSceneController.cs#L480) · [Геометрия атак](../../../Assets/_Game/Scripts/Presentation/HybridProjectilePool.cs#L120)
- **Реакции.** Сопоставление использует `Origin`, `Tick`, `ReactionSequenceInTick`, `Kind` и `SourceTowerId`. Steam включает primary с `HitIndex=0` в лимит четырёх целей. Storm рисует до двух дополнительных попаданий в порядке `HitIndex`, каждое по своим `FromPosition` и `TargetPosition`. Вспышки целей требуют положительного `AppliedHpLoss`. Сверил четыре комбинации реакции и причины с сырыми событиями, включая обе точки каждой дуги Storm. [Фильтрация попаданий](../../../Assets/_Game/Scripts/Presentation/ReactionEffectView.cs#L39) · [Галерея реакций](gallery.md#L14)
- **Editor-визуал.** На монтажах и исходных PNG различимы запуск, полёт и попадание обоих гибридов. Сверил SHA-256 всех 509 исходных кадров, декодировал выбранные кадры MP4 и сопоставил их с PNG. Для каждого L1 три фазы принадлежат одному событию. Максимальное расхождение PTS с временем захвата — **0,723 мс**. Запись production `Update` показывает Pause с замороженной геометрией и файловый Continue с восстановлением состояния и очисткой эффектов. L3 сняты на earned test clock, плотный бой и инспектор — на synthetic test clock. [Галерея](gallery.md) · [Кадры и PTS](reports/video-proof.json)
- **Регрессии и границы diff.** Сохранённые полные gates автора на `c4d59c3` подтверждают Static **38/38**, EditMode **1297/1297**, PlayMode **99/99**, compile без ошибок, ноль failed, skipped и inconclusive. Файлы runtime и тестов побайтно совпадают с HEAD. Свежие трассы before/after совпали целиком для FLAME (**2 371 375 байт**) и GALE (**2 078 626 байт**). В обеих трассах подтвердил Victory8, убийство босса, убийства способностью и четыре положительные ячейки REG214. В diff сохранены Domain, правила, форматы сейвов, конфиги, art, базовые атаки и статусы, ability, HUD и камера. Новые эффекты используют существующие часы и reset. [Отчёт gates](validation.md#L5) · [Сводка трасс](reports/replay-equality.json)

### NOT VERIFIED / DEFERRED

В этом ревью запустил только read-only проверки. `verify_delivery.py`, вариант с `--check-local-preservation`, `verify_controls.py` с **28 отрицательными контролями** и `git diff --check` прошли. Unity повторно не запускал. Editor-вывод относится к Unity 6000.3.28f1, Metal и профилю Mobile с четырьмя зафиксированными локальными входами рендера. Физический iPhone, native input, IL2CPP и производительность отложены до #53/#54/#55. Финальная приёмка плотного боя, ability и boss cues остаётся в #93.
