# Revision 2 — clean-checkout delivery audit

Новый manifest/verifier не переписывают прежний ART.2a.1 packet. Protected entries сверяются с исходными Git blobs двух refs: original merge72 и baseline ba87f0f. Новый source/media/report — прямой SHA256. Markdown и сам verifier исключены из self-hash; проверяется tracked delivery.

Read-only audit и negative controls выполняются на отдельном clean checkout после commit, без Unity/Library. Финальный результат фиксируется ниже после выполнения. Unity suites выполняются только в авторизованном task worktree; read-only reviewer не запускает второй Editor.

NOT VERIFIED: human continuous-motion approval и физический iPhone. Hash/media/timeline PASS не является художественной приёмкой.
