# Проверки ART.3e

TestedSourceSHA: `9654133029902fb2541ee833d6ef80710755a5bb`; baseline main после PR83: `62d4dbd17ef9a8179f2a9ee6163dd7da75a72e12`. Проверки выполнены в существующем checkout, без нового worktree, клона или копии Unity project. [Точные команды и неуспешные попытки](commands.md).

## VERIFIED

| Проверка | Результат | Evidence |
|---|---|---|
| Static checks на финальном source | 38/38 PASS, meta/GUID/asmdef/packages | [report](reports/static-final.txt) |
| Полный EditMode на финальном source | 1195/1195 PASS, failed/skipped/inconclusive 0, 281.04 s | [per-case JSON](reports/edit-mode.json), [команда](reports/full-edit-command.txt) |
| Полный PlayMode на финальном source | 85/85 PASS, failed/skipped/inconclusive 0, 80.56 s | [per-case JSON](reports/play-mode.json), [команда](reports/full-play-command.txt) |
| Compile audit | errors 0, warnings 0; после canonical readback up_to_date | [audit](reports/compile.json), [readback audit](reports/compile-delivery.json) |
| STEAM feature capture Console | Нет Error/Exception/Assert | [fresh flow](reports/console.txt), [video](reports/video-console.txt) |
| Общая Console suites | 113 ожидаемых checkpoint IO failures + 1 ожидаемое исключение frozen measurement guard; 114 captured records / 25 distinct Editor errors | [raw](reports/console-suite-errors.json), [status](reports/console-final.json), [classification](reports/console-suite-classification.json) |
| Source/export/import | Три editable .blend; 1696/1816/2088 triangles, два shared slots, UV/normals/semantic groups/units/pivot/ground/footprint | [source manifest](../../../ArtSource/Blender/ART3/STEAM/geometry-manifest.json), [Blender check](reports/geometry-final.txt) |
| Реальный reexport | Все три .blend экспортированы, before/after geometry/UV/normals/remaps/GUID/localIDs/refs идентичны; финальный canonical readback совпал | [roundtrip](reports/roundtrip-final.txt), [before](reports/reexport-before.json), [after](reports/reexport-after.json), [canonical](reports/reexport-canonical-readback.json) |
| Hybrid/earned flow | Настоящие unlocks; 50 kills / HP14; бюджет 498, investment 495, Gold3; repeated file/menu Continue; полный rollback с live Grid; Sell refund346, Gold349 | [trace](reports/natural-trace.txt), [commands](inputs/earned.commands.json), checkpoints L1/L2/L3 и video-safe/rollback-wire |
| Intrinsic Steam | Настоящий AttackEvent + связанный primary/secondary positive HP loss; в видео 2 hits / 102 applied HP loss, primary HP15; basic status payload пуст | [receipt](reports/video-intrinsic.json), [Inspect](reports/video-inspector.txt), финальные PlayMode cases |
| Визуальные проверки | Actual Play Mode singles/lineup/пять семейств, все 9 front/side/behind и dense12; catalog/preview/Inspect/range; continuous Editor video и exact-stage PNG picks | [обзор и изображения](README.md), [video proof](reports/video-proof.json) |
| Малые negative controls | Positive fixture и отклонение clean committed foreign source inventory/source bytes/artifact bytes | [current controls](reports/delivery-controls.json) |
| Независимое финальное review после PR | Новых P1/P2 нет; source/artifact hashes, full saved suite results, модели/occlusion/video/PTS/earned flow проверены отдельно; merge BLOCKED clean gate | [заключение](review.md) |
| Восстановление после suites | Только 17 созданных тестами сериализаций возвращены к pre-run bytes; четыре исходных пользовательских файла сохранены; прочий Assets/Packages/ProjectSettings совпадает с source | [byte audit](reports/final-gates-restored.json), [generated-only patch](reports/final-gates-generated-only.patch.gz) |

Общая Console **не объявляется нулевой**: все 114 записей сохранены, checkpoint stacks связаны с существующими LogAssert.Expect, frozen guard ожидается ArtReferencePlayModeTests.WarmupActivityCannotApproveFrozenSampleWindow. До suites новый task Editor имел 0 errors / 0 warnings ([startup](reports/console-startup.json)). Console не очищалась ради итогового результата.

Исходные четыре dirty файла присутствовали при тестах и визуальных captures. Их список и SHA256 сохранены в byte audit. Они исключены из PR, но это ограничивает утверждение о проверке полностью канонических source inputs. Media/product inputs заморожены в `2a0e05175b87a636b86db5136a7252a357241194`; delta до TestedSource — только более строгая same-event assertion, после которой полные suites запущены заново.

## NOT VERIFIED / BLOCKED

**Обязательный полный read-only delivery verifier в clean tracked checkout — BLOCKED.** В существующем checkout сохранены исходные пользовательские изменения:

- `Assets/Settings/Mobile_RPAsset.asset`
- `Assets/_Game/Art/Materials/GrayboxSelection.mat`
- `Assets/_Game/Art/Materials/GrayboxTower.mat`
- `Assets/_Game/Art/Materials/GrayboxTowerPreview.mat`

Текущий entry point `PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.3e/verify_delivery.py` обязан отклонить их как dirty tracked source. Это не PASS и не отменяется положительными малыми controls. По правилу #84 чужие изменения не сбрасывались. Запрошено разрешение сохранить ровно эти четыре файла вне проекта, временно проверить Git bytes в том же checkout, затем восстановить пользовательские bytes; без ответа этот шаг не выполняется. Draft/Refs #84 нужны из-за этого незакрытого gate, а не из-за iPhone Deferred.

Historical ART3d tamper matrix не запускалась заново. Current small-fixture controls проверяют только common inventory/blob/artifact validators; не доказывают полный entry point. Никакого заменяющего clean checkout или обхода clean check не создавалось.

**Device Deferred to prerelease QA #53/#54/#55.** Physical iPhone, native recording/benchmarks/binaries, iOS export/signing/TestFlight не выполнялись. Editor captures не подтверждают device performance, touch/safe-area/background. Sparse capture overhead не является benchmark. Видеопрефикс волн 4–7 использует подписанный test clock; Combat8 — production Update. Synthetic positions/HP/copied Wet snapshots не являются естественным боем и не приписывают STEAM basic Wet.

STORM, parent #44, карта #45, враги #46, VFX #47, UI #48, G2 и release остаются отдельно. Merge/release не выполнялись; продуктовая приёмка — владельцем после независимого review.
