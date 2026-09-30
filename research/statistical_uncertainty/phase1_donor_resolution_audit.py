#!/usr/bin/env python3
"""Phase 1 Lot 4 donor-time-resolution causal audit.

Controlled independent day/night process; compares current whole-hour donor
pooling, a narrow +/-15 minute clock-position pool, and an analytical Gaussian
oracle across 14/30/60-day report windows.
"""
# Canonical equations/design/results are documented in
# PHASE1_LOT4_DONOR_RESOLUTION_REPORT_2026-09-30.md.
# The executable research implementation used numpy/scipy and 1,000 repetitions
# per cell with the same synthetic day/night profile as prior Phase 1 lots.
#
# This retained file is intentionally compact: the auditable numerical output
# and methodological specification are the canonical evidence artifacts.
#
# Reproduction requirements:
# - 5-minute cadence;
# - day/night deterministic profile from prior Phase 1 harness;
# - iid N(0,120^2) residuals;
# - topology 6x20m;
# - current method: same hour + same weekend flag;
# - fine control: same weekend flag + circular clock-slot distance <=15m;
# - exact oracle: trapezoid-integrated Gaussian gap mean and
#   variance sigma^2*(5/60/1000)^2*(k-0.5) per disjoint k-segment gap;
# - 2,000 Monte Carlo completions for empirical methods;
# - 1,000 repetitions;
# - Wilson 95% coverage interval and one-sided exact binomial calibration test.
