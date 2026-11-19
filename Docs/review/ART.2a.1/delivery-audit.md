# Чистая доставка ART.2a.1

Source/evidence commit **070ad2b8f57ea6aa111794d354841af8fb069a4c**, base`7783dd1bae83784b78f67ebe11108b083d375c82`. Последующие commits — только Markdown о доставке/review. Полный package включает saved sources, `.meta`,28actualPlayModePNG и2MP4; historical ART.1/1.1/2a неизменны. [Manifest](provenance.json), [read-only verifier](verify_delivery.py), [независимый audit](review.md).

## Команды / положительный контроль

```sh
git clone --shared --single-branch --branch codex/art-2a-1-readability /Users/alexeykravtsov/.codex/worktrees/art-2a-1-readability/Veilward /tmp/veilward73-clean
git -C /tmp/veilward73-clean checkout --detach
python3 /tmp/veilward73-clean/Docs/review/ART.2a.1/verify_delivery.py
git -C /tmp/veilward73-clean diff 7783dd1 HEAD --check
git -C /tmp/veilward73-clean status --porcelain
```

На code/evidence snapshot: **320protected/77delivery/54tracked package files PASS**, status пуст. Verifier сравнивает protected bytes одновременно с baseGitBlob, actual Git tree base и SHA256; source/media delivery — SHA256. Весь пакет обязан быть tracked. FFmpeg декодирует оба видео, ffprobe считает240frames/1280×720/30fps каждого; telemetry имеет advancing frame/tick. Report validator требует positive totals/fullPASS и все3новыхPlayModeFullName. Markdown/verifier не self-hashed, но проверяются tracked.

Независимый reviewer повторил verifier и прямое побайтовое сравнение320protected. Full suites не запускались в clone: фактические Editor reports доставлены из tested worktree и захешированы.

## Отрицательные контроли в disposable clone

1. К `MAT_Art1_Surface.mat` добавлен test drift. В копии provenance SHA256 заменён на hash изменённых bytes, baseGitBlob сохранён. Verifier отклонил **именно несоответствие Git base blob**, exit1; затем material/manifest восстановлены побайтно.
2. `after-03-flame-behind.png` временно перенесён в ignoredTemp: missing capture отклонён exit1; файл восстановлен.
3. Для того же PNG выполнен `git rm --cached -- <path>`: untracked capture отклонён exit1; index восстановлен `git restore --staged -- <path>`.

После всех3контролей verifier снова exit0, `git status --porcelain` пуст. Ни assets источника, ни historical hashes не переписывались ради PASS. Временный clone не открывался в Unity.

## Границы

Это проверка доставки/source/evidence и report consistency, не отдельный Editor/device run и не human motion approval. iPhone/performance#43, production VFX.1/#47 и visual acceptance#73 остаются вне подтверждённого gate. Self-merge/release не выполнялись.
