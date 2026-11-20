# ART.3a — FLAME L1–L3 / #76

Основа: `7dfdf0ec1bc1ef63e6df76810cfd08ad6de5181f`, merge PR75 подтверждён. Scope: одно семейство FLAME, production Battle binding, source/import и visual evidence. Направление/открытый фонарь приняты в #41/#73; guardrails #43 относятся только к art-reference microbenchmark iPhone16Pro. Родитель #44 остаётся открытым.

## Контракт до реализации

- Отдельные `ArtSource/Blender/ART3/` и `Assets/_Game/{Art/Models,Prefabs}/ART3/`. Исходник — принятый ART1 Tower_FlameLantern.blend, не повторная генерация ART1. L1 сохраняет открытые опоры, ядро и узкую крышу; L2 добавляет боковые бронзовые рёбра/чаши; L3 — разомкнутый венец и раздвоенное ядро. Различия конструктивные при одной камере/масштабе, без uniform scale/роста крыши. Основание ≤.94m, root identity, ground pivot, прежние оси/preset/палитра. ≤5000tri/2slots, без новых textures/packages/lights/bloom.
- Минимальный компонент на TestTower выбирает три serialized prefab references по authoritative `definition.Id == flame` + level. Смена уровня отключает старый model root до deferred Destroy. Graybox renderers остаются fallback других типов/legacy. Palette renderers не получают type-tint MaterialPropertyBlock. Builder воссоздаёт binding; placement preview сохраняет текущий graybox footprint/range. Collider/selection/Domain coordinates прежние. ShotSource — только локальный prefab anchor, generic AttackBeam остаётся текущим до #47.
- Place/Upgrade/Continue/Combat rollback отображают правильный уровень; Sell/Restart/reload не оставляют активных дублей. Новых save fields и simulation changes нет. content600/armor5/schema4/цены/урон/скорости/spawns/способность/packages/ART1/history защищены.
- Кадры: одиночные и рядом L1/L2/L3 в исходной Battle camera/scale/light; natural public-command flow с реальными бюджетами, checkpoint/rollback; отдельные подписанные synthetic front/side/behind/dense probes. HP/status/inspect/build controls читаемы, game coordinates не сдвигаются ради кадра. Actual advancing PlayMode, frame/tick/Console audit обязательны. ArtReference delay .2s не переносится в Battle.
- Physical iPhone: кадры/короткая advancing последовательность, точный source/profile/version/model/OS/quality/resolution и доступные измерения с сопоставимым baseline. Недоступное устройство/метрики явно NOT VERIFIED, PR только Refs76 до полного acceptance. Editor не выдаётся за device.

## Regression scenarios

- ART3A-01: Blender source/export и Unity mesh сохраняют ground/pivot/identity/forward/UV/material slots; три уровня отличаются геометрией, крыша не шире принятой. Повторный export даёт тот же imported geometry hash/GUID/local IDs/dependencies; ART1 bytes неизменны.
- ART3A-02: saved prefab binding + builder references валидны; FLAME L1→L2→L3 меняет только model child, сохраняет TowerId/position/scale/colliders, имеет один активный model root и неизменные shared palette materials без tint. Повторный Apply не создаёт объекты. Неизвестный тип/уровень возвращается к fallback.
- ART3A-03: actual Battle/public Place→earned budget→Upgrade2→earned budget→Upgrade3→Sell; repeated rejected commands не меняют Gold/roots. Не редактировать live Gold/HP/checkpoint DTO для natural proof. Повторяемый checkpoint Continue и interrupted Combat rollback сохраняют ID/level, не повторяют rewards. ResetPresentation/reload очищают старые roots в текущем кадре.
- ART3A-04: TIDE/GALE/STONE/STEAM/STORM/test-tower и preview сохраняют текущие renderer/tint paths. CellSize .5/1 масштабирует модель от ground и не меняет radius/selection/collider.
- ART3A-05: одинаковые actual PlayMode игровые кадры всех уровней поодиночке/рядом; front/side/behind enemy/HP/status probe отдельно помечен synthetic. Advancing natural combat и Console audit. Generic beam явно подписан как временный; finalVFX47 не закрыт.
- ART3A-06: static, полные доступные EditMode/PlayMode с positive totals, diff/compile/meta/GUID/reference/protected-source check; tracked hashes/sourceSHA/manifest/clean-checkout verifier; независимый source/visual/evidence review. Никакого self-merge/release/TestFlight.

Риски: FBX submesh/localID drift, flattening palette из ApplyVisual, отложенный Destroy при upgrade/restore, occlusion низких graybox врагов высокой моделью, authored prefab refs после builder, свободное место для IL2CPP и недоступность физического iPhone. Результаты и ограничения будут записаны отдельно от specification.

## Локальная правка по первому visual probe

Первый synthetic behind probe выявил скрытый корпус graybox врага. Используется уже проверенный #73 stencil mask/cue: один mask renderer на модели FLAME; body и две HP-плоскости врага получают overlay только в пикселях маски, где они скрыты башней. Обычный body/color/HP ratio/anchor/status прежние; HP cue следует той же fill transform, два маленьких material assets сохраняют исходные background/fill colors. Нет нового shader/render feature, анимации, масштаба или изменения gameplay. Это три дополнительных presentation passes на враге и один на FLAME; их фактическая device стоимость остаётся NOT VERIFIED до physical proof. REG ART3A-05 дополняется проверкой idempotent mesh sharing и ratio/цвета HP cue. Final VFX отдельно #47.

## Доработка R2 после независимого review #76 (20.11.2026)

Вход: PR77 reviewed head31369a2. Ранее L2 имел недостаточно крупное отличие от L1; существующие device кадры/память остаются историческим PARTIAL evidence.

Узкий delta: открытые диагональные бронзовые кронштейны и две крупные чаши в средней части L2, унаследованные L3; L1 и принятый ART1 не меняются. Максимальные XY±.47m/крыша/ground/scale/shared palette и budgets прежние. Geometry regression дополнительно проверяет раскрытие средней конструкции в проекции игровой камеры; реальная визуальная приёмка отдельно, числовой extent её не заменяет.

Для сопоставимой стоимости — существующий art-reference profile и ArtDeviceProbe sampling5s+30s×3; явный selector ART1/L1/L2/L3, одинаковые camera/quality/12enemy dense+projectile/ScreenSharingOFF. Ссылки на production prefabs внедряются только в build scene ArtReference; saved scene и production Bootstrap→MainMenu→Battle contract сохраняются. Новых ресурсов/пакетов/Domain/save fields нет. Regression: пустой selector сохраняет ART1, unknown selector отклонён, три prefab refs присутствуют только в art build scene, единственный tower model без дублей/повторного mask, measurement metadata фиксирует вариант и structural renderer/slots. Существующие raw CPU/playerloop/GPU/Unity memory samples сохраняются,0counters не становятся измерениями; native memory собирается одинаково отдельным инструментом с раскрытым overhead.

Evidence R2 хранится отдельно в review/ART.3a/revision-2. Сопоставимые singles/lineup/occlusion перед сборкой, затем полные EditMode/PlayMode и source roundtrip. На iPhone: естественный earned Upgrade L1→L2→L3 и advancing combat на непрерывном native video; front/side/behind/dense probes явно synthetic, actual Battle HP/status/inspect отдельно. Transfer failure должен оставлять точную диагностику и незакрытый gate. После конечного source — manifest/verifier/чистый checkout/независимый review; PR77 остаётся Refs76/44 до принятия.
