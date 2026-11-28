# Доводка UI.REWORK.1 по Review 26.11.2026

[Раздел Review в #103](https://github.com/AlexeyKravtsov95/Veilward/issues/103#review-26112026--pr106-head-0cd138b) · [Принятый четырёхэкранный эскиз](../../../../ArtSource/References/UI_REWORK_1/approved-ui-concept.png) · [Draft PR #106](https://github.com/AlexeyKravtsov95/Veilward/pull/106)

## Сравнение полных экранов

| Экран | До доводки, `0cd138b` | После доводки |
| --- | --- | --- |
| MainMenu | [Без сохранения](../story/menu-new.png) | [Без сохранения](story/menu-new.png), [ошибка чтения и Retry](story/menu-load-error.png) |
| Волна 8 | [Страж в бою](../earned/boss-combat.png) | [Страж в бою](earned/boss-combat.png), [Печать готова](earned/ability-ready.png), [перезарядка](earned/ability-cooldown.png), [прицеливание](earned/ability-preview.png) |
| Подготовка | [Обычная клетка](../story/build-preview.png), [центральная клетка на narrow](../review/narrow-build-center.png) | [Обычная клетка](story/build-preview.png), [закрытая школа](story/build-locked.png), [не хватает золота](profiles/narrow-build-center.png), [ошибка записи](review/modern-build-save-warning.png) |
| Pause | [Пауза](../story/pause.png) | [Пауза](story/pause.png) |
| Карточки и итог | [Инспектор с четырьмя статусами и двумя реакциями](../review/inspector-full.png), [Victory](../earned/victory.png) | [Полный инспектор](review/inspector-full.png), [инспектор Стража](earned/enemy-inspector.png), [не хватает золота для улучшения](story/tower-low-gold.png), [ошибка записи](story/save-error.png), [Victory](earned/victory.png) |

- На полном MainMenu сохранена композиция эскиза с эмблемой и действиями слева и воротами справа. В Pause матовая бумага отделяет два действия от затемнённого поля. Тонкий контур и нерегулярные края видны на полном экране, прежние растянутые полосы фактуры убраны из общего атласа.
- В бою полоса Стража занимает 246×52 вместо 301×79 единиц Canvas. HP, броня и урон Сердцу видны рядом с портретом. Медальон шириной 56 единиц и подпись состояния используют зону тапа 104×84. На первом просмотре надпись выходила за бумагу. Повторная запись подтверждает, что обе строки помещаются целиком.
- В подготовке шесть школ и действия собраны в ленту высотой 84 единицы. Видны выделение, отдельные замки и выбранная постройка на карте. При угрозе перекрытия клетки лента целиком переходит под верхний HUD, как на профиле modern. Цена, урон, дальность и темп атаки видны и у заблокированной школы, рядом указана причина. Принятый эскиз использован для композиции UI. Карта и камера используют текущую сцену Battle. Маркеры статусов и строительных клеток входят в отдельные [#104](https://github.com/AlexeyKravtsov95/Veilward/issues/104) и [#105](https://github.com/AlexeyKravtsov95/Veilward/issues/105).

## Профили GameView

| Размер и тестовая safe area | MainMenu | Обычная клетка | Центральная клетка | Pause |
| --- | --- | --- | --- | --- |
| 1136×640, DPR 2 | [Кадр](profiles/narrow-menu.png) | [Кадр](profiles/narrow-build.png) | [Кадр](profiles/narrow-build-center.png) | [Кадр](profiles/narrow-pause.png) |
| 2556×1179, DPR 3, 132 px по бокам и 63 px снизу | [Кадр](profiles/modern-menu.png) | [Кадр](profiles/modern-build.png) | [Кадр](profiles/modern-build-center.png) | [Кадр](profiles/modern-pause.png) |
| 2048×1536, DPR 2, 40 px снизу | [Кадр](profiles/ipad-menu.png) | [Кадр](profiles/ipad-build.png) | [Кадр](profiles/ipad-build-center.png) | [Кадр](profiles/ipad-pause.png) |

[Видео MainMenu → постройка → Pause → Continue](story/gameview.mp4) · [Видео earned v2 → бой → Victory](earned/gameview.mp4)

[События основного маршрута](story/events.tsv) · [События боя](earned/events.tsv) · [События профилей](profiles/events.tsv) · [Проверка Retry и полного инспектора](review/events.tsv)

## VERIFIED

Проверенная реализация — `f80bd40fa465381bee47c5a8e626093018b889d8`. Unity 6000.3.28f1, macOS arm64, Metal, активная платформа редактора iOS. [SHA-256 входов](reports/inputs.sha256)

- Полные прогоны дали Static 38/38, EditMode 1319/1319 и PlayMode 110/110. Failed, skipped и inconclusive в обеих Unity suites равны 0. Компиляция прошла с 0 ошибками и прежним предупреждением в BossGreedySearchTests. `git diff --check` прошёл. [Static](reports/static.txt) · [EditMode](reports/EditMode.json) · [PlayMode](reports/PlayMode.json) · [Компиляция](reports/compile.json)
- Четыре новых ограничения размеров HUD и ленты ловят старые габариты. Целевая проверка каталога сравнивает точные урон, дальность и темп атаки, а также проверяет единичную покупку. [Габариты до правок](reports/compact-hud-red.json) · [Проверки каталога](reports/catalog-regression.json)
- В атласе 24 спрайта, из них восемь 9-slice поверхностей. PNG атласа занимает 162 884 байта. Временная регенерация дала те же байты атласа, иллюстрации меню и слайсов. После прогрева 1000 переключений glyph с перестройкой mesh дали 0 managed bytes на измеренном потоке редактора. [Импорт](reports/imports.json) · [Регенерация](reports/regeneration.txt)

- На узком профиле минимальная цель 60×60 единиц Canvas соответствует 46,05×46,05 logical pt. Проверены обе позиции ленты, safe area и непересечение активных контролов. Через EventSystem нажаты три школы при ошибке записи, затем Cancel и Retry. Недоступные Build и Retry соблюдают исходные ограничения.
- Из earned v2 выполнены два применения Печати через Preview и Confirm. Победа на восьмой волне с 16 HP Сердца и 64 побеждёнными врагами подтверждена кадром и журналом. В основном маршруте Continue восстановил 129 gold после постройки и продажи.
- В сериализации совпали 383 записи Battle и 14 MainMenu. Правки общих объектов затронули ссылки на UI. Локальные ссылки Canvas разрешаются. [Сравнение сцен](reports/scene-scope.txt)
- 27 PNG сняты целиком в Play Mode через `ScreenCapture.CaptureScreenshotAsTexture`. Оба видео H.264 1600×900 полностью декодируются и совпадают с журналами кадров. В основном маршруте 422 кадра и 21,10 с, в бою 646 кадров и 32,30 с. При задержках захвата текущий кадр повторяется для вывода 20 кадров/с. [Размеры и SHA-256 кадров, параметры видео](reports/media.json)

### Воспроизведение

Использованы команды из [исходного протокола проверок](../validation.md#воспроизведение). Драйвер запускается по очереди с режимами `polish-story`, `polish-earned`, `polish-profiles` и `polish-review` в активном Play Mode. Контрольные слоты создаются под `TestResults/UIREWORK1/capture/`. Проверка опубликованных кадров и видео выполняется командой `python3.12 Docs/review/UI.REWORK.1/tools/verify_media.py --polish`.

Перед полным EditMode открывается `Assets/_Game/Scenes/Bootstrap.unity` через `unity command scene open`. Тогда восстановление сцен после прогона совпадает с последующим восстановлением файлов обёрткой.

Кадры отражают открытый checkout с исходными локальными правками импорта URP и цветов трёх Graybox-материалов. Их diff сохранён побайтово и остался в локальном checkout.

## NOT VERIFIED / BLOCKED

Физические iPhone/iPad, touch latency и native-производительность остаются в Device QA #53/#54/#55. Приёмка оформления владельцем проекта и решение о слиянии остаются открытыми.
