# Независимое review P2.3d

Read-only senior review выполнено отдельным агентом GPT-6 Astra, `p23d_review`, по Task 3/spec и diff относительно merged #28. Reviewer не запускал Unity и не подтверждал ещё готовившиеся screenshots/gates. Итог: Critical/Important не обнаружены, два Minor. Это техническое review, не продуктовое/device approval и не разрешение self-merge.

1. Retry Save был доступен в Combat и мог создавать ложную ошибку ExportCheckpoint. Исправлено: общий pause/modal guard + запрет Combat до I/O; кнопка видна при failed save. `RegBattle184_RetrySaveInCombat_IsRejectedWithoutChangingDurableStatus` проверяет прямой вызов/реальный callback; PlayMode проверяет неизменность bytes/status во время Combat.
2. Start на pending3/6 возвращал pending вместо phase-first InvalidPhase. Исправлено: специальная pending-причина только в Preparation. Оба milestone cases проверяют InvalidPhase перед Confirm.

До исправления focused suite **8/11**, три ожидаемых failure: [review-red.json](review-red.json). После — **17/17**: [review-green.json](review-green.json), включая семь malformed-config cases. Полные suites и source commit приведены в [validation.md](validation.md). Fix pass проверен основным исполнителем; повторного независимого review после исправлений не заявляем.

Reviewer подтвердил чтением кода: привязку preview к progression instance/pending ID, explicit save после successful Choose даже без смены фазы, eligibility до I/O, read-only locked catalog и явные historical fixtures. По замечанию reviewer удалены случайные Unity serialization diffs URP/materials/prefab. Открытых известных findings не осталось; PR передаётся владельцу на окончательное review/approval, self-merge/release не выполняются.
