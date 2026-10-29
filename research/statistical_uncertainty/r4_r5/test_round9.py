#!/usr/bin/env python3
"""Regression, invariants and fail-closed tests for R4/R5 round 9."""
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path

import numpy as np
import pandas as pd
from scipy.stats import beta

from round9_research import evaluate_r4,evaluate_r5,load_fixed_source,one_sided_upper,run,SOURCE_SHA256,CUTOFF
from round9_blind_count_draft import count_structural,ALLOWED

SOURCE=Path(os.environ.get('R4R5_ROUND5_ZIP','SolarEnergyMonitor_R4_R5_Ronda5_20261028.zip'))

class Round9Test(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.a,cls.b=load_fixed_source(SOURCE)
        cls.all=run(SOURCE)
    def test_sha_locked(self):
        self.assertEqual(self.all['source_zip_sha256'],SOURCE_SHA256)
    def test_future_not_read(self):
        self.assertFalse(self.all['future_R3_outcomes_used'])
        self.assertTrue((self.a.local_date<CUTOFF).all());self.assertTrue((self.b.local_date<CUTOFF).all())
    def test_R4_counts(self):
        x=self.all['R4']['events_by_duration']
        self.assertEqual([(v['duration_min'],v['comparable_days'],v['event_days']) for v in x],[(30,140,0),(120,140,0),(240,139,5)])
    def test_R4_windows(self):
        x=self.all['R4']['events_by_duration']
        self.assertEqual([(v['zero_endpoint_windows'],v['event_windows']) for v in x],[(5398,0),(1179,0),(489,5)])
    def test_R4_upper_bound_n0_does_not_fake_zero(self):
        self.assertGreater(one_sided_upper(0,140),0.01)
        self.assertLess(one_sided_upper(0,140),.02)
    def test_R4_prequential_risk(self):
        sc=self.all['R4']['chronological_policy_scenarios']
        a=next(x for x in sc if x['prior_min_days']==20 and x['risk_tolerance_pct']==5)
        self.assertEqual((a['LOW_RISK_SIGNAL_days'],a['FALSE_REASSURANCE_event_days']), (16,1))
    def test_R4_warn_all_not_useful(self):
        sc=self.all['R4']['always_warn_comparator_start_20']
        self.assertEqual((sc['warnings'],sc['false_warnings'],sc['events']), (119,114,5))
    def test_R4_no_superficial_safe_at_1pct(self):
        a=next(x for x in self.all['R4']['chronological_policy_scenarios'] if x['prior_min_days']==20 and x['risk_tolerance_pct']==1)
        self.assertEqual(a['LOW_RISK_SIGNAL_days'],0)
    def test_R4_post_range(self):
        v=one_sided_upper(5,139)
        self.assertAlmostEqual(v,beta.ppf(.95,5.5,134.5))
    def test_R5_counts(self):
        a=self.all['R5'];self.assertEqual((a['cases'],a['dates'],a['positive_cases'],a['positive_dates'],a['hybrid_applied_cases'],a['hybrid_applied_dates']),(1302,22,96,7,64,6))
    def test_R5_total_mae(self):
        a=self.all['R5'];self.assertAlmostEqual(a['bridge_mae_kwh'],.02281425298941799)
        self.assertAlmostEqual(a['hybrid_mae_kwh'],.02260610201741622)
    def test_R5_duration_strata(self):
        m={x['duration_min']:x for x in self.all['R5']['duration_strata']}
        self.assertGreater(m[30]['hybrid_mae_kwh'],m[30]['bridge_mae_kwh'])
        self.assertGreater(m[120]['hybrid_mae_kwh'],m[120]['bridge_mae_kwh'])
        self.assertLess(m[240]['hybrid_mae_kwh'],m[240]['bridge_mae_kwh'])
    def test_R5_uncertainty_not_robust(self):
        l,h=self.all['R5']['equal_day_bootstrap_delta_95pct'];self.assertLess(l,0);self.assertGreater(h,0)
    def test_R5_LOO_some_reverse(self):
        self.assertGreater(self.all['R5']['case_weighted_loo_delta_range'][1],0)
    def test_R5_gate_illustrative_inconclusive(self):
        self.assertTrue(all(not g['meets'] for g in self.all['R5']['illustrative_positive_date_gates']))
    def test_posterior_validation(self):
        for args in [(0,0),(-1,5),(6,5)]:
            with self.assertRaises(ValueError):one_sided_upper(*args)
    def test_source_hash_reject_other_zip(self):
        with tempfile.TemporaryDirectory() as d:
            p=Path(d)/'fake.zip';p.write_bytes(b'not the source')
            with self.assertRaises(ValueError):load_fixed_source(p)

class BlindTest(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        self.p=Path(self.temp.name)/'test.csv'
        self.df=pd.DataFrame([{'local_date':'2026-11-01','regular_samples':287,'r4_zero_zero_240_candidates':12,
               'r5_active_endpoint_candidates':8,'r5_known_age_candidates':3},
              {'local_date':'2026-11-02','regular_samples':280,'r4_zero_zero_240_candidates':5,
               'r5_active_endpoint_candidates':2,'r5_known_age_candidates':2}],columns=ALLOWED)
    def check(self):return count_structural(self.p,'2026-10-30')
    def test_safe_output_status(self):
        self.df.to_csv(self.p,index=False);r=self.check()
        self.assertEqual(r['status'],'DRAFT_NO_READY_OR_SCORE');self.assertFalse(r['score_computed'])
        self.assertEqual(r['r4_candidate_count'],17);self.assertEqual(r['r5_known_age_count'],5)
    def test_reject_truth_column(self):
        self.df['truth_kwh']=[1,2];self.df.to_csv(self.p,index=False)
        with self.assertRaises(ValueError):self.check()
    def test_reject_event_column(self):
        self.df['event_day']=[0,1];self.df.to_csv(self.p,index=False)
        with self.assertRaises(ValueError):self.check()
    def test_reject_old_dates(self):
        self.df.loc[0,'local_date']='2026-10-29';self.df.to_csv(self.p,index=False)
        with self.assertRaises(ValueError):self.check()
    def test_reject_duplicate_date(self):
        self.df.loc[1,'local_date']='2026-11-01';self.df.to_csv(self.p,index=False)
        with self.assertRaises(ValueError):self.check()
    def test_reject_reverse_dates(self):
        self.df=self.df.iloc[::-1];self.df.to_csv(self.p,index=False)
        with self.assertRaises(ValueError):self.check()
    def test_reject_negative(self):
        self.df.loc[0,'regular_samples']=-5;self.df.to_csv(self.p,index=False)
        with self.assertRaises(ValueError):self.check()
    def test_reject_fractional(self):
        self.df.loc[0,'regular_samples']=281.4;self.df.to_csv(self.p,index=False)
        with self.assertRaises(ValueError):self.check()
    def test_reject_known_age_over_endpoint(self):
        self.df.loc[0,'r5_known_age_candidates']=12;self.df.to_csv(self.p,index=False)
        with self.assertRaises(ValueError):self.check()
    def test_reject_R4_over_sample(self):
        self.df.loc[0,'r4_zero_zero_240_candidates']=288;self.df.to_csv(self.p,index=False)
        with self.assertRaises(ValueError):self.check()
    def test_reject_missing(self):
        self.df.loc[0,'regular_samples']=np.nan;self.df.to_csv(self.p,index=False)
        with self.assertRaises(ValueError):self.check()
    def test_does_not_contain_outcomes(self):
        self.df.to_csv(self.p,index=False);result=json.dumps(self.check()).lower()
        self.assertNotIn('kwh',result);self.assertNotIn('coverage',result)

if __name__=='__main__':unittest.main(verbosity=2)
