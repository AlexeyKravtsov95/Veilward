# ART.1 — исправления exporter exception и winding Shade

Внешнее review владельца обнаружило два дефекта после исторического R2 (`d23d7ae`). Старый R2 не является утверждением этих исправлений.

## VERIFIED

1. **Ошибка Python больше не может дать PASS на старых FBX.** Production verifier запускает Blender с `--python-exit-code 1` перед `--python`, `check=True` прекращает pipeline до after snapshot/evidence. Настоящий отрицательный контроль исполняет verifier с intentionally failing export script; подменён только Unity snapshot неизменной фикстурой. Проверены return code 1, один Blender call/один before snapshot, отсутствие PASS/записи golden reports и неизменные hashes FBX.
2. **Shade:** отражение координат меняло handedness при прежнем winding. Обе стороны плеч/рогов теперь outward; неплоский quad рога заменён явными triangles с нужной диагональю. В сохранённом `.blend` исправлены только эти faces, FBX экспортирован из него; материалы и culling settings не менялись. Source generator отражает то же исправление. Проверяется geometric cross product индексов, не только imported vertex normals.

## Red → green / команды

| Контроль | До | После |
|---|---|---|
| Actual Blender exception | exit 0 без флага при RuntimeError; exit 1 с флагом | [Фактические коды](fixes/blender-exit-code.json) |
| `python3 ArtSource/Blender/ART1/tests/test_reexport_failure.py` | [FAIL: CalledProcessError not raised](fixes/export-negative-red.txt) | [1/1 PASS](fixes/export-negative-green.txt) |
| Blender `.blend` + exported FBX winding | [RED: inward face](fixes/winding-blender-red.txt) | [8/8 hulls](fixes/winding-blender-green.txt), по 5 vertices/6 outward triangles и positive signed volume |
| Unity `ShadeShouldersAndHorns_HaveOutwardTriangleWinding` | [1/4, 3 failed](fixes/winding-unity-red.json) | [4/4](fixes/winding-unity-green.json) после force reimport |

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python ArtSource/Blender/ART1/tests/check_shade_winding.py
unity command 'test run' --caller plugin --skill unity-cli --format json -- --mode editor --filter ShadeShouldersAndHorns
./Scripts/run-static-checks.sh
./Scripts/run-edit-mode-tests.sh
./Scripts/run-play-mode-tests.sh
python3 ArtSource/Blender/ART1/verify_reexport.py
git diff --check
```

Source correction только Shade воспроизводима по [скрипту исправления сохранённого source](fixes/source-correction.py.txt); source generator также исправлен. `.blend` остаётся редактируемым, GUID вручную не создавались. Полные финальные suites: static **38/38**, EditMode **544/544**, PlayMode **29/29**; 0 failed/skipped/inconclusive. Отдельные Python/Blender controls не включены в Unity totals.

Основные кадры/видео и Console audit в этой директории пересняты в настоящем Editor Play Mode с исправленным Shade. Финальный reexport before/after относится к исправленному source и fail-closed verifier. Production Domain/configs/Battle/MainMenu/packages/ProjectSettings не менялись.

## NOT VERIFIED / OPEN

Физический iPhone, iOS player build, performance/thermal/allocations, художественное утверждение и коммерческая/правовая приёмка оригиналов остаются открытыми. Старый R2 отчёт исторический; повторное техническое review выполняется на новом commit. Self-merge/release не выполняются.
