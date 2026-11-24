# VFX.1d / #93 — Печать Разрыва, Страж и приёмка боя

База `913adb1d4d255fe4fc6683c18091b8379f38f917`, существующий checkout и Unity 6000.3.28f1. Scope включает presentation Battle, регрессии и Editor evidence. Domain, баланс, конфигурации, сейвы, энкаунтеры, модели, принятые анимации ART5, карта, камера и lighting сохраняются.

## Печать Разрыва

`AbilityController` владеет существующим preview lease. Pointer/touch выбирает центр, `CheckCastAbility` определяет допустимость с текущим radius, Confirm публикует один intent, Cancel очищает линии и lease. После fixed Steps `LateUpdate` передаёт intent в прежний синхронный `CastAbility`. Принятый `AbilityCastEvent` несёт центр и радиус. В тех же streams эффект попадания принимает `DamageResolvedEvent` с `Origin=Ability`, совпадающими `Tick`, `SourceAbilityId`, `AbilityCastSequence` и положительным `AppliedHpLoss`. Точка и EnemyId берутся из события, включая lethal после удаления цели. Порядок относительно реакций и `finally` за каждый Step сохраняются.

У превью тонкая граница заданного радиуса и центральный знак. Допустимая точка янтарная с ромбом. Недопустимая точка красная с крестом. Cancel, смена владельца, потеря фокуса и восстановление очищают геометрию. Принятая область рисует краткую янтарную окружность и раскрывающийся знак на земле до 0,50 с. Каждое фактическое попадание поднимает короткую угловую искру из captured точки до 0,34 с. Ширина линий уменьшается при рассеивании. Область остаётся прозрачной для HP, статусов и пути.

Четыре слота области по две линии и 28 слотов попаданий по одной линии. Циклическое переиспользование в каждом пуле отдельно сохраняет сигнал применения даже при насыщении попаданиями. Preview использует две линии. Все работают с существующим shared material и ограничены 32 вершинами на линию. `AppliedHpLoss`, lethal, ID и captured geometry очищаются при reuse, expiry и reset.

## Страж Разрыва

Сигналы привязаны к EnemyId существующего Battle view. При первом наблюдении живого Стража открывается короткая лавандовая скоба у ног. При первом наблюдении на последних 2,2 м пути появляется направленный к Сердцу янтарный шеврон. Сигнал расстояния участвует только в визуализации. Для установленного владельца lethal использует captured damage position и ломаное лавандовое рассеивание, leak использует конец пути и коралловый шеврон. Смерть и leak взаимоисключающие. Сигналы живого владельца освобождают слот при завершении lifecycle. Прежний ART5 tail продолжает своё движение и затухание.

Восстановление view при reset не публикует появление и помечает уже пройденный порог угрозы. Если враг появился и исчез в одном Step до первого живого snapshot, presentation сохраняет прежнее отсутствие владельца и визуала lifecycle. Четыре prewarmed слота по одной линии. Сначала свободный слот, затем самый старый сигнал. Lifetime 0,46–0,62 с, локальная геометрия до 13 вершин и существующий shared material. Active clock останавливает всю анимацию на Pause/background и пропускает первый delta на Resume.

## Acceptance и регрессии

| ID | Проверка |
|---|---|
| VFX1D-01 | Valid/invalid preview по авторитетным центру и радиусу, Cancel, queued/rejected Confirm и повторный ввод. Проверка расходов, HP и отсутствия ложных эффектов. |
| VFX1D-02 | Точный ключ cast/hit, положительный HP loss, lethal captured endpoints, несколько событий и Steps, пустой consumer, исходный порядок ability/reaction и очистка в `finally`. |
| VFX1D-03 | Живой boss spawn, один threshold cue, death/leak и прежние ART5 poses, повторный snapshot, reset, never-presented spawn→death. |
| VFX1D-04 | Bounded pools, finite geometry, shared material, saturation, expiry, Pause/background/Resume, Sell/Upgrade и restore/Continue/Retry/NewGame/menu/unload. |
| VFX1D-05 | Плотный production Update в Battle. Шесть семейств, два overhead slots, четыре статуса, реальные Steam/Storm × Intrinsic/StatusPair, способность и босс. Видимые HP, selection, range, inspector и маршрут. PNG по фазам, непрерывное видео, Editor counts и profile. |
| VFX1D-06 | Fresh побайтные FLAME/GALE Victory8 traces, REG214, public earned state, полный snapshot/Grid и сохранённые bytes при файловом Continue. Полные gates и read-only проверка Git, settings/carry-in, suites, replay и media с малыми negative controls. |

Риски: попадания могут перекрыть друг друга в толпе, старый cosmetic cue вытесняется при насыщении, pause/reset могут оставить stale vertices или повторно запустить lifecycle, captured video может замедлить Editor. Проверки используют актуальные Play Mode кадры, фактические события и измеренный Editor clock. Physical iPhone отложен до prerelease QA #53/#54/#55.
