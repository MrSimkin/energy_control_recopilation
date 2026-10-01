#!/usr/bin/env python3
"""Phase 2R R2 grouped day-calibrated C2 evaluation.

Implements the frozen PHASE2R_R2_GROUPED_DAY_CALIBRATION_PROTOCOL_2026-09-30.md.
Development is leave-one-local-day-out cross-fitted. Locked holdout is a separate
explicit stage and requires frozen parameters produced only after development PASS.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path

import numpy as np
import pandas as pd

HARNESS_VERSION = "phase2r-r2-grouped-day-calibration.v1"
EXPECTED_CASE_SHA256 = "726ae954afeb996bece4e3f30c4d9fc6f8275a4783691393cba0390dbb64f03f"
DEV_START = pd.Timestamp("2026-07-19")
DEV_END = pd.Timestamp("2026-09-11")
HOLD_START = pd.Timestamp("2026-09-13")
HOLD_END = pd.Timestamp("2026-09-26")
MIN_CALIBRATION_DAYS = 15
TARGET_COVERAGE = 0.90
ALPHA = 0.10
BOOTSTRAP_REPS = 5000


def sha256_file(path: Path) -> str:
    h=hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda:f.read(1024*1024),b""):
            h.update(chunk)
    return h.hexdigest()


def seed64(*parts: object) -> int:
    d=hashlib.sha256("|".join(map(str,parts)).encode()).digest()
    return int.from_bytes(d[:8],"big",signed=False)


def interval_score(lower: float, upper: float, truth: float) -> float:
    score=upper-lower
    if truth<lower:
        score+=(2/ALPHA)*(lower-truth)
    elif truth>upper:
        score+=(2/ALPHA)*(truth-upper)
    return float(score)


def add_group_fields(df: pd.DataFrame) -> pd.DataFrame:
    out=df.copy()
    out["local_date_dt"]=pd.to_datetime(out["local_date"])
    out["duration_class"]=np.where(out["nominal_duration_min"]<=120,"short","long")
    out["start_state"]=np.where(out["active_start"].astype(bool),"active","inactive")
    out["r2_group"]=out["start_state"]+"-"+out["duration_class"]
    out["duration_hours"]=out["actual_duration_min"]/60.0
    return out


def fit_group(train: pd.DataFrame, group: str) -> dict:
    g=train[train["r2_group"]==group].copy()
    if g.empty:
        return {"group":group,"status":"INSUFFICIENT_CALIBRATION_DAYS","calibration_days":0}
    g["rate_residual"]=(g["truth_kwh"]-g["c2_p50"])/g["duration_hours"]
    day_med=g.groupby("local_date")["rate_residual"].median()
    b=float(day_med.median())
    g["lstar"]=np.maximum(0.0,g["c2_p5"]+b*g["duration_hours"])
    shifted_upper=g["c2_p95"]+b*g["duration_hours"]
    g["ustar"]=np.maximum(g["lstar"],shifted_upper)
    g["s_rate"]=np.maximum.reduce([
        np.zeros(len(g)),
        (g["lstar"]-g["truth_kwh"]).to_numpy(),
        (g["truth_kwh"]-g["ustar"]).to_numpy(),
    ])/g["duration_hours"].to_numpy()
    day_scores=g.groupby("local_date")["s_rate"].max().sort_values().to_numpy()
    n=len(day_scores)
    if n<MIN_CALIBRATION_DAYS:
        return {
            "group":group,"status":"INSUFFICIENT_CALIBRATION_DAYS",
            "calibration_days":n,"b_kw":b
        }
    rank=min(n,max(1,math.ceil((n+1)*TARGET_COVERAGE)))
    q=float(day_scores[rank-1])
    return {
        "group":group,"status":"OK","calibration_days":n,
        "b_kw":b,"q_kw":q,"conformal_rank":rank
    }


def apply_params(rows: pd.DataFrame, params: dict) -> pd.DataFrame:
    out=rows.copy()
    if params.get("status")!="OK":
        out["r2_status"]=params.get("status","INSUFFICIENT_CALIBRATION_DAYS")
        out["r2_b_kw"]=params.get("b_kw",np.nan)
        out["r2_q_kw"]=np.nan
        out["r2_p50"]=np.nan
        out["r2_lower"]=np.nan
        out["r2_upper"]=np.nan
        out["r2_covered"]=np.nan
        out["r2_interval_score"]=np.nan
        return out
    b=float(params["b_kw"]); q=float(params["q_kw"])
    h=out["duration_hours"].to_numpy()
    center=np.maximum(0.0,out["c2_p50"].to_numpy()+b*h)
    lstar=np.maximum(0.0,out["c2_p5"].to_numpy()+b*h)
    ustar=np.maximum(lstar,out["c2_p95"].to_numpy()+b*h)
    lower=np.maximum(0.0,lstar-q*h)
    upper=ustar+q*h
    truth=out["truth_kwh"].to_numpy()
    covered=(lower<=truth)&(truth<=upper)
    scores=np.array([interval_score(l,u,y) for l,u,y in zip(lower,upper,truth)])
    out["r2_status"]="OK"
    out["r2_b_kw"]=b
    out["r2_q_kw"]=q
    out["r2_p50"]=center
    out["r2_lower"]=lower
    out["r2_upper"]=upper
    out["r2_covered"]=covered
    out["r2_interval_score"]=scores
    return out


def cluster_coverage_ci(df: pd.DataFrame, covered_col: str, label: str) -> tuple[float,float,float]:
    d=df.dropna(subset=[covered_col]).copy()
    point=float(d[covered_col].astype(float).mean())
    by_day=d.groupby("local_date")[covered_col].agg(["sum","count"]).to_numpy(dtype=float)
    if len(by_day)<=1:
        return point,point,point
    rng=np.random.default_rng(seed64(HARNESS_VERSION,"cluster",label,BOOTSTRAP_REPS))
    reps=np.empty(BOOTSTRAP_REPS)
    n=len(by_day)
    for i in range(BOOTSTRAP_REPS):
        idx=rng.integers(0,n,size=n)
        s=by_day[idx].sum(axis=0)
        reps[i]=s[0]/s[1]
    lo,hi=np.quantile(reps,[.025,.975],method="linear")
    return point,float(lo),float(hi)


def crossfit_development(dev: pd.DataFrame) -> tuple[pd.DataFrame,pd.DataFrame]:
    predictions=[]; fold_params=[]
    groups=("inactive-short","active-short","inactive-long","active-long")
    for held_date in sorted(dev["local_date"].unique()):
        train=dev[dev["local_date"]!=held_date]
        held=dev[dev["local_date"]==held_date]
        for group in groups:
            p=fit_group(train,group)
            p["held_out_date"]=held_date
            fold_params.append(p.copy())
            hg=held[held["r2_group"]==group]
            if not hg.empty:
                predictions.append(apply_params(hg,p))
    pred=pd.concat(predictions,ignore_index=True).sort_values(["local_date","nominal_duration_min","start_utc"])
    return pred,pd.DataFrame(fold_params)


def summarize(pred: pd.DataFrame) -> tuple[pd.DataFrame,pd.DataFrame,dict]:
    ok=pred[pred["r2_status"]=="OK"].copy()
    groups=[]
    for group,g in ok.groupby("r2_group",sort=True):
        p,lo,hi=cluster_coverage_ci(g,"r2_covered",f"group|{group}")
        groups.append({
            "r2_group":group,"cases":len(g),"distinct_dates":g.local_date.nunique(),
            "coverage_pct":100*p,"cluster_ci_low_pct":100*lo,"cluster_ci_high_pct":100*hi,
            "r2_p50_bias_kwh":float((g.r2_p50-g.truth_kwh).mean()),
            "raw_c2_p50_bias_kwh":float((g.c2_p50-g.truth_kwh).mean()),
            "r2_mean_interval_score":float(g.r2_interval_score.mean()),
            "raw_c2_mean_interval_score":float(g.c2_interval_score.mean()),
        })
    group_df=pd.DataFrame(groups)
    durations=[]
    for dur,g in ok.groupby("nominal_duration_min",sort=True):
        p,lo,hi=cluster_coverage_ci(g,"r2_covered",f"duration|{dur}")
        durations.append({
            "nominal_duration_min":dur,"cases":len(g),"distinct_dates":g.local_date.nunique(),
            "coverage_pct":100*p,"cluster_ci_low_pct":100*lo,"cluster_ci_high_pct":100*hi,
            "r2_p50_bias_kwh":float((g.r2_p50-g.truth_kwh).mean()),
            "raw_c2_p50_bias_kwh":float((g.c2_p50-g.truth_kwh).mean()),
            "r2_mean_interval_score":float(g.r2_interval_score.mean()),
            "raw_c2_mean_interval_score":float(g.c2_interval_score.mean()),
        })
    duration_df=pd.DataFrame(durations)

    active=ok[ok.active_start.astype(bool)]
    active_cov=100*float(active.r2_covered.mean())
    raw_active_bias=float((active.c2_p50-active.truth_kwh).mean())
    r2_active_bias=float((active.r2_p50-active.truth_kwh).mean())
    insuff=100*float((pred.r2_status!="OK").mean())
    score_ratio=float(ok.r2_interval_score.mean()/ok.c2_interval_score.mean())

    group_cov={r.r2_group:r.coverage_pct for r in group_df.itertuples()}
    group_upper={r.r2_group:r.cluster_ci_high_pct for r in group_df.itertuples()}
    duration_cov={int(r.nominal_duration_min):r.coverage_pct for r in duration_df.itertuples()}
    gates={
        "active_pooled_coverage_pct":active_cov,
        "active_short_coverage_pct":group_cov.get("active-short"),
        "active_long_coverage_pct":group_cov.get("active-long"),
        "min_primary_group_cluster_upper_pct":min(group_upper.values()) if group_upper else None,
        "raw_active_p50_bias_kwh":raw_active_bias,
        "r2_active_p50_bias_kwh":r2_active_bias,
        "active_abs_bias_reduction_pct":100*(abs(raw_active_bias)-abs(r2_active_bias))/abs(raw_active_bias) if raw_active_bias else np.nan,
        "overall_interval_score_ratio_vs_raw_c2":score_ratio,
        "min_duration_coverage_pct":min(duration_cov.values()) if duration_cov else None,
        "insufficient_pct":insuff,
    }
    gates["gate1_active_pooled_ge85"]=active_cov>=85
    gates["gate2_active_short_long_ge85"]=(group_cov.get("active-short",0)>=85 and group_cov.get("active-long",0)>=85)
    gates["gate3_no_group_upper_below90"]=all(v>=90 for v in group_upper.values())
    gates["gate4_active_abs_bias_reduced"]=abs(r2_active_bias)<abs(raw_active_bias)
    gates["gate5_score_le110pct_raw"]=score_ratio<=1.10
    gates["gate6_no_duration_below80"]=all(v>=80 for v in duration_cov.values())
    gates["gate7_insufficient_le5pct"]=insuff<=5
    gates["development_pass"]=all(gates[k] for k in [
        "gate1_active_pooled_ge85","gate2_active_short_long_ge85",
        "gate3_no_group_upper_below90","gate4_active_abs_bias_reduced",
        "gate5_score_le110pct_raw","gate6_no_duration_below80",
        "gate7_insufficient_le5pct"])
    return group_df,duration_df,gates


def fit_all_development(dev: pd.DataFrame) -> dict:
    groups=("inactive-short","active-short","inactive-long","active-long")
    params={g:fit_group(dev,g) for g in groups}
    if not all(p.get("status")=="OK" for p in params.values()):
        raise RuntimeError("cannot freeze final parameters: insufficient calibration days")
    return params


def evaluate_holdout(hold: pd.DataFrame, params: dict) -> pd.DataFrame:
    pieces=[]
    for group,p in params.items():
        g=hold[hold.r2_group==group]
        if not g.empty:
            pieces.append(apply_params(g,p))
    return pd.concat(pieces,ignore_index=True).sort_values(["local_date","nominal_duration_min","start_utc"])


def validate_input(path: Path) -> pd.DataFrame:
    actual=sha256_file(path)
    if actual!=EXPECTED_CASE_SHA256:
        raise RuntimeError(f"per-case SHA mismatch: expected {EXPECTED_CASE_SHA256}, got {actual}")
    df=pd.read_csv(path)
    df=add_group_fields(df)
    dev=df[(df.local_date_dt>=DEV_START)&(df.local_date_dt<=DEV_END)]
    expected={
        "inactive-short":585,"active-short":157,
        "inactive-long":176,"active-long":48,
    }
    got=dev.groupby("r2_group").size().to_dict()
    if got!=expected:
        raise RuntimeError(f"development group counts mismatch: expected {expected}, got {got}")
    return df


def run_development(cases: Path, outdir: Path):
    df=validate_input(cases)
    dev=df[(df.local_date_dt>=DEV_START)&(df.local_date_dt<=DEV_END)].copy()
    pred,folds=crossfit_development(dev)
    group_df,duration_df,gates=summarize(pred)
    outdir.mkdir(parents=True,exist_ok=True)
    pred.to_csv(outdir/"PHASE2R_R2_DEVELOPMENT_PER_CASE_2026-09-30.csv",index=False)
    folds.to_csv(outdir/"PHASE2R_R2_DEVELOPMENT_FOLD_PARAMS_2026-09-30.csv",index=False)
    group_df.to_csv(outdir/"PHASE2R_R2_DEVELOPMENT_GROUPS_2026-09-30.csv",index=False)
    duration_df.to_csv(outdir/"PHASE2R_R2_DEVELOPMENT_DURATIONS_2026-09-30.csv",index=False)
    (outdir/"PHASE2R_R2_DEVELOPMENT_GATE_2026-09-30.json").write_text(json.dumps(gates,indent=2),encoding="utf-8")
    print("GROUPS
",group_df.to_string(index=False))
    print("
DURATIONS
",duration_df.to_string(index=False))
    print("
GATES
",json.dumps(gates,indent=2))
    if gates["development_pass"]:
        params=fit_all_development(dev)
        frozen={
            "harness_version":HARNESS_VERSION,
            "input_case_sha256":EXPECTED_CASE_SHA256,
            "development_dates":[str(DEV_START.date()),str(DEV_END.date())],
            "holdout_dates":[str(HOLD_START.date()),str(HOLD_END.date())],
            "groups":params,
        }
        (outdir/"PHASE2R_R2_FROZEN_PARAMS_2026-09-30.json").write_text(json.dumps(frozen,indent=2),encoding="utf-8")
        print("DEVELOPMENT_PASS: holdout parameters frozen")
    else:
        print("DEVELOPMENT_FAIL: locked holdout MUST NOT be inspected")


def run_holdout(cases: Path, params_path: Path, outdir: Path):
    df=validate_input(cases)
    frozen=json.loads(params_path.read_text())
    if frozen.get("input_case_sha256")!=EXPECTED_CASE_SHA256:
        raise RuntimeError("frozen parameter input SHA mismatch")
    hold=df[(df.local_date_dt>=HOLD_START)&(df.local_date_dt<=HOLD_END)].copy()
    pred=evaluate_holdout(hold,frozen["groups"])
    group_df,duration_df,gates=summarize(pred)
    outdir.mkdir(parents=True,exist_ok=True)
    pred.to_csv(outdir/"PHASE2R_R2_HOLDOUT_PER_CASE_2026-09-30.csv",index=False)
    group_df.to_csv(outdir/"PHASE2R_R2_HOLDOUT_GROUPS_2026-09-30.csv",index=False)
    duration_df.to_csv(outdir/"PHASE2R_R2_HOLDOUT_DURATIONS_2026-09-30.csv",index=False)
    (outdir/"PHASE2R_R2_HOLDOUT_GATE_2026-09-30.json").write_text(json.dumps(gates,indent=2),encoding="utf-8")
    print("GROUPS
",group_df.to_string(index=False))
    print("
DURATIONS
",duration_df.to_string(index=False))
    print("
GATES
",json.dumps(gates,indent=2))


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument("--cases",required=True,type=Path)
    ap.add_argument("--stage",choices=["development","holdout"],required=True)
    ap.add_argument("--params",type=Path)
    ap.add_argument("--output-dir",type=Path,default=Path("phase2r_r2_output"))
    args=ap.parse_args()
    if args.stage=="development":
        run_development(args.cases,args.output_dir)
    else:
        if args.params is None:
            raise SystemExit("--params is required for holdout")
        run_holdout(args.cases,args.params,args.output_dir)


if __name__=="__main__":
    main()
