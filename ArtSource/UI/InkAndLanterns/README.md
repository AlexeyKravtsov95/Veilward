# Исходники «Чернила и фонари» для UI.REWORK.1

`menu-master.png` — оригинальная иллюстрация OpenAI ImageGen для VEILWARD, созданная 26.11.2026 с утверждённым `approved-ui-concept.png` в качестве стилевого референса. Исходный результат `exec-6f42dbfb-d177-40e5-8473-4ae6b5fb6fd3.png` сохранён без редактирования. В игре используется JPEG из этого мастера. Текст, иконки и кнопки выводит Unity. Происхождение иллюстрации и права на output описаны в [условиях OpenAI](https://openai.com/policies/terms-of-use/).

## Запрос для ImageGen

> Create original painted title-screen background art for VEILWARD, an ink-and-lanterns fantasy tower defense game. Use the provided four-panel approved concept as style reference ONLY, especially lower left. Output one full-bleed widescreen 16:9 illustration, no graphic design mockup, absolutely no lettering, logos, buttons, words, panel borders, UI. The entire left 51% is quiet midnight-indigo ink wash, gentle mist and faint distant islands low in the frame, sufficiently dark for independently rendered ivory title and controls. Right half: a vermilion timber shrine gate with warm hanging paper lanterns, stone steps climbing from lower-right, and a small original ivory-and-coral flame fox guardian perched on the gate ridge. Coral cherry blossoms framing the upper-right and just the extreme upper-left corner, distant cliff silhouettes and waterfalls lower in the frame, a muted apricot moon behind the shrine. Bold ink contour, warm matte gouache, weathered Japanese print paper grain, deep desaturated prussian-blue shadows, refined geometric stylization with lively brush texture. Strong defined shapes, asymmetrical composition, no photorealism. Gate occupies right-center and lower right; left central zone stays dark and visually quiet. Original artwork in the established game palette.

## Иконки и поверхности

`render.py` хранит авторские векторные контуры и градиенты пиктограмм, контуры бумаги и кисти, а также детерминированную фактуру. Он выводит один прозрачный runtime atlas 1024×512, отдельный MenuBackdrop JPEG и координаты слайсов. Растровые размеры, девять сегментов и имена спрайтов фиксирует Unity TextureImporter в `InkUi.png.meta`. Повторить вывод можно командой `python3.12 ArtSource/UI/InkAndLanterns/render.py` с CairoSVG 2.9.0 и Pillow 12.3.0.

Для Review 26.11.2026 отрисованы восемь отдельных силуэтов поверхностей. Один тонкий контур, матовые градиенты и слабые следы пигмента в углах сохраняют читаемость при растяжении 9-slice. [Кадры GameView](../../../Docs/review/UI.REWORK.1/polish/README.md)

Для UI.REWORK.2 в `render.py` добавлены шесть оригинальных векторных контуров боя `BurnBadge`, `WetBadge`, `GustBadge`, `CrackBadge`, `SteamBadge`, `StormBadge`. Векторные мастера и матовая рамка находятся в том же файле. Слайсы 68×68 заняли свободную нижнюю строку 1024×512 без изменения прежних координат, fileID или import settings.

Заголовки используют Cormorant Garamond Bold Italic, Christian Thalmann / The Cormorant Project Authors, [upstream 0167848](https://github.com/CatharsisFonts/Cormorant/tree/016784836d2ad47bc4c62b7f504a0a5875d85aeb). [SIL Open Font License 1.1](OFL-Cormorant.txt) сохранена рядом. Шрифт встроенных контролов и динамического мелкого текста взят из Unity 6000.3.28f1 (`LegacyRuntime.ttf`).
