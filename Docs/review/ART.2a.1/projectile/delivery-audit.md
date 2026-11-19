# Revision 2 — clean-checkout delivery audit

VERIFIED: source/evidence commit `c3dea66ca9d7fc210933c1607bd51be228b88d38`, pushed в Draft PR74. Новый verifier на отдельном clean checkout: **398 protected / 41 delivery PASS**. Checkout чист до и после проверок; Unity/Library в clone не запускались.

Protected entries сверены с исходными Git blobs двух refs: merge72 `7783dd1bae83784b78f67ebe11108b083d375c82` и baseline `ba87f0fe9c396f8ee084659c48949d62cedf0998`. Весь прежний ART.2a.1 packet, включая его manifest/reports/video, сохранён. Новый source/media/report сверяется прямым SHA256. Markdown и сам verifier исключены из self-hash; проверяется tracked delivery. После документирующего commit verifier повторяется на новом чистом HEAD; runtime/media остаются байт-в-байт c3dea66.

## Команды и положительный результат

```sh
git clone --shared --single-branch --branch codex/art-2a-1-readability /Users/alexeykravtsov/.codex/worktrees/art-2a-1-readability/Veilward /tmp/veilward73p-clean
git -C /tmp/veilward73p-clean switch --detach c3dea66
cd /tmp/veilward73p-clean
git status --porcelain=v1
python3 Docs/review/ART.2a.1/projectile/verify_delivery.py
git status --porcelain=v1
```

```text
protected 398, delivery 41; timeline 8 arrivals: 1 lethal/7 nonlethal
PASS: tracked source/media/base blobs, full suites, moving head and delayed authoritative hit/death timeline
```

Проверены полное декодирование MP4 и параметры1280×720/30fps/240frames, PNG1600×900, advancing tick0→239/8attacks, оба полных непустых suites и обязательные новые FullName,240unique frame records. На всех8атаках6различных flight positions,5impact ticks,arrival+6,correct captured target/ID и lethal/nonlethal lifecycle.

## Negative controls

В этом же временном clone последовательно внесены нарушения. Каждый файл/manifest восстановлен из сохранённых bytes в `finally`, после каждого control; финальный verifier повторно PASS и `git status --porcelain=v1` пуст. Все пять запусков получили exit1:

| Изменение только в clone | Ожидаемый и полученный отказ |
|---|---|
| Дописан comment в FlameStrikeView.cs | SHA256 mismatch |
| Дописан comment в protected Domain/AbilityCastEvent.cs | SHA256 и base blob mismatch |
| Удалён dense-1.png | missing/untracked capture |
| PlayMode.json заменён фактическим ранним PlayMode-first.json66/66; соответствующий новый SHA обновлён в clone manifest | new projectile regressions missing: отсутствует required HP pixel test |
| Первый flight frame объявлен impact; соответствующий SHA JSONL обновлён в clone manifest | timeline assertion: flight6/impact5 нарушено |

Два последних control проверяют семантику suite/timeline даже при совпадающем файловом SHA, а не только наличие файла. Исторический manifest не изменялся ни в task worktree, ни для нового PASS.

[Hosted Quality Gates source commit — success](https://github.com/AlexeyKravtsov95/Veilward/actions/runs/44525929403). Это hosted static gate; actual Unity1110/67 suites выполнены локально в task Editor, а не выданы за hosted Unity run.

NOT VERIFIED: human continuous-motion approval и физический iPhone. Hash/media/timeline PASS не является художественной приёмкой. Final independent audit указан в [review](review.md).
