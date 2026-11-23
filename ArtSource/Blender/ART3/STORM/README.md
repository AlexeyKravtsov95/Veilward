# ART.3f — оригинальный STORM «Грозовой карп» / #86

Геометрия создана Codex для VEILWARD 23.11.2026 по заданию владельца #86: AI-assisted deterministic Blender mesh authoring. Service/model: OpenAI Codex / GPT-6 family; точный runtime snapshot инструментами не раскрыт. Inputs: issue86, GDD, принятые ART1/palette/import conventions. Внешние модели/reference images/image-generation services не использовались; seed/jobID неприменимы. Авторское/коммерческое approval результата остаётся за владельцем; наличие AI output не доказывает исключительных прав.

Из собственного принятого ART1 загружаются только Surface/Glow/packed palette. Геометрия пяти семейств не меняется. L1 — низкий изгиб карпа и раздвоенный хвост; L2 — S-поза с раскрытыми раздельными плавниками; L3 — поднятый разворот с открытыми грозовыми дугами. Голова/губы/глаза/усики/тело/хвост/плавники объёмные, semantic vertex groups доступны для редактирования. Meters/identity/ground0/footprint≤.94m/≤5000tri/2slots. Новых сторонних creative assets, текстур, материалов, lights/packages/render features нет.

`author_storm.py` заменяет только три собственных STORM .blend. После ручных правок запускать `export_storm.py`: exact source folder/name whitelist, только собственные FBX, проверенный ART1 preset. `check_geometry.py` читает actual sources, проверяет budgets/transforms/UV/normals/semantic forms/позы, пишет geometry-manifest.json. `verify_reexport.py` реально повторяет3экспорта и сравнивает Unity before/after geometry/UV/normals/remaps/GUID/localIDs/material/prefab/scene refs. Timestamp FBX не считается geometry hash.

Первый source check не прошёл унаследованное сравнение ширины: для карпа выбранные позы отличаются S-изгибом/высотой головы и раскрытыми fins. Checker теперь проверяет эти крупные формы напрямую; исходные triangles/UV/normals/pivot/footprint gates сохранены. Initial FAIL сохраняется в evidence.

**Device Deferred to prerelease QA #53/#54/#55.** ShotSource — presentation anchor; damage/chain остаются Domain. Generic beam и текущая causal chain допустимы; финальный lightningVFX #47 отдельно.
