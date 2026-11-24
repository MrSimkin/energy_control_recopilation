# Frozen candidate references (2026-11-24)
All seven Python source/test blobs below were matched to the Git blob SHA-1 calculated from the exact bytes of the original local closure ZIPs. The four protocol/receipt blobs in the preceding documentation commit also match original ZIP bytes. No raw input telemetry or individual cases are in GitHub.

- R4/r4_frozen.py: d394961d5921699400a043f1885ae5b617c3a0cc
- R4/test_r4.py: 29334b9e3c9c48f2a1c8ce7d97ae9d1df4395fd7
- R5/r5_frozen.py: 258ec9eee036e0b0bcbfb7221b5db6c8bc13990c
- R5/r4_reference.py: d394961d5921699400a043f1885ae5b617c3a0cc
- R5/r5_bridge.py: b888806aa56fa0bc4e303688826ec8bb5791b27a
- R5/test_r5.py: cda6b6608bff618e2e7cc4f18b074a7fa5c8723c
- R5/test_operational.py: 55920b1952a87ac56b720ef05a61f2c378295890

R3: ../phase2r_r3_future_validation.py (original unchanged); use its frozen protocol and count/score rules.
R4: in R4 directory run python r4_frozen.py --stage count --package <private ZIP> --out <private result directory>. Run python test_r4.py for synthetic checks.
R5: in R5 directory run python r5_frozen.py --stage count --package <private ZIP> --out <private result directory>. Run python test_r5.py and python test_operational.py for synthetic checks. R4 reference alias is byte-identical.
Only run prospective score after each candidate's own READY; do not change the start, stop rule, parameters or tests after seeing outcomes.
The C# internal integration port is separate and not a substitute for reference acceptance. Exact Python source archival is complete; parity, aggregate acceptance and prospective acceptance remain open. The historical per-case files stay in private closure archives and can be regenerated from the owner package.
