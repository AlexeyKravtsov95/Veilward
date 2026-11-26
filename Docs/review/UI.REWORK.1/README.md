# UI.REWORK.1 — «Чернила и фонари» в GameView

[Принятый эскиз](../../../ArtSource/References/UI_REWORK_1/approved-ui-concept.png) · [Спецификация](../../UI_REWORK_1_DESIGN.md) · [Мастера и лицензия](../../../ArtSource/UI/InkAndLanterns/README.md) · [Проверки и воспроизведение](validation.md).

## Игровой маршрут

| Сцена | Кадры и видео |
| --- | --- |
| MainMenu | [Нет сохранения](story/menu-new.png), [повреждённый файл и Retry](story/menu-load-error.png), [Continue](story/menu-continue.png), [завершённый забег](earned/menu-finished.png) |
| Резонанс | [Первый выбор](story/first-element.png), [предпросмотр стихии](story/element-preview.png), [после волны 3](milestones/after-wave-3.png), [после волны 6](milestones/after-wave-6.png) |
| Подготовка | [HUD](story/preparation.png), [закрытая школа](story/build-locked.png), [предпросмотр покупки](story/build-preview.png) |
| Башня | [Не хватает золота для улучшения](story/tower-low-gold.png), [улучшение на тестовом бюджете](stress/upgrade.png), [подтверждение продажи](story/sell-confirmation.png) |
| Пауза и запись | [Пауза](story/pause.png), [ошибка записи и Retry](story/save-error.png) |
| Волна 8 из earned v2 | [Подготовка](earned/earned-preparation.png), [Печать](earned/ability-preview.png), [полоса Стража](earned/boss-combat.png), [инспектор](earned/enemy-inspector.png), [Victory](earned/victory.png) |
| Поражение и плотная волна | [Defeat с ошибкой записи](defeat/defeat-save-error.png), [Defeat после Retry](defeat/defeat.png), [плотная тестовая волна](stress/dense-wave.png) |

[Видео MainMenu → карточки → Pause → Continue, 18,3 с](story/gameview.mp4) · [Видео earned v2 → Battle → Victory, 31,3 с](earned/gameview.mp4). События и индекс видеокадров сохранены в [story](story/events.tsv) и [earned](earned/events.tsv).

## Соотношения сторон

| Профиль GameView | MainMenu | HUD | Стройка | Pause |
| --- | --- | --- | --- | --- |
| 1136×640, DPR2 | [PNG](profiles/narrow-menu.png) | [PNG](profiles/narrow-hud.png) | [PNG](profiles/narrow-build.png) | [PNG](profiles/narrow-pause.png) |
| 2556×1179, DPR3, отступы 132 слева и справа, 63 снизу | [PNG](profiles/modern-menu.png) | [PNG](profiles/modern-hud.png) | [PNG](profiles/modern-build.png) | [PNG](profiles/modern-pause.png) |
| 2048×1536, DPR2, отступ 40 снизу | [PNG](profiles/ipad-menu.png) | [PNG](profiles/ipad-hud.png) | [PNG](profiles/ipad-build.png) | [PNG](profiles/ipad-pause.png) |

Кадры сняты в Play Mode, на полном GameView. Внутриигровые подписи статусов и текущая разметка клеток видны на фоне. Пакеты [#104](https://github.com/AlexeyKravtsov95/Veilward/issues/104) и [#105](https://github.com/AlexeyKravtsov95/Veilward/issues/105) продолжают эту работу.
