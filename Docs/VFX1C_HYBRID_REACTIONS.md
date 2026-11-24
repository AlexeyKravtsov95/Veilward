# VFX.1c / #92 — атаки гибридов и реакции

База `4dac946a142e281adc739aeb05ffb9aba136f402`, существующий checkout и Unity 6000.3.28f1. Scope включает presentation Battle, тесты и доказательства. Правила Domain, баланс, контент, checkpoint, четыре принятые атаки и status cues, ability, ART3–5, камера, настройки рендера и packages сохраняются. Финальная плотная приёмка остаётся в #93, физический iPhone отложен до #53/#54/#55.

## Атаки и реакции

- **STEAM.** Три короткие паровые дуги формируют импульс от дула к captured primary. Дуги расходятся в полёте .22 с, затем скручиваются и рассеиваются у места попадания .18 с. При реакции появляется расширяющаяся тонкая волна с клубами у captured primary и отдельные маленькие завитки на подтверждённых попаданиях.
- **STORM.** Разряд зарождается у дула, за .14 с проходит отдельными ломанными сегментами к captured primary и за .16 с распадается на искры. Реакция показывает отдельный первичный сигнал и короткие электрические дуги по сохранённым hops, с последовательным раскрытием и затуханием. Дополнительных попаданий нет — дополнительные дуги не появляются.
- Direct запускается только от `AttackEvent`, включая lethal, подавление реакции cooldown и отсутствие соседей. В момент потребления фиксируются башня, уровень, tick, ID и положение цели. Источник берётся из существующего bounded ShotSource текущего уровня или центра клетки +.85 м, цель — из captured X/Z с высотой .60 м. Recapture по живому врагу отсутствует.
- Reaction получает событие с kind и trigger, а попадания связывает через `ReactionPresentationState.Matches`. Steam включает primary в лимит 4. Storm хранит только до 2 дополнительных целей в порядке HitIndex; каждый сегмент использует свой FromPosition и TargetPosition. Размер Steam берётся из RadiusOrHopRange, форма дуги Storm ограничена этим параметром. Вспышки отдельных целей требуют AppliedHpLoss > 0, включая частичное lethal попадание; отдельный primary cue обозначает саму реакцию. Оттенок причины помогает различать Intrinsic и StatusPair, существующие объяснения и приоритеты берутся из прежнего consumer.

## Бюджет и жизненный цикл

Direct pool 8 × 4 LineRenderer, один существующий shared ReactionEffect material через PropertyBlock, first-free/oldest eviction. Переиспользуется существующий ring pool реакций, 24 × 5 LineRenderer и 4 damage slots на view, циклический overwrite. Максимум 16 vertices на direct линию и 32 на линию реакции. Runtime-эффекты берут только ограниченные буферы геометрии. При expiry/overwrite/reset очищаются origin/event/level/positions, impacts, геометрия, цвет и lifetime. Никаких новых материалов, physics callbacks или gameplay consumers.

Один activeDelta из BattleSceneController, включая freeze при Pause/background и пропуск первого resumed delta. Direct/reaction могут завершиться в пределах .40/.50 с после Sell/Upgrade/lethal/leak. Reset/Continue/Retry/NewGame/menu/unload очищают effects, даже если ID повторяются. Порядок ability/reaction и finally cleanup сохраняется. Основные риски — потеря captured endpoints при lethal, смешивание hops в одном tick, визуальное перекрытие HP/status и вытеснение старой косметики при saturation.

## Regression и evidence

| ID | Обязательная проверка |
|---|---|
| VFX1C-01 | STEAM/STORM L1/L2/L3, authoritative anchor/fallback, direct при suppressed/no-neighbor/lethal, captured geometry после Sell/Upgrade. |
| VFX1C-02 | Steam/Storm × Intrinsic/StatusPair, лимиты, AppliedHpLoss, lethal, exact key, shuffled HitIndex, отсутствие фантомных hops. |
| VFX1C-03 | Несколько реакций/tick и Steps/frame, finally/ability order, clocks и freeze фактических vertices, cap/reuse/expiry/Continue. |
| VFX1C-04 | Прежние четыре атаки, status priorities/inspector и ART5. Fresh whole FLAME/GALE before/after байты равны, оба production Victory8 с REG214 coverage. |
| VFX1C-05 | Полный static/Edit/Play, compile, builder и refs/GUID, carry-in SHA и restoration, actual rendering inputs. |
| VFX1C-06 | Непрерывное production Update Editor видео с timeScale 1/captureFramerate 0 и file Continue, Pause/Resume. Same-event launch/intermediate/impact PNG с видимыми пикселями и source SHA; отдельные подписанные earned test-clock/synthetic редкие случаи, L3/dense/босс/status/inspector. |
| VFX1C-07 | Read-only Git/source, protected/artifact, suite, replay и media verifier с anchor, ancestry, runtime blobs и малыми controls; PR и независимое code/visual review. |
