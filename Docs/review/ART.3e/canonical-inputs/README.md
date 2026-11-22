# P2 canonical rendering inputs — PR85 / #84

Недеструктивное доказательство относится к **стабилизированным эффективным значениям Battle rendering** в прежнем macOS Editor/URP17.3.0, а не ко всему состоянию Editor. TestedSource остаётся `9654133029902fb2541ee833d6ef80710755a5bb`. Никакой product C#/model/binding/gameplay/balance/config/package правки нет.

## VERIFIED

Поставлены [четыре canonical файла](canonical/) и [четыре точных carry-in файла](carry-in/), [полный Git→carry-in diff](git-to-carry-in.diff) и [машинный field/source/dependency report](equivalence.json). Canonical bytes проверяются против Git blobs TestedSource. Carry-in SHA256 проверяются против исторического `reports/final-gates-restored.json`, то есть это те же четыре входа, а не новые выбранные версии. Raw files сохранены без нормализации; локальные attributes помечают только эти evidence bytes как binary для Git display/line endings.

```sh
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.3e/canonical-inputs/verify_inputs.py
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.3e/canonical-inputs/test_inputs.py
```

Comparator разрешает ровно перечисленные различия. **Все остальные bytes** каждого файла должны совпасть: shader/texture/renderer-feature refs, material floats/colors/keywords/render queue/passes/flags и pipeline settings не отбрасываются общим фильтром. Settings snapshot содержит все19 source-файлов Assets/Settings + QualitySettings/GraphicsSettings/manifest/lock. Все 142 code/texture/meta dependency hashes, включая95 shader includes, сохранены и проверяются заново из существующего PackageCache; без подходящего cache проверка откажет, а не объявит PASS.

### Pipeline: все12 различающихся полей

| Поле | Canonical → carry-in | Доказательство |
|---|---|---|
| k_AssetVersion | 12→13 | OnAfterDeserialize ветка `<13` меняет counters; более ранние ветки не выполняются при12 |
| k_AssetPreviousVersion | 12→13 | delayed UpgradeAsset ветка `<13`; итоговый counter13 |
| m_ReflectionProbeAtlas | отсутствует→1 | initializer `true`, UniversalRenderPipelineAsset.cs:554 |
| m_AllowPostProcessAlphaOutput | отсутствует→0 | initializer `false`, asset.cs:613 |
| m_PrefilterAlphaOutput | отсутствует→0 | initializer `false`, Prefiltering.cs:125 |
| m_PrefilterScreenSpaceIrradiance | отсутствует→0 | initializer `false`, Prefiltering.cs:179 |
| m_PrefilterBicubicLightmapSampling | отсутствует→0 | initializer `false`, Prefiltering.cs:193 |
| m_PrefilterReflectionProbeRotation | отсутствует→0 | initializer `false`, Prefiltering.cs:198 |
| m_PrefilterReflectionProbeBlending | отсутствует→0 | initializer `false`, Prefiltering.cs:202 |
| m_PrefilterReflectionProbeBoxProjection | отсутствует→0 | initializer `false`, Prefiltering.cs:206 |
| m_PrefilterReflectionProbeAtlas | отсутствует→0 | initializer `false`, Prefiltering.cs:210 |
| m_UseLegacyLightmaps | 0→отсутствует | Поля/FormerlySerializedAs target нет ни в одном актуальном asset partial; действующий m_PrefilterUseLegacyLightmaps присутствует и одинаков0 |

Code refs выше — `Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRenderPipelineAsset*.cs`, точные SHA в report. При смене initializer/version branch verifier отказывает. Не предполагается произвольная эквивалентность других миграций.

### Полный delayed upgrade, включая ResourceReloader

Внешний `UpgradeAsset` вызывает **и** `ResourceReloader.ReloadAllNullIn`, **и** `EditorUtility.SetDirty` (asset.cs:1999–2000); они не замаскированы правилом counters. Reloader перебирает поля, а не properties. У самого URP asset единственная ReloadGroup — `m_Textures/TextureResources` в Runtime/Deprecated.cs:143–171. Уже существующая группа не заменяется; blueNoise64LTex и bayerMatrixTex имеют одинаковые ненулевые fileID2800000/type3/GUID и разрешаются в ожидаемые PNG/TextureImporter meta. Их GUID/path/meta/PNG bytes тоже pinned. Следовательно FixGroupIfNeeded/SetAndLoadIfNull не присваивают значения и ReloadAllNullIn возвращает changed=false. DefaultResources field m_DefaultShader не имеет Reload attrs; renderer getters и renderer feature refs не обходятся.

Базовая иерархия сверена reviewer по [официальному RenderPipelineAsset6000.3](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Runtime/Export/RenderPipeline/RenderPipelineAsset.cs), его deprecated partial, ScriptableObject и Object: relevant Reload/ReloadGroup полей нет. Внешний SetDirty регистрирует необходимость сохранения согласно [Unity6000.3 API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/EditorUtility.SetDirty.html); сам по себе он не сохраняет файл и не меняет GPU parameters. Canonical asset проходит delayed migration/dirty registration, carry-in13 — нет. **Editor queue/dirty state не объявляется идентичным.** Native LoadSerializedFileAndForget не запускался: его callbacks не считались заведомо read-only.

### Материалы: три различающихся legacy alias

Shader GUID933532a4fcc9baf4fa0491de14d08ed7 разрешается в URP/Lit. Только `_Color` отличается: canonical white → carry-in копия BaseColor с float roundtrip. Это **не численное равенство `_Color`**. `_BaseColor` и каждый прочий serialized byte совпадают. Lit.shader:9 помечает BaseColor как MainColor; LitInput.hlsl:20 помещает его в UnityPerMaterial. Единственное `_Color` в shader — obsolete property (Lit.shader:68), после SubShader его нет. Все95 разрешённых include files не читают `_Color`, включая meta/depth/shadow/motion/GBuffer passes. Все product Presentation C# проверены по source blobs; прямого consumer `_Color` нет. TowerDisplay и ReactionEffectView используют `_BaseColor`.

Шесть отсутствующих optional includes перечислены полностью в report: GameCore/XBoxOne/PS4/PS5 platform API и PS5 foveation находятся под соответствующими SHADER_API guards; они не используются macOS Editor. VFXMatricesOverride под HAVE_VFX_MODIFICATION, который не определяется Lit graph; VFXGraph package в этом проекте отсутствует. Неизвестный отсутствующий include — FAIL; это не общий skip зависимостей.

Legacy alias всё ещё существует для Editor/lightmapper compatibility. BaseShaderGUI.cs:939–940 копирует **BaseColor→Color**, не наоборот. BattleSceneBuilder.SetMaterialColors также пишет BaseColor и legacy Color; TowerDisplay PropertyBlock направлен в BaseColor. Таким образом различие legacy slot не меняет текущие Battle GPU consumers. Новое baking/lightmapper/console/VFXGraph acceptance этим не заявляется. Другой shader, consumer, keyword или любой необъяснённый field difference будет отклонён.

### MaterialPostprocessor: обратный legacy updater не выполняется

MaterialPostprocessor.cs:473 действительно содержит `RenameColor("_Color", "_BaseColor")` в LitUpdaterV1. Он не скрыт и не приписан направлению BaseShaderGUI. У всех трёх canonical и carry-in .mat уже есть встроенный AssetVersion10 с GUID d0353a89… (Selection/Preview в начале, Tower в конце YAML). GUID разрешается в Editor/AssetVersion.cs; `k_Upgraders` имеет ровно10 элементов. Existing-subasset branch получает version10, затем `10 < 10` ложно: UpgradeV1 и обратное присваивание не выполняются. Дополнительно ProjectSettings/URPProjectSettings.asset + script identity pinned: m_LastMaterialVersion10, поэтому version-triggered ReimportAllMaterials не запускается. Missing-subasset/project-version fallback не нужен для этих трёх файлов. MaterialPostprocessor/UniversalProjectSettings/ShaderUtils/AssetVersion code+meta включены в dependency hashes; negative controls отклоняют embedded version0 и project version0. Новые/исторически старые материалы этим proof не охватываются.

## Проверки и ограничения

[21/21 Python controls](tests-final.txt) PASS: меняются строки только в памяти, проверяются реальные active values/defaults/refs/keywords/texture/render queue/unknown fields, effective default source drift, HLSL legacy consumer и quality drift. Первая контрольная попытка18/19 сохранена в [tests-first-attempt.txt](tests-first-attempt.txt): renderScale mutation ошибочно искала1 вместо фактического0.8, поэтому не меняла input; теперь mutation подтверждается assertNotEqual. Генератор сначала попытался UTF8-декодировать PNG (исправлено на byte hashing), затем обнаружил неучтённый уже установленный universal-config17.0.3 dependency (включён в graph). Это не скрытые PASS.

[Actual-input receipt](actual-inputs.json) связывает старые Unity reports/media, точные четыре использованные SHA и canonical counterparts с этим certificate. **Новых Unity Edit/Play/compile/captures/video/device runs нет.** Старые1195/85 — старые положительные suites, а не новый run. P2 закрывается предусмотренным issue84 доказательством эквивалентности эффективных rendering inputs, после независимого re-review. Source/runtime не менялись, поэтому full Unity suites повторно не запускались.

User files при анализе/controls не заменялись; их исходные SHA подтверждены после проверки. Финальный полный delivery verifier сохраняет прежние clean/source/artifact/protected checks и дополнительно исполняет этот certificate. Для его clean окна применяются только ранее разрешённые временные Git bytes4 и finally restoration с внешним backup. Никаких новых worktrees/clones/Unity copies, device/build/signing/merge/release действий.

**Финальный результат:** [независимое re-review](re-review.md) подтвердило устранение P2. Полный усиленный verifier на committed P2 head выполнен исполнителем и reviewer: PASS3511 source / 3008 protected / 120 artifacts; [receipt](delivery.json) включает восстановление exact user bytes4/4. Новый delivery receipt затем включён в manifest; финальный evidence-only head проверяется ещё раз. Model/gameplay source и historical media/reports не менялись.
