# P2.4f revision3 — автономный игровой анализ

Основание: новое решение владельца19.11.2026 в issue40, reviewed head a5faca3. Scope: локальный HP новой миссии; armor5, speed1, leak20, reward50, spawns, towers, ability и экономика фиксированы. REG214 и готовая trace/file/menu/rollback matrix сохраняются. Риск: слишком малая угроза босса, зависимость от точного cast, несовместимые saves.

Первый эксперимент: ровно16 full public-NewGame replays — четыре revision2 scripts при HP1000/700/650/600. Definition задаётся до NewGame, state/DTO не редактируются. Для всех сохраняются full accounting/traces, per-wave HP/Gold/cells, boss origins и event timings. Выбирается максимальный HP с qualifying Victory каждого старта. Затем16 дополнительных replays (четыре scripts × cast ±15/30ticks), центр исходный; ошибки cast сохраняются как фактический результат. При необходимости следующие узкие гипотезы публикуются до запуска.

Оценка: время жизни босса, запас до matched no-cast leak, состояние базы/экономики, сборки/уровни и сравнение Stone. HP1000/5 control/history неизменны. Новая production версия получает явный content ID, old saves продолжают1000/5; no-cast8 legal leak20 сохраняется.

Acceptance: winning routes каждого старта с ability AppliedHpLoss/kill, boss kill и всеми4positive Steam/Storm pair/intrinsic; два full identical traces, вся существующая file/menu/failed-save/Retry/Combat7–8 matrix на победных inputs, реальные wide/narrow PlayMode кадры/Console. Static, полные Edit/Play/graphical nonempty PASS без skipped; history/source/input/report/raw trace/media hashes, diff --check и независимое review. No self-merge/release/TestFlight; G2/device/ART отдельно.

## Гипотеза2 после первой matrix

HP600 — максимальный первый candidate с winning coverage обоих starts. Но F→T→G/G→T→F поздние cast8 не убивают Shade, поэтому не подходят готовому обязательному Combat8 rollback. Публикуется до запуска: максимум16 full-NewGame маршрутов (две histories × skip cast6 yes/no × original/early kill cast7 × first8 threshold100/200). До7 prefix/армия без изменений; cast8 при готовности по Shade cluster, следующий — по живому boss. Только public commands; сохранить все фактические outcomes/receipts и победы до выбора. Выбор: Victory+4cells+ability kill+positive reaction при живом Combat7 и8. Затем16 timing replays выбранных routes (оба ±15/30 для первого8 и boss8), без новых material search. Если недостаточно — следующая ограниченная гипотеза отдельно.

Гипотеза2 дала FLAME winning interrupt route (skip6/cast7at0/cast8at168,768); GALE ready7=204 не меняется от skip6, поскольку cast6 отсутствовал. Следующая строго локальная поправка: восемь GALE full replays, вместо skip6 убрать cast5 и варьировать original/early7/threshold100/200. Имена артефактов routes-gale-v2, прежние16 сохраняются. Проверить также вклад только раннего cast8 отдельно от boss cast; это не новый balance parameter search.

Окончательный prefix повторяется в двух full runs с cast7 после первого Step (не boundary0), чтобы UI/rollback имели явный advancing Combat. Дополнительные8 spatial replays: boss center ±0.5 по каждой оси обоих winning inputs. Готовый v2 no-cast leak control: сохранённый150/20 FLAME prefix, legal Sell всех tower вPreparation8, безcast8 (leak20 от живой базы); прежний1000/5 no-cast control отдельно сохраняется наv1. При600 прежняя Stone defense безcast8 сама убивает boss; не выдавать её за Defeat.

Независимое source review455a2ca выявило authoring gap: validator проверяетv2path, но validv1payload поэтомуpath проходитCreateSimulation. Новая meaningful regression сначала RED, затем exactV2 production ID guard дляMainMenu/Battle иповторfullgates. Runtime/saves/balance не меняются.
