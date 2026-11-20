# ART.3b — validation и границы

Baseline: `57cae61557ab71a79aa1d67a1d10bdad7cc972cc` (main, merge PR77), принятая FLAME `95a6563` входит в историю. Tested SourceSHA: **`7aef7206b788ca57e754a43c52fe337ab088e3c3`**. Последний production runtime delta — `2badf41`; следующие commits меняют tests и Editor evidence harness вне Assets. Нового iOS build нет: buildSourceSHA не заявляется.

Implementation: GPT-6.1 Sol / High. Независимое scoped review: GPT-6.2 Astra / High. Итоговое review и clean-checkout audit записываются отдельно в [review](review.md) и manifest.json.

## VERIFIED — gates

| Проверка | Полный результат |
|---|---|
| Static self-tests | 38/38 и project checks PASS |
| EditMode | 1143/1143 PASS |
| PlayMode | 79/79 PASS |
| Failed / skipped / inconclusive | 0 / 0 / 0 в обоих Unity suites |
| Actual source reexport / Unity roundtrip | 3/3, before == after |

[Edit report](reports/edit-mode.json), [Play report](reports/play-mode.json), [static](reports/static-final.txt), [команды и окружение](commands.md). Runners проверяют nonempty totals и весь per-case результат; фильтрованные targeted прогоны не заменяют эти полные suites. Compile, diff и clean-checkout результаты добавлены в финальный audit ниже.

## VERIFIED — source, import, binding

Оригинальные editable TIDE Bell L1–L3 созданы в Blender 5.3.0 (`02f0899d78e3`). Один mesh на уровень с semantic vertex groups, полый колокол с языком, открытая рама и водяные чаши. L2 добавляет крупные боковые водосборники, L3 — высокие разомкнутые рога. Общая ART1 palette упакована в source; FLAME geometry не использована. [Sources/provenance](../../../ArtSource/Blender/ART3/TIDE/README.md).

| Level | Source/import triangles | Material slots | Максимум XY | Height |
|---|---:|---:|---:|---:|
| L1 | 1356 | 2 | ±0.45 m | 1.712 m |
| L2 | 2084 | 2 | ±0.46818 m | 1.811 m |
| L3 | 2308 | 2 | ±0.46818 m | 2.383 m |

Meters, identity transforms и ground pivot сохранены. Средняя projected width L1→L2 составляет 0.93338→1.23419 m; числовой guard дополняет actual camera captures. Художественное approval владельца отдельно. Новых textures, materials, packages, lights, bloom или render features нет.

Три production prefabs используют shared Surface/Glow и прежний stencil mask. На prefab два renderers и четыре structural material submissions (model+mask); это не измеренные draw calls. ShotSource local(0,1.1,0) — presentation anchor без изменения Domain coordinates или generic beam. Importer: тот же FBX preset, scale1, ReadWriteOff, без animations/lights/collider.

Единственный `FlameModelBinding` сохраняет GUID, class, прежние `levels`/`fallbackRenderers` и API. Добавлены optional `tideLevels` и cache по type+level. FLAME/TIDE скрывают graybox, palette не получает общий tint. Unknown/legacy/остальные четыре типа/missing refs/invalid levels используют fallback. Старый root выключается до deferred Destroy; повторный Apply не создаёт объектов. TowerId, position, scale, collider, selection, footprint и range сохранены. Старый FLAME-only serialized shape поддержан. Tide builder не пересоздаёт принятые FLAME assets.

Все три .blend реально переэкспортированы в FBX и повторно импортированы Unity. Сравнение включает geometry, UV, normals, submesh indices, remaps, materials, GUID/localIDs, dependencies и prefab/scene bytes: [before](reports/reexport-before.json), [after](reports/reexport-after.json). ART1/FLAME и исторические proofs защищены baseline audit.

## VERIFIED — actual Play Mode

Все PNG — 1600×900, исходная Battle camera (8,11,8), ortho8.5, Quality Mobile, sun1.2 и прежний ambient. [Просмотр](README.md): singles, lineup, TIDE/FLAME comparison и front/side/behind/dense probes явно подписаны SYNTHETIC. В probes назначены enemy positions/HP, а Wet badge копирует read-only snapshot отдельной simulation. Живой Domain/DTO не редактируется. Проверены head/body/HP/Wet и UI controls. Generic beam и graybox enemies остаются до #47/#46.

Natural lifecycle: настоящий New Game и public commands на production data. Первые шесть волн сжаты явно подписанным test clock. После них tick2589 / HP16 / kills41; законная продажа армии оплачивает TIDE Place и Upgrade. TowerId6, cell(2,1):

| Состояние | Gold | InvestedGold |
|---|---:|---:|
| TIDE L1 | 316 | 60 |
| Upgrade за90 → L2 | 226 | 150 |
| Upgrade за120 → L3 | 106 | 270 |
| Sell refund189 | 295 | башня удалена |

Duplicate/MAX/недопустимые команды не меняют ресурсы. Continue повторён дважды на каждом уровне без изменения primary bytes. Actual Wet в Combat, полный safe-snapshot rollback, immediate root cleanup, повторный Sell и restart проверены. [Trace](reports/natural-trace.txt), [binding trace](reports/binding-trace.txt), L1/L2/L3 checkpoint JSON — в reports.

[Editor video](editor-flow.mp4): **204 composited GameView кадра, 20.952 s, 1×, без склеек**, интервалы захвата около0.1s. Это replay побайтно неизменённого earned L1 checkpoint выше, а не второй fresh run1…6. Origin/restore/readback SHA: `2ab6aa15df3c8d753a27dfba0da1fb8516fc975bc34000dd45a6c2d092bd82a0`. Через обычный MainMenu Continue показаны UI Upgrade1→2→3, real-time Update/fixed Step, Wet ticks2693…2750, actual enemy Inspect с HP64/100→46/100/Wet30, Menu Continue → Preparation2589 / Gold106 / HP16 / kills41 / L3, затем Sell → Gold295 / no model roots.

Video L3 checkpoint совпал с natural L3 SHA `0c74442e40e5b3aa7f3adcd1a63c2da9d3d9485f3590655e1670c3b646719325`. [Time/frame/tick](reports/video-frames.txt), [readback](reports/video-checkpoint-origin.json), [PTS/encoding](reports/video-proof.json). PNG picks — неизменённые source pixels. Scenario Console: 0 errors/warnings. Capture overhead не является performance benchmark; sparse captured frames не подтверждают native presentation timing.

## Ранние результаты и import environment

- RED 13/13 Failed до extension — ожидаемая проверка отсутствующей TIDE.
- Ранний targeted Edit17/18: неверная legacy JSON fixture EditorJsonUtility → JsonUtility. Raw report был перезаписан до запроса сохранить; это раскрыто в review, retained raw не заявляется.
- Ранний Play1/2: тест ожидал два colliders, authored tower исторически имеет0. Assert исправлен по authored prefab; отдельно synthetic BoxCollider проверяет сохранность identity/geometry.
- [Ранний full Edit1140/1143](reports/edit-mode-integration-before.json): два stale TIDE graybox assertions117/119 заменены production mesh/palette/fallback assertions. Все проверки экономики, preview и остальных типов сохранены.
- Третий ранний full failure — canonical legacy `_Color` GrayboxSelection white→yellow между первым/вторым Build. `_BaseColor` уже был yellow. Соседний builder/test и принятый material не изменены: после exact source restore выполнен штатный BattleSceneBuilder.Build warmup, затем полный suite1143/1143. Unity также пересериализует URP version12→13, материалы, сцены и localIDs. Эти generated deltas сохраняются в audit/patch, затем исходные bytes возвращаются из HEAD; authored delta этих систем отсутствует.
- Начальный Preview helper вызвал Select до обработки SceneLoad при frozen frame1, когда background override был снят предыдущим Play stop. [Runtime helper error](reports/preview-helper-before.json) раскрыт отдельно; это не production defect. Пустой промежуточный кадр заменён после включения background до Load. Project PlayerSettings остаются baseline; original main Editor пользователя не использовался.

Builder regression теперь делает backup14 outputs до первого Build и восстанавливает их в finally с sync Refresh и byte assertions; точные root refs проверяются до Build. External SHA14 drift пуст. Console сохранена per-case и aggregate, выбранный GameView37/count38 восстановлен. Independent Task1 Spec Compliance и Task Quality PASS после исправлений; исторические goldens/traces не ослаблены.

## NOT VERIFIED / Device Deferred

**Physical iPhone Deferred to prerelease QA #53/#54/#55**, решение владельца20.11.2026. Нет нового iOS export/Xcode build, signing/install, native recording/matched benchmark или device performance/memory/GPU/thermal/touch/safe area/background PASS. Исторические ART2b/FLAME device proofs сохранены без переписывания.

Owner ART approval/merge, остальные четыре семейства и родитель #44, карта #45, враги #46, VFX #47, UI #48, G2/release/TestFlight отдельно. При полном Editor/source/Battle acceptance Closes #78 допустим без нового device proof. Self-merge/release не выполняются.

## Финальный source audit

Полный PlayMode завершён 79/79, EditMode1143/1143, Static38/38. `unity recompile` подтвердил up_to_date / compiling=false / failed=false, errors0/warnings0 ([report](reports/compile.json)); последний source уже скомпилирован и исполнен обоими suites. Diff/meta/GUID/package checks обязательны и не заменяются этим up_to_date ответом.

Финальный roundtrip повторён успешно после операционной ошибки закрытия Editor до after-snapshot; [прерванная попытка](reports/roundtrip-interrupted.txt) сохранена и не является PASS. Успешный [roundtrip-final](reports/roundtrip-final.txt) сравнил все три заново экспортированные модели. FBX exporter меняет также timestamp bytes заголовка: после проверки возвращены точные committed FBX bytes, затем Unity заново импортировала их и дала тот же полный snapshot ([canonical readback](reports/reexport-canonical-readback.json)). Это возврат tested артефакта с равной геометрией, не замена .blend source.

[Generated audit](reports/unity-generated-final.json) содержит каждый временный Unity serialization output, исходный/generated/restored SHA; [patch](reports/unity-generated-final.patch.gz) сохраняет exact delta. Все restored hashes равны committed source, `git diff --check` PASS. New packages/settings/Domain/config changes отсутствуют. Actual final preview: [public API result](reports/build-preview.json), Gold106/towers1/type=tide до/после выбора, покупки не было; [кадр](playmode/catalog-build-preview.png).
