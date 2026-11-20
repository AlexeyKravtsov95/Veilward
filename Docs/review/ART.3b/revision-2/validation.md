# ART.3b R2 — validation

Tested SourceSHA **696dc8ae3cc7799bea586c57d44749163300b06c**. Runtime HP correction8a3bce0; baseline main57cae61/merge77. Binding/оригинальные модели исходного пакета сохранены. R1 aa effdb source/reports/screenshots/video остаются историческими; его открытые P2 не скрыты.

## Исправленные замечания

**P2 HP.** Старый localY.85 помещал низ HPquad на9.8148px ниже projected capsule head при исходной camera(8,11,8),ortho8.5/900px. Y1.15 тоже недостаточен (-2.98px), y1.5 пересекает второй status slot. LocalY2.0 даёт16.38px над головой, gap3.55px над вторым и5.54px под первым glyph slot. Новый regression использует реальные mesh vertices, font glyph bounds, Face/ratio/anchor и проверяет отсутствие overlap, а не просто числоY. RED0/1 и промежуточная float-equality fixture error сохранены отдельно. Final targeted Edit43/43, TidePlay2/2+FlamePlay1/1, capture2/2 PASS. Saved Shade и builder отличаются только необходимым HP anchor; materials/passes и root/Domain positions прежние.

Новые front/side/behind всех3, dense12 и real-time Wet/Inspect видео показывают полную зелёную/тёмную полосу. Сопоставление с принятыми FLAME behind выполнено при той же camera/scale/light; новая геометрия моделей не понадобилась. Status labels и численный Inspect сохранены.

**P2 video pick.** Выбор по подстроке заменён exact stage identity, все6 source indices и hashes различны. `video-L3.png` — frame68 доCombat, `video-rollback.png` — frame159 послеContinue. Копии сверяются с source PNG SHA и telemetry; verifier независимо проверяет stage prefix/row/index/hash и запрет duplicate picks. Непрерывный clip204frames/20.891s переснят после HP delta.

**P3 negative controls.** После подготовки финального manifest выполняются из отдельного clean checkout; точные команды/выход будут сохранены в delivery-controls.json. Утверждение о выполнении не подменяется планом.

## Play Mode и источник бюджета

Production New Game/public commands, первые6волн явно test-clock; после них tick2589/HP16/kills41. Законная продажа армии, затем TIDE TowerId6/cell(2,1): L1Gold316/invested60 → Upgrade90 → L2Gold226/invested150 → Upgrade120 → L3Gold106/invested270. Sellrefund189 → Gold295. Duplicate/MAX/rejected команды, repeatedContinue и полный Combat rollback проверены без изменения reward/Domain/schema4.

R2 video — отдельный replay byte-identical earnedL1 SHA2ab6aa15df3c8d753a27dfba0da1fb8516fc975bc34000dd45a6c2d092bd82a0, а не второй fresh1…6run. Actual Wet ticks2693…2750, InspectHP64/100→46/100; rollback к Preparation2589/Gold106/HP16/kills41/L3. SafeL3 SHA0c74442e40e5b3aa7f3adcd1a63c2da9d3d9485f3590655e1670c3b646719325 прежний: visual anchor не попадает в save. Scenario Console0warnings/errors после успешного capture.

Ранний R2 video attempt завершился до первого PNG из-за ScreenCapture/GameView dimension mismatch. Raw failure сохранён video-dimensions-before*. Новые preflight проверяет именно GameView render target до store/session действий, abort останавливает gameplay coroutine до снятия isolated factory. Источник успешного clip — установленный Pipeline `capture game_view` encoder/RT1600×900; package source раскрывает backbuffer/focus/GUI-scaling проблемы старого ScreenCapture. Только task Editor, никаких изменений original main/user sessions. В GameView размер выбирается после входа вPlay; Screen.width из Editor callback не является размером composited RT.

## Полные финальные gates

Static38/38 и project checks PASS. **Full EditMode1144/1144 и PlayMode79/79 PASS после последнего runtime delta**, failed/skipped/inconclusive0. R1 totals не подставлялись. Compile up_to_date/errors0, реальный reexport3/3 и canonical FBX readback включая Shade/HP refs PASS. Read-only clean-checkout и negative controls фиксируются отдельно перед финальной поставкой.

Unity6000.3.28f1(8f082d2fe476), URP17.3.0, Pipeline0.10.0-exp.1, CLI1.0.1, Blender5.3.0, macOS27.0.1/ARM64, BuildTargetiOS, QualityMobile, camera(8,11,8)/ortho8.5/light1.2, PNG1600×900. Три TIDE1356/2084/2308tri/2slots, ground0, width≤.937m. Новых packages, materials, textures, renderfeatures/lights нет. Source/.meta/GUID/refs и protected baseline/history проверяются manifest/verifier.

Обычный builder warmup перед full suite сохраняет раскрытый R1 serialization contract; временные Unity outputs будут сохранены audit/patch и возвращены в exact source bytes. Reexport всех3 проверяет geometry/UV/normals/material remaps/GUID/localIDs/prefab+scene refs, теперь включая Shade+meta. После FBX timestamp-header drift canonical committed bytes возвращаются и заново дают равный Unity snapshot. Исторический R1 packet не меняется, кроме superseded notice README.

## NOT VERIFIED / Device Deferred

Physical iPhone Deferred to prerelease QA #53/#54/#55 по решению владельца20.11.2026. Нет нового device video/matched run/native binary/iOS export/Xcode build, performance/memory/GPU/thermal/touch/safearea/background PASS. Это не перенос Editor defect: HP устранена и перепроверяется сейчас. Owner ART approval/merge, остальные4семейства/родитель44, финальные VFX47 и release/TestFlight отдельно. Без self-merge.

## Независимая перепроверка visual/evidence R2

Fresh R1 reviewer продолжил scoped re-review final fix wave. Оба P2 ADDRESSED: независимо измерены17green pixels во всех behind, front17/side18; dense и actual Wet/Inspect читаемы. Проверены projected mesh/две glyphslots, builder/prefab anchor согласованы, отсутствие новых passes/materials/Domain. Vendor capture RT/encoder прочитан, все204 raw PNG SHA и6picks сверены со stage/telemetry. L3№68 и rollback№159 различны. FFmpeg decode чист,204PTS, maxошибка0.583ms. Новых code/visual замечаний нет; final Evidence verdict ждёт записанные negative controls.
