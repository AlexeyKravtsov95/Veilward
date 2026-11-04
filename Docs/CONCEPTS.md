# VEILWARD — Concept directions / Art brief v0.1

These are original design directions, not third-party art assets. Concept images found online must remain references and require their own licensing before use in a shipped game.

## A — Shattered Shrine / «Разбитые святилища» (RECOMMENDED)

**One-liner:** аниме-хранители духов удерживают острова, разрушенные межмировым Разломом.

**Visual:** парящие острова, каменные мосты, японская храмовая архитектура без привязки к конкретной лицензированной франшизе, фиолетовый туман пустоты, сакура, бумажные печати, стилизованный 2.5D cel shading. По игровому полю движутся силуэтные ёкаи; башни — миниатюрные духовные конструкции, не люди в полный рост.

**Advantages:** ясная визуальная иерархия, дешёвые повторно используемые окружения, яркие VFX стихий, удобно объяснить мир и способности.

**Risks:** легко скатиться в стандартный fantasy-anime; нужен авторский рисунок форм башен и врагов.

## B — Neon Exorcists / «Неоновые экзорцисты»

**One-liner:** студенты-экзорцисты охраняют город от цифровых духов.

**Visual:** ночной город, крыши, неон, вывески, голографические сигилы, магические дроны и барьеры.

**Advantages:** яркий ключевой арт; более современное позиционирование. **Risks:** много сияющих эффектов, плохая читаемость на телефоне, сложные городские ассеты и высокий overdraw.

## C — Astral Academy / «Астральная академия»

**One-liner:** магическая школа изучает боевую алхимию четырёх стихий.

**Visual:** летающие библиотеки и лаборатории, герои-ученики на иллюстрациях, лабораторные алхимические реакции.

**Advantages:** естественно обучать комбинациям, хорошо подходит для кампании и персонажей. **Risks:** дорогое производство портретов, высокая вероятность непродуктивного ухода в gacha/систему коллекционирования.

## Selected direction: A

### Visual pillars

1. Боевой экран читается прежде декоративности: нейтральная дорога; башни тёплые/яркие; враги тёмные силуэты; эффекты короткие.
2. Персонажный арт 2D anime (портреты между волнами/миссиями), мир боя — экономичная 2.5D диорама.
3. Формы элементов: Пламя — угловатые лепестки; Прилив — кольца/капли; Ветер — дуги/ленты; Камень — ромбы/осколки. Иконки различимы без цвета.
4. Камера ortho 30–45° от горизонта (проверить читаемость тестом), фиксированный базовый поворот.
5. 3 палитры карт: Sakura Dawn, Rain Bridges, Eclipse Citadel.

### Art MVP checklist

- 1 environment blockout / tile set; 1 base shrine; 4 base tower silhouettes + 2 hybrids; 4 enemy silhouettes + 1 boss.
- 4 icon glyphs, 2 reaction VFX, 1 hit VFX per element, 1 death VFX, 1 clear build tile highlight.
- 1 illustration of original protagonist; 1 mentor portrait/icon; 1 key art mockup.
- 1 ambient loop, 1 combat loop, tower shoot/hit/UI sound variants.

### Suggested single key-art brief

"Original anime-fantasy game key art: lone shrine guardian with pale indigo cloak holding a luminous talisman, small fox spirit companion, suspended stone bridges leading to a glowing shrine above clouds, four distinct elemental sigils in sky (flame, tide, wind, stone), violet rift silhouettes encroaching, readable cinematic composition for iPhone landscape app, saturated coral/jade/indigo, original character design, no typography, no existing anime character or franchise."

**Important:** generating art concept ≠ usable 3D game asset. Agent should produce a separate asset pipeline for licensed 2D/3D source files, mesh topology, animation, import settings and optimization.
