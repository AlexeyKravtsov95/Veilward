# ART4-08 — P2 воспроизводимости delivery verifier

Замечание владельца в [#45](https://github.com/AlexeyKravtsov95/Veilward/issues/45): на head6f23d9a основной audit требовал не только4actual rendering inputs, но и локальные TXT/дваpyc, чьи bytes не поставлены. Это ограничивало прежние полные PASS личным checkout. Старые receipts сохранены, но не являются доказательством воспроизводимости без личных3.

## Изменение контракта

- `verify_delivery.py` по умолчанию выполняет обязательные source/Git inventory/protected/artifact/strict suites/replay/geometry/roundtrip/actual66/exact4rendering/video/input checks. Личные3 не открываются и не требуются. Receipt сообщает `localPreservation.status=NOT_REQUESTED`.
- `--check-local-preservation` дополнительно проверяет все7исходных файлов по историческим SHA. Missing/changed ожидаемый файл —FAIL. Это только локальный audit, не prerequisite delivery и не требование получить личный TXT/pyc от владельца.
- Allowlist dirty/untracked paths остаётся прежним historical receipt. Любой неожиданный source/evidence path и committed inventory drift отклоняются; точные4rendering hashes обязательны в обоих режимах. Сохранённые пользовательские bytes не меняются.

```sh
# Обычный обязательный delivery audit, личные3 не нужны:
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.4/verify_delivery.py
# Опционально: только в исходном локальном окружении с семью файлами:
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.4/verify_delivery.py --check-local-preservation
# Малые fixtures, без project copy/Unity:
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.4/verify_controls.py
# Full entrypoint на текущем committed packet, private3 виртуально отсутствуют:
PYTHONDONTWRITEBYTECODE=1 python3 Docs/review/ART.4/verify_controls.py --delivery-entrypoint
```

Entry controls маскируют только наличие/read доступа к трём нерендеринговым local paths и их строки в`git status`. Все848source/3754protected/actual66/exact4/artifact/replay/roundtrip/suite/video checks используют настоящие repo/evidence inputs. Личные файлы не перемещаются и не заменяются. Дополнительные entry controls требуют FAIL вявном preservation mode при missing3 и изменённом первом private input; fixture controls проверяют каждый из трёх changed private files, rendering4missing/tamper и прежние source/protected/artifact/inventory negatives. Entry controls не требуют наличия этих личных файлов в reviewer checkout.

## Доступные exact4 rendering inputs

Обязательный режим ожидает те четыре фактических rendering файла, с которыми были получены старые Unity/media runs. Их raw bytes публично поставлены в`Docs/review/ART.3e/canonical-inputs/carry-in/`; SHA pinned в`CARRY` иactual-render-inputs. Это использование доступных точных bytes, **не** распространение старого ART3 lighting certificate на новую карту.

Если существующий reviewer checkout имеет canonical Git bytes четырёх runtime paths, приведённая подготовка временно устанавливает только4доступных counterpart, выполняет read-only verifier и возвращает exact исходные4bytes. Unity должен быть закрыт, записи — одним владельцем. Не создаётся worktree/clone/project copy. Неизвестные чужие bytes отклоняются **до первой записи**. Сам verifier файлов не меняет; личные3 не нужны и не публикуются.

```sh
PYTHONDONTWRITEBYTECODE=1 python3 - <<'PY'
import importlib.util, subprocess, sys
from pathlib import Path
p = Path('Docs/review/ART.4/verify_delivery.py')
spec = importlib.util.spec_from_file_location('art4', p)
v = importlib.util.module_from_spec(spec); spec.loader.exec_module(v)
head = v.tree('HEAD'); before = {}; actual = {}
for name, expected in v.CARRY.items():
    target = v.ROOT / name
    raw = (v.ROOT / 'Docs/review/ART.3e/canonical-inputs/carry-in' / target.name).read_bytes()
    v.require(v.sha(raw) == expected, 'Counterpart SHA mismatch: ' + name)
    original = target.read_bytes()
    v.require(v.sha(original) == expected or v.blob(original) == head[name], 'Unknown foreign bytes; do not overwrite: ' + name)
    before[name] = original; actual[name] = raw
try:
    for name, raw in actual.items():
        if before[name] != raw: (v.ROOT / name).write_bytes(raw)
    subprocess.run([sys.executable, str(p)], check=True)
finally:
    for name, original in before.items():
        target = v.ROOT / name
        v.require(target.read_bytes() in (actual[name], original), 'Unexpected concurrent change: ' + name)
        if target.read_bytes() != original: target.write_bytes(original)
    v.require(all((v.ROOT / name).read_bytes() == data for name, data in before.items()), 'Restoration mismatch')
PY
```

## Проверки этой ревизии

На committed **be12f5b** выполнены новые проверки:

| Проверка | Новый результат | Receipt |
|---|---|---|
| Static |38/38 PASS|[raw](static.txt)|
| Малые fixtures |14/14 PASS, без skipped|[raw](controls.txt)|
| Полный entrypoint controls |3/3 PASS: default безprivate3, explicit missing/changed FAIL|[raw и full-audit receipt](entrypoint-controls.txt)|
| Обязательный read-only delivery |PASS848source/3754protected/146artifacts, exact rendering4|[receipt](delivery.json)|
| Explicit local preservation |PASS7/7 в исходном local checkout|[receipt](local-preservation.json)|
| Старый entrypoint missing3 |3/3 ожидаемых отказа воспроизведены read-only|[receipt](reproduced-old-dependency.json)|
| Product source / исходные файлы |product delta0, original7 SHA unchanged|[scope](source-scope.json)|

Default receipt явно показывает NOT_REQUESTED для local preservation; optional-mode PASS7/7 не prerequisite independent delivery. После добавления этих receipts artifact count меняется, определяется manifest и последним read-only run. Self-hashing stdout заключительных запусков остаётся ignoredTestResults.

Только новые Static/controls/read-only verifier/preservation/re-review receipts размещаются здесь. Старые UnityEdit1233/Play88, compile, mesh reexport иmedia остаются прежними runs наtestedb10e8b8/media03af; новый Unity run, reexport, video или device-тест для verifier-only правки не выполняются.

Независимое re-review — [review](review.md). До него P2 не считается закрытым. Self-merge/release не выполняются.
