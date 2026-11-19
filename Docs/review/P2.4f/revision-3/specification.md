# P2.4f revision3 — автономный игровой анализ

Основание: новое решение владельца19.11.2026 в issue40, reviewed head a5faca3. Scope: локальный HP новой миссии; armor5, speed1, leak20, reward50, spawns, towers, ability и экономика фиксированы. REG214 и готовая trace/file/menu/rollback matrix сохраняются. Риск: слишком малая угроза босса, зависимость от точного cast, несовместимые saves.

Первый эксперимент: ровно16 full public-NewGame replays — четыре revision2 scripts при HP1000/700/650/600. Definition задаётся до NewGame, state/DTO не редактируются. Для всех сохраняются full accounting/traces, per-wave HP/Gold/cells, boss origins и event timings. Выбирается максимальный HP с qualifying Victory каждого старта. Затем16 дополнительных replays (четыре scripts × cast ±15/30ticks), центр исходный; ошибки cast сохраняются как фактический результат. При необходимости следующие узкие гипотезы публикуются до запуска.

Оценка: время жизни босса, запас до matched no-cast leak, состояние базы/экономики, сборки/уровни и сравнение Stone. HP1000/5 control/history неизменны. Новая production версия получает явный content ID, old saves продолжают1000/5; no-cast8 legal leak20 сохраняется.

Acceptance: winning routes каждого старта с ability AppliedHpLoss/kill, boss kill и всеми4positive Steam/Storm pair/intrinsic; два full identical traces, вся существующая file/menu/failed-save/Retry/Combat7–8 matrix на победных inputs, реальные wide/narrow PlayMode кадры/Console. Static, полные Edit/Play/graphical nonempty PASS без skipped; history/source/input/report/raw trace/media hashes, diff --check и независимое review. No self-merge/release/TestFlight; G2/device/ART отдельно.
