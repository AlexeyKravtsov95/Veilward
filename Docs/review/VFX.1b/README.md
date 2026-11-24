# VFX.1b / #91 — TIDE, GALE, STONE и status cues

[Контракт и regression plan](../../VFX1B_BASIC_SHOTS_STATUS.md). От свежего main merge94, существующий checkout/Library, без worktree/clone/projectcopy/iPhone.

Новые атаки: TIDE капля/рябь, GALE поворачивающийся серп, STONE вращающийся осколок. Общий prewarmed pool12/48renderers/1existing shared material. FLAME остаётся прежним pool8/104renderers/2materials. Status cues следуют live snapshots только в двух существующих slots; inspector и ready/reaction priorities сохранены.

[Evidence и границы](validation.md) · [PlayMode gallery](gallery.md) · [Непрерывное production Update Editor видео](editor-flow.mp4).

Read-only delivery: `python3 Docs/review/VFX.1b/verify_delivery.py`; малые controls: `python3 Docs/review/VFX.1b/verify_controls.py`. Личные TXT/pycache не prerequisite; отдельный audit — `--check-local-preservation`.

Device Deferred to prerelease QA #53/#54/#55. Editor не подтверждает player IL2CPP/Metal/performance/touch. Parent47 до owner merge остаётся1/4; #92/#93/G2/M0/release отдельно. Self-merge/release не выполняются.
