# VFX.1a — production FLAME / #90

База: main 45efa5676ac5c27ee70d4a13b1fbe2622d3f82f6 (merge89). Существующий checkout/Library; последовательные Unity runs, без worktree/clone/iPhone.

## Спецификация до реализации

Только FLAME меняет generic beam на движущийся fireball: горячее ядро, компактная голова, хвост ≤.48m, source flash, краткий impact и четыре уголька. Переиспользуется принятый FlameStrikeView: .2s полёта + .167s затухания, 13 renderers/slots на view, два shared material assets FlameImpulse. Reference snapshot ring/задержка HP в production не переносится. HP/Burn/Hit/Death/rewards происходят в исходный Domain tick; arrival — запоздалый визуальный отклик, в том числе на уже исчезнувшую lethal-цель.

Каждый consumed AttackEvent с authoritative TowerId/definition flame захватывает level, Tick, TargetEnemyId/TargetPosition/WasLethal и source до finally clear четырёх streams. Source: authored ShotSource правильного уровня, finite и в пределах 2 cell sizes от Domain center; иначе прежний center+.85m fallback. После capture нет ссылок на tower/enemy. Несколько Steps/frame и command consumers сохраняют прежний порядок; повторное consumption пустых streams ничего не воспроизводит. Другие пять семейств/legacy используют прежний beam.

Prewarm: 8 views, максимум104 renderers/slots, 2 shared materials, без Instantiate/AddComponent/Destroy/material instances на обычный shot/Advance. Сначала свободный slot, при saturation — самый старый shot; без очереди и переноса damage. Срок .367s меньше FLAME интервала атаки; 8 слотов рассчитаны на до8 синхронных FLAME, фактическая cadence/насыщение дополнительно фиксируются в evidence. Это явный presentation cap, не обещание показать каждую атаку при произвольной плотности.

Advance использует существующий activeDelta после pause/skip-next-delta guards, даже вне Combat для terminal drain. Reset/успешный restore/Continue/Retry/restart/menu/unload очищают head/trail/flash/impact/IDs/timing; pool reuse не меняет shared materials. Повторная инициализация не создаёт второй FLAME pool/consumer. Ни Domain/config/balance/save/schema/content, ни ART3–5/camera/quality/light не меняются. Builder и saved Battle получают два явных material refs; прежние fixture fallback refs допустимы.

## Acceptance / regression scenarios

| ID | Проверка |
|---|---|
| VFX1A-01 | FLAME L1/L2/L3 routing по authoritative ID/definition/level, authored source и bounded fallback, один shot/event, FLAME без beam; другие пять семейств и legacy прежние. |
| VFX1A-02 | Intermediate head motion и короткий хвост; captured target после movement/lethal/removal; исходные HP/Burn/Hit/Death/reward/cooldown не меняются при arrival. |
| VFX1A-03 | Несколько Steps/frame, command-triggered consumption, повторный consumer; все streams finally clear и ART5 feedback сохранены. |
| VFX1A-04 | Prewarm/repeated initialization/capacity/reuse/oldest overflow; geometry/metadata cleared, object/material counts стабильны; нет очереди. |
| VFX1A-05 | Настоящий Update: pause/background freeze всей geometry, resume skip delta без catch-up, flight→impact→expiry; reset/restore/Continue/menu/unload без ghosts. |
| VFX1A-06 | Полные FLAME/GALE production traces до/после идентичны. Earned public NewGame/place/upgrade/Combat/ability/file Continue/Grid rollback/Sell; никаких setters ресурсов/HP. |
| VFX1A-07 | Неизменная Battle camera/ART4 quality/light: L1–L3 launch/intermediate/arrival PNG, одиночный/плотный бой, Shade/boss HP/status/Hit/Death. Непрерывное production Update видео, pause/resume и Continue cleanup; accelerated/synthetic отдельно. |
| VFX1A-08 | Новые static/full Edit/full Play/compile/meta/GUID/refs/diff, read-only strict source/protected/actual inputs/suites/replay/video verifier и малые controls; независимое code/visual review после PR. |

Риски: bounded overflow скрывает старый визуальный отклик; задержка картинки после мгновенного damage; stale model source при upgrade; ghost geometry после restore; ложное realtime evidence от test clock. Условия тестов и точные новые totals фиксируются отдельно. Device Deferred to prerelease QA #53/#54/#55. Parent47/остальные VFX/G2/merge/release отдельно; self-merge запрещён.
