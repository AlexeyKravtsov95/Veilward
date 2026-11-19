# ART.2a — спецификация пакета для владельца

19.11.2026, Asia/Novosibirsk. Refs #41; исходный main `0028981dc84f15bcdf8b5ad934584efcded56709` (PR #71). Прочитаны AGENTS, README, GDD, TECH_DESIGN, ROADMAP, ProjectVersion, ART_PIPELINE, ART1_VISUAL_PROTOTYPE, ART1_1_VISUAL_REFINEMENT, ART.1.1 README/source README и issues #33/#41/#43.

## Scope и acceptance

Один документационный PR: компактный просмотр существующего ART.1.1 «парящего святилища», FLAME и Shade. Результат — README с 4–6 ключевыми реальными Unity кадрами, видео боя, палитрой/силуэтами/иерархией акцентов и конкретным перечнем решений владельца. UI описывается только как предлагаемое направление. Отдельно обозначаются оставшаяся окклюзия и ограничения плотной группы.

Пакет готов к передаче после проверки происхождения/целостности материалов и независимого технического review. Полный acceptance #41 требует явно записанного решения владельца по кадрам и правкам: без него только `Refs #41`, issue остаётся open. #43 — отдельный real-iPhone art proof; QA.1 его не заменяет.

## Затронутые системы и риски

Изменяются только новые документы/evidence в `Docs/review/ART.2a/`. Не изменяются Blender sources, FBX/importers, материалы, камера, сцены, runtime, content v2 600/5, schema4, saves, баланс, packages. Массовое производство и редизайн не входят. Риски: устаревшие кадры, смешение static probes с combat, преувеличение читаемости по closeup, трактовка технического PASS как ART approval. Для исторического боя явно указать, что он не является новым прогоном mission v2.

## Сценарии проверки до подготовки пакета

- ART2A-01: все восемь `.blend`, FBX, `.meta`, prefabs, палитра/материалы, saved ArtReference/camera, визуальные scripts и render settings побайтно совпадают с source head ART.1.1 `4b14e21`. Сохранить полный SHA256 manifest; при drift исторические кадры не выдавать за вид текущего ассета.
- ART2A-02: 5 различных PNG 1600×900 с ortho 8.5: общий настоящий combat, Shade light/dark, плотная группа, за FLAME. Capture telemetry определяет тип каждого кадра; static probes и HP 65% не описывать как настоящие попадания. Closeups ortho 1.25 не использовать как основные кадры.
- ART2A-03: существующее видео доступно вместе с frame/tick telemetry и Console audit; 240 advancing frames / tick 0→239 / 8 атак. Offline-paced 30 fps не считать измерением производительности.
- ART2A-04: historical ART.1/ART.1.1 файлы неизменны, все ссылки/картинки и manifest доступны из чистого Git checkout; modified scope строго ART.2a. Проверить static gates, `.meta`/GUID/package baseline и `git diff --check`.
- ART2A-05: README отделяет предлагаемую приёмку владельца от VERIFIED техники и NOT VERIFIED / BLOCKED ART/device. Независимый reviewer проверяет передачу; merge/release/TestFlight не выполняются.

Для docs-only пакета полные gameplay EditMode/PlayMode и Blender reexport повторно не запускаются согласно явному scope #41 п.7. При необходимости реального model/import изменения этот план пересматривается с соответствующими source roundtrip и Unity regressions.
