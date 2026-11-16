# P2.2d / Task 3 — integration evidence

База: свежий `origin/main` `d68f770c2e71a76ae6a5e53ab4d1469d4bf6d893` (merged PR #20). Изолированная ветка `codex/p2-2d-hybrid-catalog`. Проверенные runtime/config/test исходники: **`60d20393b226a00d6cfa04d70fa14a73e601e4a9`**; последующие commits содержат только docs/evidence. Правила отчёта: `p2.2-steam-storm-v1`. Точные hashes и totals — [quality-gates.json](quality-gates.json).

## VERIFIED

- Публичная production factory включает validated Steam/Storm; basic status-pair integration и intrinsic обоих типов используют существующий resolver. Нет изменения Domain formulas/runtime.
- Shared TestWave: шесть видимых типов в порядке FLAME/TIDE/GALE/STONE/STEAM/STORM по утверждённому P-05; legacy test-tower скрыта и восстанавливается. Все новые уровни/intervals/payloads точно по spec §6, base/legacy данные прежние.
- Upgrade сохраняет IDs/cell/NextAttackTick и enemy timestamps; investments 110/275/495, refunds 77/192/346. Preparation/Pause/повторы/atomic rejects покрыты регрессиями.
- Schema 2 каждого гибрида/уровня и смешанной армии, прежние schema 2/v1 migration, unknown primary → valid backup, rejected restore без mutation live State/Grid/events, Load без записи bytes. Store/content IDs/physical slots не менялись; transient бой не сохраняется.
- Builder расширяет только полный валидный legacy набор; malformed/partial каталоги и локальные копии wrapper отвергаются; корректные configs сохраняют bytes при двух Build. Saved assets обновлены Unity API. Новых packages/Unity/meta/GUID изменений нет.
- Historical `BattleElementalReplayTests` по-прежнему использует explicit internal Empty, команды/числа/старый P2.1e JSON не изменялись.
- Реальный Editor Play Mode flow: [qa.log](qa.log), точный [script](visual-qa.py), десять PNG ниже. Все кадры просмотрены. Бой двигался по реальным frames: sample A tick 121/frame 44692 → B tick 162/frame 46941. На живом enemy 3 были LastSteamTick=67/LastStormTick=73; telemetry уже очищена обычным adapter lifecycle.
- Console audit после очистки перед flow: **0 errors / 0 warnings**, compilationFailed=false. Внутреннее read-only review нашло один P2, исправленный RED→GREEN: [ответ](review-response.md).

## Реальный визуальный flow и ограничения fixture

Unity **6000.3.28f1 (8f082d2fe476)**, macOS ARM64, OSXEditor, active build target **iOS**, проектный backend **IL2CPP/ARM64**; это не player/device build. Unity CLI **1.0.0**, Pipeline **0.10.0-exp.1** (project package не менялся). Приложение runInBackground и Editor auto tick включались для QA; runInBackground возвращён в исходное false. Time.timeScale не менялся; simulation Step вручную не вызывался.

Для UI/economy QA загружен **временный валидный schema 2 Preparation fixture с 1000 Gold**, не изменяющий saved assets. Исходная production factory вернула **150 Gold** — это записано отдельно. Этот fixture позволяет показать оба гибрида/Upgrade в одном коротком flow и **не является evidence доступности стратегии при обычном бюджете или balance approval**. Default-budget construction/insufficient/Pause проверены отдельными regression tests.

Действия после Load — только Presentation API/Button.onClick: построены Steam/Storm L1 (1000→780), оба Upgrade L2 (780→450), реальная волна (5 kills, bonus 20, tick 253, HP 20, Gold 495). MainMenu Continue восстановил оба ID, L2, investment 275, NextAttackTick 232/271 и Gold 495 без повторных rewards. Next Wave → Preparation; Sell Steam вернул 192 (Gold 687), повторный Sell rejected.

| Кадр | Фактическое состояние |
|---|---|
| [01-catalog.png](01-catalog.png) | Preparation, шесть видимых типов, покупок нет |
| [02-steam-preview.png](02-steam-preview.png) | Steam L1: price 110, damage 18/range 3/rate 0.8 |
| [03-storm-preview.png](03-storm-preview.png) | Storm L1: price 110, damage 9/range 3.5/rate 1.5 |
| [04-steam-upgraded.png](04-steam-upgraded.png) | Steam L2, investment 275, next price 220, refund 192 |
| [05-storm-upgraded.png](05-storm-upgraded.png) | Storm L2, investment 275, next price 220, refund 192 |
| [06-hybrid-combat.png](06-hybrid-combat.png) | Живой бой двух гибридов, generic presentation |
| [07-continue-menu.png](07-continue-menu.png) | MainMenu, валидный checkpoint/Continue |
| [08-continued-hybrids.png](08-continued-hybrids.png) | WaveComplete после Continue, оба L2, Gold 495 |
| [09-sell-confirmation.png](09-sell-confirmation.png) | Preparation второй волны, Steam refund 192 |
| [10-after-sell.png](10-after-sell.png) | Steam удалён, Storm/его ID сохранены, Gold 687 |

## Команды и gates

Из изолированной worktree на указанном source SHA:

```bash
./Scripts/run-static-checks.sh
./Scripts/run-edit-mode-tests.sh
./Scripts/run-play-mode-tests.sh
git diff --check
python3 Docs/review/P2.2d/visual-qa.py
```

Первичная генерация saved assets выполнена тем же Unity через:

```bash
/Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -quit -projectPath "$PWD" -buildTarget iOS -executeMethod Veilward.Editor.BattleSceneBuilder.Build -logFile /tmp/veilward-p22d-build2.log
```

Полные suites запускались стандартными wrappers при закрытом Editor: backend auto → установленный Unity batch `-batchmode -nographics -buildTarget iOS -runTests -testPlatform EditMode|PlayMode -testResults TestResults/<Mode>.xml -logFile TestResults/<Mode>.log`, без `-quit` для tests. Отчёты проверены `Scripts/verify_unity_test_results.py`; XML сохранены в gzip artifacts рядом, SHA256 считается от исходного XML. Пропущенные/пустые suites не считались PASS. Побочные пересериализации материалов/scene/prefab от builder tests убраны из PR; intentional changes — только TestWave/EightWave через API.

RED до реализации: baseline **430/430**; расширенный RED **488 total / 435 passed / 53 failed**, PlayMode **16 total / 15 passed / 1 failed**. Причины — отсутствующие hybrid IDs/serialized reactions и четыре visible entries. V1 compatibility уже проходила на baseline, ей не приписывается искусственный FAIL. Финальные gates: **static 38/38 + project checks, EditMode 503/503, PlayMode 16/16, git diff --check PASS**; failed/inconclusive/skipped = 0. Проверка diff сначала обнаружила только побочные trailing whitespace в пересериализованных builder outputs; после их удаления из worktree итоговый diff чист. Подробности — quality-gates.json.

## NOT VERIFIED / BLOCKED

Физический iPhone, built-player touch/safe area/background/force quit, iOS export/signing/release, Simulator, Unity CI, performance/allocations/thermal, external playtests и balance approval не проверялись. G2 открыт. Причинная визуализация, VFX, маркеры статусов и inspector — P2.2e. Сейчас generic UI показывает `Status: None` и прежнюю fallback role `Legacy defense` у гибридов; это известное ограничение текущего этапа, а не доказательство отсутствия runtime реакции. Полный impact/replay P2.2f не выполнялся и historical evidence не переписывалось. Self-merge/release не выполняются.
