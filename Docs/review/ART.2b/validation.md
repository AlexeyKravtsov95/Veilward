# ART.2b — device proof, частичная приёмка

**Refs #43.** Source `7b753e3f8a44abdaca07cb9a3030ac8eac13c0ba`, art-reference0.1.0(44), content `legacy-single-wave-v1`. Это отдельная техническая ArtReference, не production integration #47. Product/art/budget approval остаётся за владельцем. Финальный пакет имеет manifest с SHA256 и read-only verifier для чистого checkout.

## VERIFIED

- Физический wired iPhone16Pro (`iPhone17,1`, A18Pro, Metal), iOS27.0.1/24A446. macOS27.0.1/26A434, Unity6000.3.28f1/8f082d2fe476, URP17.3.0, Xcode27.1/27A9275, iphoneos27.1. Реальный arm64 export, xcodebuild Debug, codesign verify, install, launch. Receipt signed app совпадает с run.json и tested SHA. Unity Development flag=false, frame timing stats=true; Unity Profiler не подключён.
- Static38/38, host exporter22/22, analyzer4/4, EditMode1111/1111, graphical PlayMode74/74. Полные Unity suites без failed/skipped/inconclusive. Regression scenarios/spec в Docs/ART2B_DEVICE_PROOF.md и Docs/REGRESSION.md. Полные sanitized case reports и commands приложены; failed attempts отдельно описаны, не засчитаны.
- Настоящие Play Mode PNG: normal/dense, source4141764 до невизуальной iOS activation fix. Device44: normal/front/side/behind/dense12, исходный camera/scale, HP0.48m, synthetic placement/HP явно подписаны. Front/side пересняты после подтверждённой готовности probe. Первый43 uninstrumented PNG — только исторический exploratory кадр.
- Нативное continuous video normal combat: [normal-native.mp4](device/normal-native.mp4),28.005s,2622×1206,1680frames,1×. Обрезаны Control Center и audio/metadata; H264 transcode, без resize/interpolation/time scaling.1680 исходных PTS совпадают после вычитания40s (максимальная ошибка7.11e−15s). Native raw SHA/processing в video-proof.json; raw сохранён локально. Видео снято отдельно от замеров.
- Frame analysis видео: nonlethal source flash≈0.25s→head0.317/0.383→arrival0.433→HP/impact0.45; lethal low HP5.18→source5.30→head5.38 при живой цели→arrival5.45 с исчезновением цели/HP→fade5.52→cleanup5.60. Раннее исчезновение в проверенном цикле не обнаружено. Это техническая проверка кадров, не художественное одобрение владельца.
- Реальная dense пауза: два native PNG с интервалом2.3815s идентичны по всем decoded pixels; Resume меняет VFX; Restart возвращает обычный бой без12 synthetic actors. Первый короткий remote click не сработал (device-pause.json=false), повторный held300ms сработал (device-held-pause.json=true). Это remote pointer/device lifecycle smoke, не human multi-touch test.
- За3 завершёнными measurement sessions unexpected gameplay exceptions в сохранённых console logs не обнаружены (scoped console-audit.json). Полный системный iOS log не анализировался.

## Замеры на устройстве

Каждый normal/behind/dense:5s warmup,3×30s;5400 samples, суммарно16200 interval и16200 positive GPU samples. Normal — реально advancing saved combat с ticks/attacks в каждом окне. Behind/dense — synthetic repeatable probes. Основные timing windows без screen sharing, native recording или xctrace; console attached и instrumentation overhead остаются. Raw windows/run/completion доставлены, независимый analyzer пересчитывает значения.

| Сценарий | interval median/p95/max, ms | GPU median/p95/max, ms | CPU frame p95/max, ms |
|---|---|---|---|
| normal |16.668 /16.911 /19.217|1.759 /2.220 /3.122|16.802 /17.101|
| behind |16.667 /16.851 /21.482|1.742 /2.186 /2.990|16.813 /21.504|
| dense12 |16.667 /16.852 /22.425|1.856 /2.347 /3.312|16.808 /22.410|

CPU frame содержит pacing/wait, не CPU execution cost. Interval не display-present deadline; p95 выше16.67ms, поэтому постоянные60fps не объявлены PASS. Данные относятся только к этим коротким окнам на одном устройстве.

| Сценарий | Unity allocated peak, MiB | Unity reserved peak, MiB | native footprint observed peak, MiB | native resident observed peak, MiB |
|---|---|---|---|---|
| normal |91.60|107.31|263.033|270.047|
| behind |91.84|107.61|263.830|308.000|
| dense12 |91.42|107.25|258.486|303.312|

Unity memory — из primary run. Native memory — отдельный20s Activity Monitor trace,21samples каждый; normal снят после реального dense Restart/cleanup. CSV извлечены exact executable/single PID из physical-device all-process trace (не Mac). Memory-real и physical footprint — разные метрики. Пики наблюдаемые, не lifetime peaks. Baseline/after каждого окна в measurements.json; короткий цикл не доказывает отсутствие долгосрочной утечки.

Runtime: landscape Screen2622×1206, safeArea(186,60,2250,1146), nominal refresh120Hz, target60/vsync0, quality0/Mobile_RPAsset; renderScale0.8000000119, effectiveMSAA1, main shadows1024/distance50/cascades1/soft=false. Camera pixel/scaledPixel2622×1206. Фактические internal GPU RT dimensions не измерены и из renderScale не выводятся.

## Предложение бюджетов — PENDING OWNER APPROVAL

Это предлагаемые guardrails для повторения данного microbenchmark на указанной модели. Они не утверждены и не являются production cap или гарантией для iOS17/base iPhone.

| Метрика | Предложение | Наблюдение |
|---|---|---|
| player-loop interval | p95≤18ms, max≤25ms | в пределах предложения |
| GPU frame | p95≤4ms, max≤5ms | в пределах предложения |
| Unity allocated / reserved |≤100 /≤120MiB | в пределах предложения |
| native physical footprint / resident |≤300 /≤350MiB | в пределах предложения |
| ordinary geometry/material slots | существующие ART_PIPELINE≤5000tri /≤2 | FLAME1232tri, Shade185tri; ассеты не менялись |
| shared palette | существующая128×64, configured ASTC4×4, Point/Clamp, mip off | source configuration проверена; actual runtime texture format/memory не измерены |

CPU execution, draw calls, overdraw и internal RT dimensions: бюджет по фактическим данным пока не предложен.10s Metal System Trace успешен, но2608 encoder rows не равны draw calls; единственный RT Unit Active — определение raytracing counter, не измерение overdraw; allocation table пуст. См. metal-capability.json.

## NOT VERIFIED / BLOCKED

- Художественное/product acceptance и утверждение бюджетов владельцем. Закрытие #43 преждевременно; PR остаётся draft/Refs.
- Continuous dense video: дополнительная запись заблокирована `cgWindowNotFound` у Device Hub после успешной normal recording. Dense readability/movement представлены native PNG sequences и Play Mode evidence; normal-speed continuous dense viewing не подтверждено.
- Первые CoreDevice screen-record (1001) и QuickTime capture попытки не работали; последующая native iOS recording normal успешна. Эти ошибки не скрыты.
- Actual GPU draw-call count, raster overdraw, internal render target dimensions; actual texture runtime format/memory. Проблема измерения не заменена синтетическими цифрами.
- Базовые/старые iPhone, iOS17 runtime,15-wave production load, prolonged thermal/leak/endurance, полноценные pause/background/multitouch acceptance, QA.53/QA.54/G2, TestFlight/physical distribution. Один высокопроизводительный iPhone не устанавливает нижнюю границу устройств.
- Production art integration #47. Default exporter и production validation сохранены и покрыты регрессиями; новый production device QA/export в этой задаче не выполнялся. Domain/boss600/armor5/schema4/saves/balance, source scenes/art assets/packages не менялись.
