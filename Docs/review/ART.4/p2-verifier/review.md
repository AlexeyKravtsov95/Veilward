# P2 — повторное review delivery verifier / PR88

Прежний вывод whole-delivery PASS на6f23d9a был ограничен личным checkout и не выявил обязательной зависимости от трёх нерендеринговых private files. Finding владельца в#45 воспроизведён на старом полном entrypoint для каждого из3missing paths, без перемещения/изменения файлов: [receipt](reproduced-old-dependency.json).

Узкая исправленная ревизия отделяет обязательный delivery от optional local preservation. Source/карта/scene/models/gameplay/lighting/Unity/media reports не меняются. Historical carry-in7 и raw receipts сохраняются; личные contents не публикуются.

Независимое re-review будет выполнено после committed manifest и текущих Static/controls/full read-only audit. До его результата P2 не объявляется закрытым. Unity1233/88 ниже по дереву — прежние runs, не новые проверки этой ревизии. Self-merge/release не выполняются.
