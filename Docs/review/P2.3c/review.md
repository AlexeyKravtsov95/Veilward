# Независимое техническое review P2.3c

Reviewer: отдельный fresh-context senior Unity/C#/iOS agent (GPT-6 Astra); read-only review, без изменения files/index/HEAD и без запуска мутирующих Unity suites. PR #28, reviewed range `59f07e2..d96bd47`. Report получен 2026-11-17 (Asia/Novosibirsk).

## Вердикт исходного head: WITH FIXES

Critical: 0. Important: 1. Minor: 0.

**Important: transient save failure блокирует текущий production UI.** В `BattleCheckpointController.SaveForStartWave/SaveForNextWave` проверка `!LastSaveSucceeded` отклоняла повтор до попытки сохранения. После completion/pre-Next IO-сбоя игрок в WaveComplete не мог повторить Save через текущие кнопки даже после восстановления storage: экономические команды недоступны, phase observer больше не сохраняет ту же фазу, Retry choice UI относится к следующему этапу. Прямой вызов SaveCurrentState из tests не доказывал доступность восстановления для игрока.

Согласованное техническое исправление: eligibility query остаётся первым; убрать отказ **до новой попытки записи**, всегда требовать successful current safe Save перед Domain transition. Отдельный Retry через SaveCurrentState остаётся persistence-only, без повторного Choose/Next/Step. Reviewer подтвердил соответствие §8.2. Regression различает сохраняющийся IO failure (никакого перехода/bytes mutation) и восстановленный storage (safe save, затем один переход).

## Что reviewer проверил

Atomic validate-before-mutate Domain restore, deep DTO, exact progression history/credits/pending/army; независимые primary/backup pipelines, legacy type/economy/content preservation и read-only Load; Confirm save при неизменной фазе; rollback с positive kill/proc; исторические combat assertions и evidence. Независимо сверены SHA256 XML, 713/713 EditMode и 32/32 PlayMode исходного head, static38/38 и diffcheck PASS. Два новых валидных raw SingleWave schema1/2 cases рабочей копии прочитаны, замечаний нет.

## Закрытие замечания

Исправлено в этой ветке одним RED→GREEN fix pass: два transient-retry regression cases ожидаемо упали (715/717), после удаления early latch и проверки ongoing IO failure полный EditMode дал 717/717. PlayMode 32/32 повторён успешно. Фактические отчёты, source commit и итоговые totals — в [validation.md](validation.md) и [gates.json](gates.json). Повторное независимое review не подменяется self-approval: исходное замечание закрывается воспроизводимым regression и полным suite; пользовательское PR approval/merge остаются отдельными.

## Declined to judge и границы вывода

- Fix не входил в исходный reviewed head: автор выполнил RED→GREEN и свежие gates. Замечание закрыто regression; отдельное повторное независимое approval не заявляется.
- Choice UI/modal callbacks/production activation относятся к P2.3d. Это согласованная граница, не оправдание регрессии текущего legacy UI.
- Natural progression replay, баланс/проходимость и G2 относятся к P2.3e/плейтестам; прежние all-open traces не являются progression PASS.
- Physical iPhone/force quit/background/touch/safe area/performance, IL2CPP export и hosted Unity CI reviewer самостоятельно не проверял. Эти ограничения сохраняются.
- Unity suites reviewer не перезапускал по read-only условию; он сверил реальные отчёты. Все проверки fix выполняет автор с сохранением новых XML.
