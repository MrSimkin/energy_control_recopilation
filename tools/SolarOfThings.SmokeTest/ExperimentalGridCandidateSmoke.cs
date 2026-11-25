using SolarOfThings.Core.Utility;

internal static class ExperimentalGridCandidateSmoke
{
    public static void Run()
    {
        var origin=new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.Zero);
        // Synthetic daily alternating 0/1000 W regimes; enough prior dates
        // for all three candidate families. No owner data or future score.
        var samples=new List<ExperimentalGridSample>();
        for(var day=0;day<70;day++)
            for(var i=0;i<288;i++)
                samples.Add(new(origin.AddDays(day).AddMinutes(i*5),i>=48&&i<144?1000:0));
        var t=origin.AddDays(70);
        var gaps=new[]{
            new ExperimentalGridGap(1,t.AddHours(5),t.AddHours(5.5),"INTERNAL",1000,1000),
            new ExperimentalGridGap(2,t.AddHours(5),t.AddHours(10),"INTERNAL",1000,1000),
            new ExperimentalGridGap(3,t.AddHours(3),t.AddHours(12),"INTERNAL",0,0),
            new ExperimentalGridGap(4,t,t.AddMinutes(10),"BOUNDARY_START",null,0),
            new ExperimentalGridGap(5,t.AddHours(5),t.AddHours(7),"INTERNAL",1000,1000)};
        samples.Add(new(t.AddHours(4).AddMinutes(55),1000));
        samples.Add(new(t.AddHours(5),1000));
        var r=ExperimentalGridImportCandidates.Analyze(samples,gaps,"UTC");
        if(r.Estimates.Count!=15)throw new InvalidOperationException("R345 matrix incomplete");
        var r3=r.Estimates.Single(e=>e.GapIndex==1&&e.Candidate=="R3");
        var r4=r.Estimates.Single(e=>e.GapIndex==1&&e.Candidate=="R4");
        var r5=r.Estimates.Single(e=>e.GapIndex==2&&e.Candidate=="R5");
        if(r3.P50 is not double v3 || Math.Abs(v3-.5)>1e-9 ||
            r4.P50 is not double v4 || Math.Abs(v4-.5)>1e-9 ||
            r5.P50 is not double v5 || !double.IsFinite(v5))
            throw new InvalidOperationException("R345 synthetic support or constant bridge failed");
        if(r.Estimates.Where(e=>e.GapIndex is 3 or 4).Any(e=>e.P50.HasValue)||
            r.Estimates.Single(e=>e.GapIndex==5&&e.Candidate=="R4").P50.HasValue)
            throw new InvalidOperationException("R345 out of scope estimate emitted");
        if(r.Estimates.Where(e=>e.P50.HasValue).Any(e=>e.P5<0||e.P5>e.P50||e.P50>e.P95))
            throw new InvalidOperationException("R345 unordered quantiles");
        if(r.Aggregates.Any(a=>a.Estimated>=a.Total)||r.Aggregates.Single(a=>a.Candidate=="R5").Estimated!=1)
            throw new InvalidOperationException("R345 partial aggregate not accounted for");
        var fallback = gaps.ToDictionary(g=>g.Index,g=>new[]{1d,2d});
        var hybrid = ExperimentalGridImportCandidates.Analyze(samples,gaps,"UTC",fallback);
        if (hybrid.Hybrids.Count!=3 || hybrid.Hybrids.Any(h=>h.CandidateGaps+h.FallbackGaps!=h.TotalGaps ||
            !h.P50.HasValue || h.P5>h.P50 || h.P50>h.P95))
            throw new InvalidOperationException("Hybrid coverage and quantiles failed");
        if (hybrid.Hybrids.Single(h=>h.Candidate=="R5").CandidateGaps!=1)
            throw new InvalidOperationException("Hybrid R5 candidate count failed");
        fallback.Remove(3);
        var incomplete=ExperimentalGridImportCandidates.Analyze(samples,gaps,"UTC",fallback);
        if (incomplete.Hybrids.Any(h=>h.P50.HasValue))
            throw new InvalidOperationException("Hybrid must not fabricate totals for uncovered gaps");
        // Prior-only: perturb samples after the target day; predictions
        // must remain identical.
        var later=samples.Append(new(t.AddDays(1),999999));
        var repeat=ExperimentalGridImportCandidates.Analyze(later,gaps,"UTC");
        if(!r.Estimates.SequenceEqual(repeat.Estimates)||!r.Aggregates.SequenceEqual(repeat.Aggregates))
            throw new InvalidOperationException("R345 future leakage or unstable seed");
    }
}
