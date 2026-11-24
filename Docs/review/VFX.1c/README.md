# VFX.1c / #92 — Editor evidence

[Контракт и регрессии](../../VFX1C_HYBRID_REACTIONS.md). Ветка основана на merge #95 `4dac946a`. Все Unity-прогоны выполняются последовательно в существующем checkout, с переиспользованием открытого Unity 6000.3.28f1.

## Что проверяем

- STEAM выпускает направленный паровой импульс и расходящиеся клубы у цели. STORM выпускает движущийся сегментированный разряд и короткие искры. Direct-эффекты используют captured AttackEvent и авторитетный уровень башни.
- В Steam реакции паровая волна и вспышки соответствуют captured центру и фактическим HP loss. Storm визуализирует дополнительные попадания отдельно, последовательно и с captured From/Target для каждого hop.
- Direct pool содержит 8×4 LineRenderer. Переиспользуемый reaction pool содержит 24×5. Оба используют существующий shared ReactionEffect и общий active clock Battle. Inspector, две overhead позиции и порядок ability/reaction сохраняются.
- Процедурные исходники авторства VEILWARD по спецификации #92. Формы, палитра и тайминги версионируются в C#. Источник материала — существующий ReactionEffect с URP Unlit. Зафиксированы актуальные Unity, URP, Core и Pipeline package inputs. Отдельных импортов моделей, текстур или аудио нет.

## Доказательства

- [Непрерывное production Update Editor видео](editor-flow.mp4) и [PNG по фазам и событиям](gallery.md).
- [Команды, точные результаты и ограничения](validation.md) · [Независимое code/visual review](review.md).
- `python3 Docs/review/VFX.1c/verify_delivery.py` выполняет read-only проверку. Нужны Python 3.10+, Pillow, FFmpeg/FFprobe и соответствующие pinned файлы Unity PackageCache. Проверка `--check-local-preservation` дополнительно использует локальный начальный аудит.
- `python3 Docs/review/VFX.1c/verify_controls.py` запускает малые положительные и отрицательные проверки целостности без Unity и копии проекта.

Физический iPhone отложен до общей QA #53/#54/#55. Parent #47 остаётся 2/4 до решения владельца. Общая визуальная приёмка, ability и boss cues входят в #93.
