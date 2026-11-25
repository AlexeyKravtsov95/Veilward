# Авторинг Ink and Lanterns

Мастера получены 25.11.2026 во встроенном OpenAI ImageGen с параметром прозрачного фона. Модель, версия и seed инструментом не раскрыты. Вход для каждого запроса — утверждённый владельцем лист из `ArtSource/References/ART_REWORK_0`, blob `bca4ed2d46e325b65a7ee9f580322ee4da2a269b`. Оригинальные RGBA-выходы сохранены в `Masters/`. Подготовка слоёв и кадров автоматизирована в `prepare_sprites.py`. Подтверждение коммерческого использования остаётся за владельцем. [Решение владельца по референсу](https://github.com/AlexeyKravtsov95/Veilward/issues/100) · [Условия OpenAI](https://openai.com/policies/terms-of-use/).

## Первая серия. Общая часть

> Create a production game sprite master for VEILWARD. The attached approved Ink and Lanterns concept sheet is a STYLE and DESIGN reference. Use expressive textured ink outlines, matte gouache and hand-painted brushwork, indigo/jade shadows, warm strong coral and yellow-gold. Prioritize a bold silhouette with thick dark ink and large clear facial features that survive at 45–90 pixels. Orthographic 3/4 view at 28 degrees above ground level. Isolated art, actual transparent RGBA background, no paper texture outside objects, no cast shadow, detached debris, headings, text, border, grid lines or stage. Center each object with generous transparent outside margin.

### Flame1

> One compact ember lantern shrine, use L1 at lower left of the reference. Render EXACTLY TWO ISOLATED CUTOUTS side-by-side on the canvas, one in each half, no contact. LEFT half: the full compact shrine by itself, flat visible ink stone footing, one rectangular amber paper lantern with luminous broad golden panes and short teal roof with upward tips, a hand-tied rope around the low dark rock pedestal. The top of its roof is a perch. RIGHT half: one seated white fox spirit facing screen-right, white face with black eye and cheek ink, coral ear tips, a single broad coral flame tail curled left, paws together, full head and body, entirely separate from shrine. Similar painted look and contrasts to the reference.

### Flame2

> An intermediate ember lantern tower between L1 and L3 of the reference. Render EXACTLY TWO ISOLATED CUTOUTS side-by-side on the canvas, one in each half, no contact. LEFT half: shrine by itself, two wide gold paper lantern panels side by side and one short suspended coral charm on either side, deep jade broad bow-roof with a second small raised center ridge and warm bronze trim, open wooden frame around amber windows, two-layer ink stone pedestal tied with knotted rope. Strong wider silhouette with central roof perch. RIGHT half: one seated white fox spirit facing screen-right with two sweeping distinct coral and gold flame-shaped tails extending left, alert black eye, coral cheek ink and ears, paws together, full head and body, fully separate from the shrine. Draw at game-asset scale with a simple ink contour.

### Flame3

> A large three-bay shrine and multi-tailed ember spirit based on L3 at the lower left of the reference sheet. Render EXACTLY TWO ISOLATED CUTOUTS side-by-side on the canvas, one in each half, no contact. LEFT half: shrine by itself, ornate high stepped slate pedestal with tall squared pilasters, three broad illuminated amber windows, flared double indigo/jade roof profile with copper-gold roof crest, two dangling vermilion lanterns, white folded offerings tied to ritual rope, wide solid visible footing. Top roof ridge supports a fox spirit. RIGHT half: one powerful seated ivory fox spirit facing screen-right with three big distinct curling coral-and-gold flame tails to the left, bold pointy ears, keen dark eye, gold ear clasp and a broad coral marking. Full figure, no overlap with the shrine. Confident ink wash and high contrast.

### Warden

> One complete Rift Warden character based on the approved bottom-right ref. Independent stout intimidating silhouette with long sharp ivory and coral fox-style ceremonial mask, two pointed horns, dark eye sockets and golden eyes, broad ivory ash mane, a single large asymmetric antique-gold shoulder pauldron, overlapping deep indigo pleated robes with printed jade trims and coral cords, heavy boots planted in a broad stride. Left of its silhouette an oversized brass ritual bell with visible mouth and coral tassel held out on wrapped short crook, right shoulder the gold pauldron. Pose is oriented slightly screen-right. Paint deliberately broad shapes with inky borders, dark outlines and ochre gold. One single isolated full-body cutout, no other figures, no supporting objects, transparent beneath boots.

## Повторная серия. Общая часть

> Use the approved Ink and Lanterns concept sheet as design and visual style reference for production raster game art. Clear, bold ink contours, restrained textured gouache, shadows in deep indigo and jade, bright cream and vermilion and warm gold. Make the silhouette and face legible at 60 pixels high. Actual alpha cutout on transparent RGBA, no backdrop, text, border, shadow, spark or detached pieces. Entire bodies are visible.

### Shade

> Draw the paper and ink Shade spirit seen in the lower central panel. A large ivory angular paper cat/fox mask with one big orange eye visible in sharp three-quarter view toward screen-right, thick coral brush stroke on the brow, an impish angled mouth, wicked dark claw extended forward, compact swinging angular paper robe in ink purple, cream hem and V-shaped red ribbons that trail behind. Small separated legs bent in a confident running step. Strong black ink, tactile paint. One single centered character cutout, with extra room on all sides, suitable for a 2.5D orthographic mobile game.


## Сохранённые выходы

| Мастер | Идентификатор файла ImageGen |
| --- | --- |
| Flame1 | exec-0d81cb3e-d3d7-4833-94a5-511410b1cb93.png |
| Flame2 | exec-1478bbad-ff4a-411e-b6e6-5a4b8859e3a2.png |
| Flame3 | exec-308677eb-3d03-4255-ab93-a44e09e8ceec.png |
| Shade | exec-f2d87f33-bbc3-4bdf-a327-24c15d5e4692.png |
| Warden | exec-150dfde9-b293-47b4-a676-56345ac3d2a6.png |

Первый запрос Shade завершился сетевой ошибкой. Для сохранённого Shade использован повторный запрос выше. Для L3 задана маска по краю конька, чтобы в игре остался отдельный лис с правой половины мастера. Ручная дорисовка в процессе подготовки не выполнялась.
