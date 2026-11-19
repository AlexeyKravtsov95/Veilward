# QA.1 revision2 — P2 log symlink и Editor/support diagnostics

Продолжение Draft PR71/Refs42 от head7af99ca, main de8b9b58; исходное задание/контракт и runtime/content600/5/schema4 сохранены. Нового balance/art/packages scope нет.

Acceptance:

- QA1-08: ignored last-export.log symlink к tracked source или внешнему файлу не изменяет target bytes и original git status. Публикация лога атомарно заменяет directory entry; failure Unity с созданным логом не меняет target/предыдущий успешный export.
- QA1-09: preflight различает app-bundle и adjacent PlaybackEngines layout, включая resolved executable. На этой машине CLI/Hub показывает Installed iOS6000.3.28f1 arm64, default executable тот же, UNITY_EDITOR_PATH unset. Реальный модуль найден в adjacent version directory, прежняя проверка app-bundle-only ошибочна.
- QA1-10: фактическая capability нужного Editor проверяется BuildPipeline.IsBuildTargetSupported(iOS,iOS); затем два настоящих exports и Xcode/install/launch/QA1-07 по доступности. Directory/Hub metadata не заменяют capability/build evidence. Не переустанавливать module на основании старого вывода.
- Tooling RED→GREEN, static38/diff-check, новый source/manifest/clean-checkout verifier и независимый review. Старые reports/source audits/history не переписываются. C# не меняется; старые host1110/60 сохраняют tested sourceab9c17a с comparison к новой source, не выдаются за новый run.

Риски/системы: Python build orchestration, локальный выбор Editor, filesystem atomicity, signing/device environment. Device acceptance остаётся NOT VERIFIED до фактического полного smoke; no self-merge/release/TestFlight. Private paths/IDs/credentials остаются в ignored локальных raw files.
