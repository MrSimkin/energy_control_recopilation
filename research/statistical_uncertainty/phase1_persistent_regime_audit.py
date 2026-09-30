#!/usr/bin/env python3
"""Phase 1 Lot 5 analytical persistent-regime stress specification.

True episode:
  B ~ Bernoulli(q)
  Y = B * P * m * Delta

Current point-wise approximation:
  B_i iid Bernoulli(q)
  Y_ind = P * Delta * sum(B_i)

Exact variance ratio:
  Var(Y_ind)/Var(Y) = 1/m

Canonical numerical results are retained in
PHASE1_LOT5_PERSISTENT_REGIME_RESULTS_2026-09-30.csv.
"""
