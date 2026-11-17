# P2.3d — native EightWave, выбор и проверенный UI

База — `2a9da21de5f175cf8bf0fcd91064acc385be8d39`, merge PR #28. Изолированный worktree `Veilward-p23d`, ветка `codex/p2.3d-native-ui`. Дата проверки: 2026-11-17, Asia/Novosibirsk. Unity **6000.3.28f1 (8f082d2fe476)**, macOS arm64. Исходный implementation commit `dabc9cc`; финальный source `18874fb` после fixes. Полный проверенный source SHA и SHA256 отчётов — [gates.json](gates.json). Последующий evidence/docs commit не меняет runtime/tests.

## VERIFIED

- Public EightWave New Game создаёт native `eight-wave-unlocks-v1`, 150 Gold / 20 HP, mandatory unselected FLAME/GALE. Serialized rules/options/milestones и восемь волн строго проверяются; malformed/missing native metadata отклоняется без all-open fallback. TestWave остаётся shared combat source. GUID/reference audit и сравнение combat data — в gates.json.
- ElementChoiceController хранит preview с ожидаемым pending ID; options идут в canonical order, только закрытые после3/6. Tap/Cancel не меняют Domain; Confirm проходит через ChooseElement и same-phase save, повтор/stale rejected. Межволновой Confirm остаётся WaveComplete, Next → Preparation4/7, отдельный Start → Combat.
- Все шесть каталоговых карточек показывают цену/статус и missing elements. Locked STEAM при FLAME требует TIDE, STORM — TIDE + GALE; описание read-only, world model/range/highlight выключены, покупка отклоняется Domain. Unlocked insufficient Gold объясняется отдельно. После второго открытия нет обещания будущей награды. Native parameterless hidden test-tower path запрещён.
- Единый pause/modal guard защищает public gameplay API, placement/inspector/Sell/choice callbacks и реальные onClick.Invoke. Choice владеет собственными Confirm/Cancel, foreign modals не получают его права. Pause/background очищают preview/selection, сохраняют pending/credits и freeze; Resume подавляет текущий world pointer и не делает catch-up/autoconfirm.
- Ошибка сохранения видна; Retry Save вызывает только SaveCurrentState. Успешная live choice остаётся при IO error. Сохранён P2.3c retry-safe Start/Next: eligibility до filesystem, переход только после успешного safe save. Новые UI tests плюс прежние native IO-fault/rollback tests.
- MainMenu New Game/Continue проходит с обоими стартами, pending/confirmed, native army и legacy schema2 Steam/Storm army. Legacy имеет явную отметку прежних правил; без фиктивных locks/backfill. Preview не сериализуется; повтор Continue не выдаёт бонус/credit.
- Builder/validator создают и проверяют choice, Retry, safe area и ссылки. Layout сохраняет принятый uGUI Text/Button; новые dependencies/packages не добавлены. Historical all-open factory **internal**, старые combat timelines/economy/reaction/rollback assertions сохранены. Они не являются native replay evidence.

## Полные gates

Из корня worktree:

```sh
./Scripts/run-edit-mode-tests.sh
./Scripts/run-play-mode-tests.sh
./Scripts/run-static-checks.sh
git diff --check
```

EditMode **741/741**, PlayMode **38/38**, static self-tests **38/38**, project checks PASS; финальные totals и exact source SHA зафиксированы в [gates.json](gates.json). Batch backend: стандартный Hub executable, `-batchmode -nographics -buildTarget iOS`, Editor Mono; full non-empty NUnit XML, failed/skipped/inconclusive = 0. Это compile и Editor tests, не iOS player build. XML скопированы до static self-tests, которые используют свои temporary fake reports.

RED evidence: [initial](red-initial.xml.gz) **717/718** — старый content ID; [UI](red-ui.xml.gz) **717/727** — отсутствующий choice controller; [safe area](red-safe-area.xml.gz) **737/738** — отсутствующая HUD safe-area wiring. Промежуточный первый activation run 649/727 отражал устаревшие all-open fixtures; после переноса контролей и исправления guards — 738/738 EditMode и 37/37 PlayMode. Assertions/golden files не переписаны. Historical config hash P2.2f считается по прежнему wrapper projection: удалены только новая progression metadata и новый content ID; полный raw/combat before/after audit отдельно в gates.json.

Первый PlayMode activation run 35/37: два старых fixtures запускали Start при открытой tower panel. Helpers теперь явно закрывают свою selection; economy/combat expectations прежние. Все новые production UI сценарии в этом run прошли. Один resident Editor run во время подготовки review regressions выполнил ещё загруженные 738 tests; он **не считается** прогоном новых тестов. Editor закрыт, review RED/GREEN и финальные suites получены новым batch запуском.

## Review и исправления

[Независимый read-only review](review.md) `2a9da21 → dabc9cc`: 0 Critical, 2 Important, 0 Minor. Добавлены failing regressions на Next с выбранной башней (обычная WaveComplete и после milestone Confirm) и создание отсутствующего EightWave asset. Исправления: Next закрывает только свою selection после owner guard; create branch инициализирует native ID/settings, existing malformed metadata по-прежнему rejected. Missing-asset test восстанавливает originals/мета и проверяет bytes. [Review RED](red-review.xml.gz) **738/741**: два отказа Next и создание missing asset; финальный EditMode **741/741**, PlayMode — gates.json. Повторное независимое approval не заявляется; PR review/approval владельца ожидается, self-merge/release не выполняется.

## Реальные Play Mode кадры

[qa.jsonl](qa.jsonl) — точные Unity Pipeline команды и результаты, [fixture source](qa-fixtures.cs.txt), [Console audit](console-audit.json). Captures использовали `capture game_view --source screen`, читающий **реальный composited Game View render target** с overlay UI. Для 1920×1080 кадры экспортированы 1600×900; tablet и wide сохранены 1:1. Истинный размер подтверждён `PlayModeView.GetMainPlayModeViewTargetSize`: **1920×1080**, **1440×1080**, **2340×1080**. `Screen.width` внутри script eval зависит от текущего Editor GUI context и возвращал1641×1007; его не выдаём за Game View target. FrameCount в qa.jsonl меняется между командами: игра выполнялась, captures не являются Editor-only render/assertions. Начальная focus-loss Pause явно снята; неверно названный первый пробный кадр удалён, финальные файлы перечислены ниже.

| Кадр | Сценарий |
|---|---|
| [01](01-start.png) | Реальное меню New Game: FLAME/GALE, preview null, Confirm disabled, 150 Gold, tick0. |
| [02](02-locked-steam.png), [03](03-locked-storm.png) | Реальный старт FLAME; locked причины одна/две стихии, все шесть цен, без world preview/покупки. |
| [04](04-tablet-locked-storm.png), [05](05-wide-locked-storm.png) | Те же реальные cards/details, actual Game View1440×1080 и2340×1080. Текст читается, детали/кнопки/summary не пересекаются. |
| [06](06-synthetic-pending3.png), [07](07-synthetic-pause.png), [08](08-synthetic-resume-unselected.png) | **Synthetic safe snapshot** WaveComplete3; реальные контроллеры/Domain/UI. Pause очищает preview, Resume возвращает pending без preselect. |
| [09](09-synthetic-confirmed3-wavecomplete.png), [10](10-synthetic-continue-confirmed3.png), [11](11-synthetic-preparation4.png) | **Synthetic boundary3**: button Confirm TIDE остаётся WaveComplete, real MainMenu Continue confirmed, отдельный Next → Preparation4. |
| [12](12-synthetic-native-army-continue.png) | **Synthetic boundary4**, реальная покупка STEAM за110 после FLAME+TIDE, настоящий Continue сохраняет tower и40 Gold. |
| [13](13-synthetic-pending6.png), [14](14-synthetic-continue-pending6.png), [15](15-synthetic-preparation7.png) | **Synthetic safe snapshot6**; canonical закрытые GALE/STONE, настоящий Continue pending, Confirm GALE и Next → Preparation7, новых выборов не обещается. |
| [16](16-synthetic-legacy-army-continue.png) | **Synthetic schema2 legacy army**, fixture Gold500 → Steam+Storm →280. Real menu Continue восстанавливает обе башни/all-open/0 credits, явная отметка старых правил. Это не natural economy доказательство. |

На этих трёх Game View aspect ratios визуально проверены цены/missing strings, карточки, Confirm/Cancel, Pause/Resume, HUD/summary и отсутствие наложений. Safe-area insets дополнительно проверены automated layout test; это не физический notch/device test. Кадры01–15 сняты на `dabc9cc`; финальная укороченная legacy-подпись переснята на `18874fb` (кадр16). Review fixes Next/builder не меняют остальные панели. Console audit visual scenario после очистки: **0 errors / 0 warnings**, compilationFailed=false. Expected IO fault logs полных suites не смешиваются с этим визуальным audit.

## NOT VERIFIED / вне scope

- Natural native EightWave replay, победа/проходимость/production reaction proc coverage с обоими стартами — **P2.3e**. Synthetic snapshots и historical combat controls этого не доказывают.
- Physical iPhone/touch/notch/safe area/background/force quit, IL2CPP/Metal player, memory/allocations/frame time/thermal, device QA/G2, signing/TestFlight, hosted Unity CI, внешние playtests/balance approval не выполнены.
- ART approval и готовый арт первого среза, P2.4, P2.5, кампания/карты/остальные гибриды не закрываются. Packages, баланс и art assets в diff не меняются.

## Review fix: инспектор и world tap

После review пользователя исправлены передача обычного выбора построенной башни инспектору и переключение инспектора на другого врага через world tap. Latest проверенный source `aed5f68`: PlayMode **41/41**, EditMode **741/741**, static **38/38** + project checks PASS; RED39/41, новые кадры и полный SHA в [follow-up evidence](inspector-world-tap/validation.md). Root reports/gates выше сохраняют исходную проверку18874fb; follow-up является актуальной проверкой PR после input fix. Ограничения device/P2.3e/G2 сохраняются.
