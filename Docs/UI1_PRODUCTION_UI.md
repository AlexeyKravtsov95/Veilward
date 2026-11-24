# UI.1 — интерфейс миссии (#48)

Стилистика — нефритовые печати на индиговой поверхности, тонкие золотые акценты, тёплый камень для текста и коралл для опасности. Battle остаётся в существующей камере. Меню, HUD и карточки используют один набор визуальных правил.

| Токен | Значение |
|---|---|
| Фон / поверхность / поднятая поверхность | `#101421` / `#1B2233` / `#273246` |
| Основное действие / владение выбором | `#276853` / контур `#E7C279` и подпись Selected |
| Опасность / ограничение | `#F49783` / приглушённая поверхность с текстовой причиной |
| Тёплый камень / вторичный текст | `#F5E9D1` / `#CBBFAF` |
| Отступы / цели | 4, 8, 12, 16, 24 units; минимум 60×60 для основных действий |
| Иерархия | 12 статус карточки, 13–16 подпись и основной текст, 20 метрика, 30 модальный заголовок, 59 логотип |

## Композиция

```text
широкий телефон                4:3 планшет / низкий телефон
┌───────── safe ──────────────┐  ┌─────── safe ────────┐
│ Heart Gold Wave   Start  II │  │ Heart Gold Wave  ▶ II│
│        Warden (final)       │  │  Warden (final)     │
│          battlefield        │  │     battlefield    │
│ Seal      [context if open] │  │ Seal       [context]│
└─────────────────────────────┘  └─────────────────────┘
```

- Main Menu: слева эмблема, справа название, New Game и Continue, ниже состояние файла и Retry при ошибке чтения.
- Боковой каталог из шести карточек показывает цену и доступность. Каталог открывается с противоположной от выбранного тайла стороны и оставляет превью на поле; tower даёт stats, цену Upgrade и Sell refund; inspector показывает HP и броню, эффекты и фактический HP loss по причинам. Пустой контекст скрыт. Rift Seal на нижнем левом краю, targeting с отдельными Confirm/Cancel и причиной отказа.
- Element Choice, Sell, Pause и Victory/Defeat ставят карточку поверх затемнения. Полноэкранный blocker лежит вне safe area. В Pause доступны Resume и Main Menu, в результате — итог и Main Menu. Доступность Continue и rollback диктует текущий checkpoint.
- Diagnostics остаётся доступна в Editor/development (`F3`). Обычный интерфейс на EN. Редактируемый код эмблемы и пиктограмм входит в исходники. Текст использует встроенный шрифт Unity.

## Landscape и регрессии

CanvasScaler Expand 740×400 сохраняет пропорции. Контрольные профили: 1136×640 → 568×320 pt, 2556×1179 → 852×393 pt, 2048×1536 → 1024×768 pt. Для цели размера 60 units расчёт `60 × min(widthPx / 740, heightPx / 400) / DPR` даёт соответственно 46.05, 58.95 и 83.03 pt. Insets задаются в пикселях в Editor и преобразуются в нормализованные anchors. Реальная iOS safe area и lifecycle входят в #53/#54/#55.

| Сценарий | Проверка |
|---|---|
| UI1-01 Menu и файлы | Нет файла, corrupt/incompatible, backup, восстановленный Continue, терминальный файл, повторный клик и reload. Проверять player copy и действительный доступ. |
| UI1-02 Экономика и выбор | Preview/cancel/confirm, locked и low gold, max level, refund, repeat и модальное владение без mutation. Нативный выбор старт/3/6, без QA unlock. |
| UI1-03 Бой и состояния | Rift Seal ready/cooldown/invalid/confirm/cancel, pause/resume, inspector и точный AppliedHpLoss, босс, Victory/Defeat, failed save и Retry, текущие rollback и reward gates. |
| UI1-04 Aдаптация и ввод | Реальные сцены, три viewport, safe-area bounds, размеры и зазоры, input press/drag/release над UI и свободным полем, overlay blocker, повторный enable и reload. |
| UI1-05 Доказательства | Полные suites, builder repeatability и refs, PNG и непрерывное Editor видео, provenance/inputs read-only audit, независимое code/visual review. |

Затронуты Presentation, uGUI builder, MainMenu/Battle и их tests. Риски — порча ссылок при rebuild, двойной ввод при закрытии модального окна и перекрытие поля карточками. Проверяются полным builder gate, PlayMode тестами и просмотром реального кадра.
