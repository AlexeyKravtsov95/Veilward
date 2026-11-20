# Физический iPhone — build76, PARTIAL

20.11.2026. iPhone16Pro/iOS27.0.1, Metal, native2622×1206, Unity6000.3.28f1, production export profile, content eight-wave-ability-boss-v2/schema4. [Receipt](device/device-receipt.json) и [Unity build receipt](device/build-receipt.json). Xcode Debug с существующим локальным development signing; Unity Development flag выключен. Release/TestFlight не выполнялись.

Начальный inventory с tunnel unavailable в reports/device-availability.json — историческое наблюдение до подключения пользователем. После подключения DDI стал доступен; первый launch отклонён Locked. После разблокировки пользователем новая сборка76 установлена и запущена. Старый44 не был успешно запущен для нового baseline: никаких свежих baseline цифр не заявляется.

## VERIFIED

Production binary собран из source aa6e0e1b58defc5516ca2ffe707d7b1a1af62b1d. Все580 runtime/config inputs совпадают с прошедшим1123/75 suites source d2bf5bf06e3a04ea05c30f24419b46b690cd42e7; промежуточные commits меняют только evidence. Export staging выполнил5 автоматических Unity import migrations: DefaultVolumeProfile, Mobile_RPAsset, URPGlobalSettings, PlayerSettings batching и UnityConnectSettings enabled1. [Полный patch](device/import-diff.patch.gz), [source/import hashes](device/import-audit.json). Packages и production sources не менялись. Эти deltas нельзя скрывать под утверждением «device binary побайтно соответствует checkout»; соответствуют исходные inputs до штатного импорта.

Xcode build succeeded, подпись проверена codesign --verify --deep --strict (exit0), CoreDevice install exit0, actual gameplay наблюдался после launch. Device identifiers, signing team/certificate и all-process trace не публикуются.

Игра пройдена через реальные UI-команды с обычным Battle.Update. Управление через Device Hub Screen Sharing; это touch injection на физическом iPhone, не самостоятельная human usability acceptance. Никаких правок сохранения/live DTO/Gold/HP и тестовых environment overrides. После пробного размещения не в той клетке начат новый законный NewGame150Gold/20HP; итоговый маршрут ниже относится к нему.

| Завершена волна | HP | Gold | Kills | Примечание |
|---|---:|---:|---:|---|
|1|17|40|2|FLAME1(2,1); запоздалая ability отклонена InvalidPhase|
|2|16|85|7|ability через UI|
|3|12|50|10|добавлена FLAME2(-1,-1); выбран TIDE|
|4|10|100|16|добавлена FLAME3(-2,0), ability|
|5|7|90|22|TIDE4(-1,0)|
|6|7|100|32|TIDE5(2,-1), ability; выбран GALE|

Таблица — операторские наблюдения HUD, не автоматический deterministic replay. Моменты ability отличаются от ускоренного Editor script, поэтому Editor41kills/16HP/82Gold и native32kills/7HP/37Gold не смешиваются. После6 волн проданы2 FLAME (+49 каждая) и2TIDE (+42 каждая):282Gold. Все дальнейшие состояния подтверждены read-only copied [checkpoint files](device/phone-flow.json):

| Действие | Gold | Tower1 | Подтверждение |
|---|---:|---|---|
|Continue L1|282|FLAME(2,1),L1,invested70|[native](device/natural-L1-solo.png)|
|Upgrade105; Continue|177|L2,invested175|[Inspect](device/natural-L2-inspect.png), [Continue](device/natural-L2-solo.png)|
|Upgrade140; Continue|37|L3,invested315|[MAX/range3.2](device/natural-L3-inspect.png), [Continue](device/natural-L3-solo.png)|
|Combat7→Menu→Continue|37|тот же L3|checkpoint SHA совпадает с L3 до боя|
|SellL3 (+220)|257|нет башен|[native](device/natural-L3-sold.png)|
|Menu→Continue|257|нет башен|[native](device/natural-sold-final.png), тот же checkpoint SHA|

Во всех checkpoint observations tick2815/HP7/kills32/Preparation7. TowerId1, позиция и nextAttackTick2845 сохраняются при Upgrade/Continue. Старая модель исчезает; палитра не перекрашена; Inspector/range и MAX соответствуют уровню. В коротком L3 Combat7 наблюдались tick2825→3030, kills32→33, Gold37→42; затем Pause и rollback снова вернули исходный37Gold. Это наблюдение UI, без опубликованного непрерывного видео.

7 опубликованных native PNG не редактировались и не масштабировались. Два других кадра содержали OS notification и исключены, соответствующие чистые game-only checkpoint JSON оставлены. Raw video/личные фото не копировались в репозиторий.

Activity Monitor20s завершился успешно. [21 filtered game-only numeric samples](device/production-memory.csv): span19.94s, physical footprint264505424–292685904bytes (peak279.13MiB), resident277725184–278216704bytes (peak265.33MiB), CPU utilization19.68–32.42%,43threads. [Summary](device/memory-summary.json). Окно включало Preparation/Combat5, Screen Sharing ON; запись экрана тогда ещё не была запущена. Выборка одного PID подтверждена существующим extractor из ART2b; данные других процессов/идентификаторы не публикуются.

## NOT VERIFIED / BLOCKED

- Это native memory observation, не FPS/CPU frame time/GPU/performance gate. Нет matched ART2b microbenchmark, drawcalls/overdraw/thermal или длительного dense stress. Renderer/submesh audit не заменяет измерений.
- Попытка Game Performance10s завершилась disk full; trace не засчитан. Освобождены только собственные generated Library и Xcode intermediates; чужие worktrees/data не менялись. Повторный тяжёлый trace не запускался.
- CoreDevice screen-record вернул unsupported capability1001. Запись через нативный Control Center была запущена, остановлена, iOS показала «сохранена в Фото», Image Capture показал новый MP42622×1206. При выборе/переносе Image Capture перестал отвечать через AX (три timeout); ролик не доставлен в evidence и не является VERIFIED normal-speed video. Приватные OS frames не публикуются.
- Полная dense/status matrix, human touch, safe areas других устройств, background/lock interruption matrix и художественное approval владельца остаются открытыми. L1/L2 отличаются слабее, чем L3; native comparison доступен для review.

PR остаётся Draft/Refs76/Refs44. Финальное VFX/projectile #47 и другие семейства отдельно.
