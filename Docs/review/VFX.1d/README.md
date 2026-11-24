# VFX.1 / #93 — приёмка Battle в Editor

Печать показывает центр, границу и допустимость выбора. Отмена снимает разметку. Принятое применение раскрывает знак, а искры используют координаты фактических попаданий, включая lethal. Страж получает сигналы появления, подхода к Сердцу, смерти и пропуска. Штатные ART5 poses продолжаются на прежнем visual child.

[Спецификация и регрессии](../../VFX1D_RIFT_SEAL_BOSS.md) · [PNG и непрерывные видео](gallery.md) · [Проверки и Editor profile](validation.md) · [Независимое code/visual review](review.md).

## Матрица VFX.1

| Элемент | Проверка и границы |
|---|---|
| FLAME | Огненный снаряд, след и затухание. Реальный Update в штатном и плотном бою. [Стадии и регрессии #90](../VFX.1a/README.md) |
| TIDE | Летящая капля и рябь. Реальный Update в штатном и плотном бою. [Стадии #91](../VFX.1b/README.md) |
| GALE | Движущийся воздушный серп. Реальный Update в штатном и плотном бою. [Стадии #91](../VFX.1b/README.md) |
| STONE | Вращающийся плоский осколок и сколы. Реальный Update в all-open бою. [Стадии #91](../VFX.1b/README.md) |
| STEAM | Направленный паровой импульс и рассеивание. Реальный Update в обоих сценариях. [Стадии #92](../VFX.1c/gallery.md) |
| STORM | Короткий сегментированный разряд и искры. Реальный Update в обоих сценариях. [Стадии #92](../VFX.1c/gallery.md) |
| Burn, Wet, Gust, Crack | Все четыре одновременно в живом snapshot. Два overhead slots и четыре строки в инспекторе. [Инспектор](playmode/four-status-inspector.png) |
| Steam × Intrinsic / StatusPair | Обе причины в плотном Update, лимит включает primary. [События нового боя](reports/dense-all-open-frames.json.gz) · [Отдельные фазы #92](../VFX.1c/gallery.md) |
| Storm × Intrinsic / StatusPair | Обе причины в плотном Update, дополнительные удары отдельно с HitIndex, FromPosition и TargetPosition. [События нового боя](reports/dense-all-open-frames.json.gz) · [Отдельные фазы #92](../VFX.1c/gallery.md) |
| Rift Seal | Valid, invalid, Cancel и принятый cast. Hit spark привязан к captured EnemyId, позиции, tick, ability ID и sequence. [Кадры и PTS](gallery.md) |
| Страж Разрыва | Spawn, near-base, death и leak. Кадры death/leak содержат существующие ART5 tails. [Кадры и PTS](gallery.md) |

## Сценарии

- **Earned.** Боевой v2, wave 8 через файловый Continue из состояния, заработанного штатными командами FLAME-маршрута. Запись показывает два старта волны, Pause/Resume, повторный Continue и Victory8. Две полные FLAME/GALE трассы до и после совпадают побайтно.
- **Dense all-open.** Отдельный Editor stress fixture. Production Battle, исходные карта, камера, модели, анимации, каталог, босс, Печать и реальный Update. Сценарий даёт все стихии, 1200 стартового золота и более живучих Shade с плотным расписанием, чтобы провести бой всеми шестью семействами. Штатный v2 открывает только три стихии за забег.
- Физический iPhone перенесён в prerelease QA #53/#54/#55. Parent #47 принят на 3/4. Release, human ART/G2 и native performance имеют отдельные проверки.
