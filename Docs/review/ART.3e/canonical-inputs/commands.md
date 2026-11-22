# Команды P2 / 23.11.2026

Работа в том же checkout/ветке codex/art3e-steam-levels, исходный head88b8d7e/source9654133. Новых worktree/clone/project copies/Library/Unity caches нет. Четыре original файла всё время анализа/control оставались на месте и совпали с прежними SHA и внешним backup. В Docs скопированы только восемь маленьких input evidence files (canonical/carry), не проект.

```sh
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.3e/canonical-inputs/verify_inputs.py
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.3e/canonical-inputs/test_inputs.py
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.3e/verify_controls.py
PYTHONDONTWRITEBYTECODE=1 ./Scripts/run-static-checks.sh
git diff --check
git diff --cached --check
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.3e/verify_delivery.py
```

Python3.12 stdlib, без PyYAML/new packages; Unity CLI1.0.1 self-update check current. CLI сообщил AGENT_PLUGIN_AVAILABLE/setup codex advisory; setup/install не выполнялись, bundled Unity skill уже доступен. Для анализа открыты using-unity/unity-cli и его reference; Editor не открывался, Unity command/suites/compile/captures не запускались. Другие процессы Unity не управлялись. Native LoadSerializedFileAndForget намеренно не использован из-за delayed callbacks.

Начальные generator ошибки (PNG decoded as UTF8, неучтённый установленный universal-config17.0.3) исправлены до финального certificate; не считаются PASS. Первый controls run18/19 не прошёл: renderScale mutation не изменила строку0.8, так как искала1. Исправлен тест на реальное изменение + assertNotEqual; raw first failure сохранён, final19/19 PASS. Последующие изменения certificate/receipt проверены повторно.

Для финального полного verifier действует прежнее явное разрешение владельца на ровно четыре файла: existing external backup → Git bytes только4 → assert clean tracked → read-only full verifier → finally restore4 → SHA/change-set checks. Содержимое исходных файлов не расширяется/не коммитится. Исторические dirty-input media/reports и предыдущий controls report сохранены; новый current verifier только добавляет certificate checks, не ослабляет clean/source/artifact/protected/suite/video gates. Raw финального head verifier остаётся в ignored TestResults/ART3E, чтобы не создавать self-hashing loop.
