# QA.1 — локальный iPhone build

Статус: SPECIFIED; Refs #42. Основа: main `de8b9b58f26ca86bfbc352eb214eaee7f7a6992c` (PR70, reviewed head4c09210).

Scope: воспроизводимый unsigned Unity export, локальный Xcode development build/install/launch и короткий smoke текущего slice. Runtime, баланс, арт, packages, исторические режимы и schema4 не меняются. Полный acceptance требует физического iPhone; отсутствие support/signing означает частичный BLOCKED, issue42 остаётся open. QA.2/#53, QA.3/#54, ART/#41/#43 и TestFlight отдельно.

Существующий контракт: Unity6000.3.28f1/8f082d2fe476, URP17.3.0, IL2CPP/ARM64, iOS17+, landscape; identifier `com.veilward.spiritdefense`, version0.1.0/build0, team/profile пустые. Graphics API Metal, automatic selection включён; exporter временно закрепляет только Metal. Enabled scenes: Bootstrap→MainMenu→Battle. MainMenu/Battle ссылаются на EightWaveAbilityBossV2: `eight-wave-ability-boss-v2`, boss600/armor5, historical v1/native/all-open и schema4 сохраняются.

Acceptance:

1. Явные version/build number; повторяемый путь `Builds/iOS/<version>-<build>/Xcode`; sourceSHA и content в manifest. Это воспроизводимый процесс, не обещание побайтово одинакового Xcode binary.
2. Закрытый Editor, чистый committed checkout, установленный iOS support. Export в новый staging; старый успешный output сохраняется при ошибке. Любой failure/отсутствующий Xcode project/receipt/source drift даёт nonzero. Ни builder, ни scene regeneration не запускаются.
3. BuildPipeline использует IL2CPP/ARM64/device SDK/Metal и точный список enabled scenes; временные PlayerSettings восстанавливаются. Signing/team не подставляются и credentials не добавляются в git. Никакого mandatory signing CI.
4. Реальные Unity export→Xcode build→install→launch на iPhone, smoke обоих стартов и evidence model/OS/sourceSHA/logs/screenshots. Editor/simulator/старые кадры этого не заменяют.
5. Static38, tooling regressions, compile/EditMode/PlayMode если доступны, diff check; tracked evidence доставляется из чистого checkout; независимый review до Ready for Review.

Риски: отсутствующий Unity iOS module, неподтверждённая совместимость Xcode27.1, локальная лицензия, team/provisioning, auto-generated source changes при импорте. Export локален и не выполняет signing. Проверка drift сигнализирует о любом изменении tracked bytes и не сбрасывает пользовательские файлы.

Regression scenarios (сначала спецификация, затем реализация):

- QA1-01: неверные version/build отклоняются до Unity; нет output/source mutation.
- QA1-02: отсутствующий iOS support, dirty checkout, resident descriptor, небезопасный/symlink output отклоняются nonzero.
- QA1-03: Unity failure или exit0 без project/receipt не заменяет предыдущий успешный export.
- QA1-04: два успешных exports заменяют только собственный ignored output, сохраняют Assets/ProjectSettings/Packages и .meta/GUID bytes. Mock runner проверяет оркестрацию, реальный repeat остаётся отдельным gate.
- QA1-05: tracked drift после Unity делает run FAIL; неверный receipt/sourceSHA/version/content не принимается.
- QA1-06: Editor regression проверяет сохранённый scene/content contract и отказ при неверном version/build; host tests не доказывают IL2CPP.
- QA1-07: physical smoke: New Game→FLAME, отдельно New Game→GALE; preview/Cancel/Confirm, placement, Start Wave/progress, ability preview/Cancel/Confirm, Pause/Resume, safe checkpoint→terminate/relaunch→Continue без повторной награды. Записать Gold/HP/wave до/после. Полная lifecycle/touch/performance matrix остаётся QA.2/QA.3.
