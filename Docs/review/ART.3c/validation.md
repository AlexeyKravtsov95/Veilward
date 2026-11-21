# ART3c — результаты и ограничения

Tested source **9871af6**, baseline **b2d31095238e8ce5e276ea1ba0a7741b8afee82f** (merge PR79). [Точные команды/окружение/неуспешные попытки](commands.md). Runtime delta — один galeLevels в прежнем FlameModelBinding + один Gale builder/wiring; Domain/Infrastructure/configs/schema4/content600/armor5/цены/скорости/волны/ability и принятые ART1/FLAME/TIDE/history bytes сохранены.

## VERIFIED

- Static **38/38**, project checks PASS; полный конечный **EditMode1161/1161** от source9871af6 через batch, полный **Metal PlayMode81/81** через resident Editor. Failed/skipped/inconclusive0. Raw unfiltered XML/JSON в reports/edit-mode.xml и play-mode.json; отдельный capture2/2 не подменяет full suites. Первый batchEdit1159/1161 и неудачные окружения сохранены отдельно.
- Compile up_to_date, errors0/warnings0. Natural capture/видео Console0 unexpected errors/warnings; final Console ground truth0errors/0warnings. Retained Pipeline buffer содержит25error/2warn entries полных suites (включая намеренно вызываемые IO/negative paths), эти исторические buffer counts не выдаются за новые ошибки capture. Console не очищалась агентом.
- Три editable originals и Unity imports: **552/1152/1740tri**, два palette material slots, source ground0/root identity/meters/footprint≤.94m/UV/normals. L2 middle projected width1.25855 против1.02552 L1 (+22.7%); L3 height2.40103 против1.78478 L2, плюс разомкнутая спираль. Это structural checks, художественная читаемость отдельно подтверждается actual screenshots/review.
- Реальный reexport3/3: imported geometry+UV+normals/submeshes/material remaps/GUID/localIDs/dependencies и prefab+scene references before==after. Source FBX timestamp headers возвращены к committed bytes; canonical readback также равен. Protected baseline bytes проверяет актуальный verifier.
- Single-owner F/T/G switches при одинаковых уровнях, legacy/missingrefs/unsupported levels/fallback, повторные Apply/Bind/Build, no tint/no graybox/no duplicates/deferred inactive cleanup, root/collider/selection/range/scale. Прежние F/T tests сохранены; заменены только GALE-graybox assertions. Catalog preview оставляет текущий graybox preview и корректную типовую окраску; purchased GALE использует authored palette.
- Actual PlayMode singles/lineup/три семейства и9 front/side/behind+dense12. Synthetic assigned positions/HP и copied actual Gust snapshots подписаны. Production Shade HP anchor/status slots без изменений, HP/head/Gust читаемы. Public catalog/preview/range/Inspect проверены, preview не покупает и не меняет Gold16/Towers1.

## Законный earned flow

Fresh production MainMenu New Game/public commands, явно test-clock первых6волн с существующей FLAME→TIDE→GALE стратегией; после6 tick2589/HP16/kills41. Законная продажа армии оплачивает GALE TowerId6/cell(2,1). L1: Gold296/invested80 → Upgrade120 → L2Gold176/invested200 → Upgrade160 → L3Gold16/invested360. Sellrefund252 → Gold268. Ни live Gold/HP, ни checkpoint DTO не редактировались. Duplicate/MAX/repeated Sell отвергаются без изменения состояния. File/menu Continue повторён дважды на каждом уровне; полный safe snapshot (включая ability/progression/IDs/cooldowns) после interrupted Combat возвращается целиком. Restart пуст, активных model roots нет.

193-frame непрерывное **Editor** видео19.748s — отдельный byte-identical replay действительно заработанного L1 через isolated store и MainMenu Continue, **не новый fresh run**. Public UI Upgrade1→2→3 → advancing Combat/Gust → live Enemy46 Inspect (кадрHP62/100, readbackHP48/100/Gust40/70ticks) → Menu Continue/Preparation rollback → ConfirmSell. Safe live wire до/после rollback равен; L3 и sold files позволяют проверить арифметику. Source hash/origin/restore/readback — video-checkpoint-origin.json.

Exact stage PNG picks: L1frame28, L2frame48, L3доCombat68, Gust/Inspect117, rollback149, sold183; все source indices и hashes различны. L3 не извлечён из rollback stage. Video193PTS монотонны и повторяют реальные timestamps с maxошибкой0.557ms. Capture interval≈.1s с overhead; sparse кадры/1×clip не являются native display timing или performance benchmark. Используется уже установленный Pipeline composited GameView RT/encoder; игровых pixels/camera не перерисовывает вручную. Generic Battle beam остаётся до #47.

## Canonical source и воспроизводимость

Unity-generated serialization после suites сохранена audit/patch и возвращена точно к tested source (edit-restored/metal-restored/capture-restored). Это не runtime delta. Saved Battle/MainMenu/Shade/materials/URP и accepted family assets не входят в source diff; source bytes проверяются inventory и protected baseline. Packages/ProjectVersion прежние. [Актуальный verifier](verify_delivery.py) read-only и не запускает Unity. Исторические verifiers/evidence не переписаны.

Clean-checkout и negative-control результаты будут добавлены после фактического запуска; до этого данный gate не заявляется PASS.

## NOT VERIFIED / Device Deferred

**Physical iPhone Deferred to prerelease QA #53/#54/#55.** Новые phone binaries/native video/matched benchmark/iOS export/Xcode signing не запускались. Нет device performance/memory/GPU/thermal/touch/safearea/background PASS. Editor не заменяет device. Юридическое/коммерческое approval AI-assisted результата и художественная приёмка владельцем не заявлены агентом. Parent44 и STONE/STEAM/STORM, карта45/враги46/VFX47/UI48, G2 и release/TestFlight отдельно. Self-merge/release не выполняются.
