# ART.2a — техническая проверка передачи

19.11.2026, Asia/Novosibirsk. Ветка `codex/art2a-owner-reference`, отдельный managed worktree `veilward-art2a/Veilward`. База — свежий `origin/main 0028981dc84f15bcdf8b5ad934584efcded56709`, PR #71 merged; спецификация записана первым коммитом `ef8d0f4`. Исходный checkout/параллельные worktrees не изменялись. Issue #33 задаёт ART gate, #41 — этот пакет, #43 — следующий отдельный device proof.

## VERIFIED

| Проверка | Команда / результат |
|---|---|
| Static | `./Scripts/run-static-checks.sh` → **38/38** self-tests, Unity version/package/`.meta`/GUID/asmdef PASS; [полный вывод](reports/static.txt) |
| Источники/визуальные входы | `python3 Docs/review/ART.2a/verify.py` → **125/125** файлов совпадают по SHA256 с ART.1.1 source `4b14e215969f8fd35dfb13be94862d875294ec34`; [manifest](provenance.json) |
| История | Та же команда → **79/79** файлов ART.1/ART.1.1 побайтно прежние, включая PNG/MP4/raw telemetry/Console/QA/roundtrip/review |
| Передача | Та же команда проверяет tracked package, все локальные Markdown ссылки, пять PNG/их telemetry и историческое advancing video/Console; [вывод из чистого checkout и отрицательных контролей](reports/package.txt) |
| Scope | `git diff --name-only 0028981` содержит только `Docs/review/ART.2a/`; `git diff --check 0028981` и `git diff --cached --check` PASS |

Manifest построен из Git bytes исторического source head, затем каждый SHA сравнен с файлами текущего worktree. Повторный verifier независимо читает исторические Git blobs и текущие files, требует tracked delivery. **125 визуальных входов**: все 8 editable `.blend` и source scripts, FBX/import `.meta`, ART prefabs, все Art материалы/палитра, saved ArtReference/camera/lighting, showcase/healthbar/builder/capture scripts, Assets/Settings, Graphics/Quality settings, ProjectVersion и package manifests/lock. Сохранённая сцена и ссылки не пересериализовывались; asset/reference audit ART.1.1 доступен как историческое evidence. Повторного Unity import audit здесь нет.

**Граница сравнения:** Domain/configs/остальной runtime с ART.1.1 исторически менялись в P2.3/P2.4; manifest не утверждает неизменность gameplay. Кадры и видео используются только как эталон сохранённых моделей, материалов, камеры и художественного вида эффекта. Они не подтверждают текущую mission v2 600/5, босса/способность, progression/schema4 или новые reactions. В этом PR gameplay/runtime/configs/scenes/packages не меняются.

## Среда и историческое evidence

Текущий audit: macOS **27.0.1 / 26A434**, Python **3.12.13**, Git **2.55.0**. Проект закреплён на Unity **6000.3.28f1 / 8f082d2fe476**, URP **17.3.0**; Unity/Blender для этого docs-only пакета не запускались.

Captures ART.1.1 сняты 16–17.11.2026 в Unity6000.3.28f1, active StandaloneOSX, Blender source5.3.0; точные команды/среда/ограничения — [исходный QA](../ART.1.1/qa.md). Пять основных кадров — 1600×900, ortho8.5. Общий combat — tick64/3атаки. Остальные четыре — статические Play Mode probes с явно синтетическими позициями/HP, не advancing combat. Дополнительные closeups ortho1.25 не являются основными кадрами.

Видео — 1280×720/8s, 240 advancing player-loop frames, tick0→239/8атак, offline-paced30fps. [Telemetry](../ART.1.1/video-capture.json), [исторический Console0errors/0warnings](../ART.1.1/console-audit.json). Исторические EditMode547/547, PlayMode30/30 и source roundtrip8/8 остаются результатами **ART.1.1**, а не текущими запусками ART.2a.

## NOT VERIFIED / BLOCKED

- **Owner ART approval не получено.** README содержит конкретные кадры/вопросы; полного acceptance #41 пока нет, PR использует `Refs #41`, issue остаётся open. Merge технического пакета не заменяет художественного решения.
- Полные EditMode/PlayMode, текущие compile/Console и source reexport в ART.2a **не запускались**: по #41 п.7 docs-only пакет не требует повторения gameplay suites. Это NOT RUN, не PASS/skip внутри suite. Новых моделей/import/runtime изменений нет.
- ART.2b/readability/окклюзия/ASTC/frame-time/memory/thermal на физическом iPhone не проверены. QA.1/PR71 подтверждает другой graybox smoke scope, не ART.2b. Сохраняются значительное перекрытие Shade за FLAME и overlap HP плотной группы.
- Готовый UI, production composition/другие модели MVP, human G2 и release не утверждаются. Нового коммерческого/лицензионного approval для прежних AI-assisted assets этот пакет не создаёт.
- Projects поле отдельно не изменялось/не подтверждено. Self-merge/release/TestFlight не выполнялись.

Независимое техническое review фиксируется отдельно в `review.md` после открытия PR; оно не является решением владельца по стилю.
