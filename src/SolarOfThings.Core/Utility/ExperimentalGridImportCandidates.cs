using SolarOfThings.Core.SolarOfThings;

namespace SolarOfThings.Core.Utility;

// Internal engineering port. The frozen Python references remain the statistical
// authority. No prospective acceptance or C#/Python parity is implied.
public sealed record ExperimentalGridSample(DateTimeOffset Utc, double Watts);
public sealed record ExperimentalGridGap(int Index, DateTimeOffset StartUtc,
    DateTimeOffset EndUtc, string Kind, double? StartWatts, double? EndWatts);
public sealed record ExperimentalGridEstimate(int GapIndex, string Candidate,
    string Version, string Status, int SupportDates, double? P5, double? P50,
    double? P95);
public sealed record ExperimentalGridAggregate(string Candidate, int Estimated,
    int Total, double? IndependentP5, double? IndependentP50, double? IndependentP95,
    double? CommonRankP5, double? CommonRankP50, double? CommonRankP95);
public sealed record ExperimentalGridReport(IReadOnlyList<ExperimentalGridGap> Gaps,
    IReadOnlyList<ExperimentalGridEstimate> Estimates,
    IReadOnlyList<ExperimentalGridAggregate> Aggregates);

public static class ExperimentalGridImportCandidates
{
    public const string OptInVariable = "SOLAR_ENERGY_MONITOR_EXPERIMENTAL_R345";
    public static bool IsEnabled => Environment.GetEnvironmentVariable(OptInVariable) == "1";
    public const string R3Version = "grid-import-boundary-residual-dayweighted-240m.candidate-v4";
    public const string R4Version = "grid-import-boundary-hurdle-dayweighted.candidate-v5.r4-v3";
    public const string R5Version = "grid-import-hidden-semi-markov-duration.candidate-v6";
    private static readonly int[] Durations = [20,30,45,60,90,120,180,240];
    private static readonly int[] R3Durations = [20,30,60,120,240];
    private const int Cap = 576, R5Draws = 10000, AggregateDraws = 100000;
    private static readonly int[] AgeBins = [1,3,6,12,24,48,72,96,144,216,288,432,576,10000000];

    private sealed record Sample(DateTimeOffset Utc, DateTimeOffset Local, DateOnly Day, double W);
    private sealed record Window(DateOnly Day, int Duration, int Group, int Band, bool Weekend,
        bool Active, int BoundaryCount, double Rate, double Bridge, double InteriorResidual);
    private sealed record Weighted(double Value, double Cumulative);
    private sealed record Distribution(Weighted[] Values)
    {
        public double Quantile(double u)
        {
            var lo=0; var hi=Values.Length-1;
            while(lo<hi) { var mid=(lo+hi)/2; if(Values[mid].Cumulative>=u) hi=mid; else lo=mid+1; }
            return Values[lo].Value;
        }
    }
    private sealed record Prediction(ExperimentalGridEstimate Row, Distribution? Dist, bool AggregateEligible);
    private sealed record Episode(int A, int B, int State, bool Left, bool Right);

    public static ExperimentalGridReport Analyze(
        IEnumerable<ExperimentalGridSample> source,
        IReadOnlyList<ExperimentalGridGap> gaps, string timeZoneId)
    {
        // Fail closed if physically invalid normalized readings occur in a
        // relevant history. Do not turn negative values into valid zeros.
        var input=source.OrderBy(s=>s.Utc).GroupBy(s=>s.Utc).Select(g=>g.Last()).ToArray();
        var zone=SolarApiTime.GetTimeZoneInfo(timeZoneId);
        var samples=input.Select(s=>{
            var l=TimeZoneInfo.ConvertTime(s.Utc,zone);
            return new Sample(s.Utc,l,DateOnly.FromDateTime(l.DateTime),s.Watts);
        }).ToArray();
        var results=new List<Prediction>();
        foreach(var gap in gaps)
        {
            var start=TimeZoneInfo.ConvertTime(gap.StartUtc,zone);
            var end=TimeZoneInfo.ConvertTime(gap.EndUtc,zone);
            var day=DateOnly.FromDateTime(start.DateTime);
            var h=(gap.EndUtc-gap.StartUtc).TotalHours;
            var minutes=h*60;
            if(gap.Kind!="INTERNAL" || gap.StartWatts is not double x0 || gap.EndWatts is not double x1 ||
                !double.IsFinite(x0)||!double.IsFinite(x1)||x0<0||x1<0||minutes<=15)
            {
                foreach(var m in new[]{"R3","R4","R5"}) results.Add(Missing(gap,m,"NOT_APPLICABLE_BOUNDARY_OR_DURATION"));
                continue;
            }
            // Calibrate exclusively on dates prior to the target day.
            var history=samples.Where(s=>s.Day<day).ToArray();
            if(history.Any(s=>!double.IsFinite(s.W)||s.W<0))
            {
                foreach(var m in new[]{"R3","R4","R5"}) results.Add(Missing(gap,m,"INVALID_PRIOR_TELEMETRY"));
                continue;
            }
            if(minutes<=240)
            {
                var donors=BuildWindows(history,false);
                results.Add(PredictR3(gap,donors,h,x0,x1));
                results.Add(SameBand(start,end,6)
                    ? PredictR4(gap,donors,h,x0,x1,start)
                    : Missing(gap,"R4","NOT_APPLICABLE_LOCAL_BAND"));
                results.Add(Missing(gap,"R5","NOT_APPLICABLE_DURATION"));
            }
            else
            {
                results.Add(Missing(gap,"R3","NOT_APPLICABLE_DURATION"));
                results.Add(Missing(gap,"R4","NOT_APPLICABLE_DURATION"));
                results.Add(minutes<=480&&SameBand(start,end,12)
                    ? PredictR5(gap,history,samples,h,x0,x1)
                    : Missing(gap,"R5","NOT_APPLICABLE_DURATION_OR_BAND"));
            }
        }
        var aggregate=new List<ExperimentalGridAggregate>();
        foreach(var m in new[]{"R3","R4","R5"})
        {
            var eligible=results.Where(p=>p.Row.Candidate==m&&p.Dist is not null&&p.AggregateEligible).ToArray();
            if(eligible.Length==0)
            {
                aggregate.Add(new(m,0,gaps.Count,null,null,null,null,null,null)); continue;
            }
            var seed=1729;
            foreach(var p in eligible) seed=unchecked(seed*31+p.Row.GapIndex);
            var rng=new Random(seed);
            var independent=new double[AggregateDraws];var common=new double[AggregateDraws];
            for(var i=0;i<AggregateDraws;i++)
            {
                var u=rng.NextDouble();
                foreach(var p in eligible)
                {
                    independent[i]+=p.Dist!.Quantile(rng.NextDouble());
                    common[i]+=p.Dist!.Quantile(u);
                }
            }
            Array.Sort(independent); Array.Sort(common);
            aggregate.Add(new(m,eligible.Length,gaps.Count,
                LinearQ(independent,.05),LinearQ(independent,.5),LinearQ(independent,.95),
                LinearQ(common,.05),LinearQ(common,.5),LinearQ(common,.95)));
        }
        return new(gaps,results.Select(x=>x.Row).ToArray(),aggregate);
    }
    private static string Version(string model)=>model switch {"R3"=>R3Version,"R4"=>R4Version,_=>R5Version};
    private static Prediction Missing(ExperimentalGridGap g,string m,string status,int support=0)=>
        new(new(g.Index,m,Version(m),status,support,null,null,null),null,false);
    private static Prediction Result(ExperimentalGridGap g,string m,Distribution d,int support,
        string status="EXPERIMENTAL_PENDING_VALIDATION",bool aggregate=true)=>
        new(new(g.Index,m,Version(m),status,support,d.Quantile(.05),d.Quantile(.5),d.Quantile(.95)),d,aggregate);
    private static bool Weekend(DateTimeOffset t)=>t.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    private static int Group(double minutes)=>minutes<=45?0:minutes<=180?1:2;
    private static bool SameBand(DateTimeOffset a,DateTimeOffset b,int width)=>
        a.Date==b.Date&&b.TimeOfDay.TotalHours<=(a.Hour/width+1)*width;
    private static bool Regular(Sample a,Sample b)=> (b.Utc-a.Utc).TotalMinutes is >=4.5 and <=5.5;
    private static int DayCount(IEnumerable<Window> x)=>x.Select(a=>a.Day).Distinct().Count();

    // R3 takes every complete same-day window; R4 takes every third
    // eligible start in each day/duration/six-hour band. R5 positive
    // residual windows are built separately, with global stride per duration.
    private static List<Window> BuildWindows(Sample[] history,bool r5)
    {
        var result=new List<Window>();
        if(r5)
        {
            foreach(var duration in Durations)
            {
                var k=duration/5; var retained=0;
                for(var i=0;i+k<history.Length;i++)
                {
                    if(history[i].Day!=history[i+k].Day)continue;
                    if(!TryWindow(history,i,k,true,out var h,out var rate))continue;
                    if(retained++%3!=0)continue;
                    var x0=history[i].W;var x1=history[i+k].W;
                    var eps=(rate*1000*k-.5*(x0+x1))/(k-1)-(x0+x1)/2;
                    result.Add(new(history[i].Day,duration,Group(duration),-1,Weekend(history[i].Local),
                        x0>100,2,rate,(x0+x1)/2000,eps));
                }
            }
            return result;
        }
        foreach(var day in history.GroupBy(s=>s.Day))
        {
            var a=day.ToArray(); if(a.Length<280)continue;
            foreach(var duration in Durations)
            {
                var k=duration/5;
                for(var i=0;i+k<a.Length;i++)
                {
                    if(!TryWindow(a,i,k,false,out _,out var rate))continue;
                    var l=a[i];var r=a[i+k];
                    var count=(l.W>0?1:0)+(r.W>0?1:0);
                    result.Add(new(day.Key,duration,Group(duration),SameBand(l.Local,r.Local,6)?l.Local.Hour/6*6:-1,
                        Weekend(l.Local),l.W>100,count,rate,(l.W+r.W)/2000,0));
                }
            }
        }
        return result;
    }
    private static bool TryWindow(Sample[] a,int i,int k,bool positive,out double hours,out double rate)
    {
        hours=0;rate=0;double energy=0;
        if(positive&&a[i].W<=0)return false;
        for(var j=i;j<i+k;j++)
        {
            if(!Regular(a[j],a[j+1])||(positive&&a[j+1].W<=0))return false;
            var h=(a[j+1].Utc-a[j].Utc).TotalHours;
            hours+=h;energy+=(a[j].W+a[j+1].W)*h/2000;
        }
        if(hours<=0)return false;
        rate=energy/hours;return true;
    }
    private static Distribution WeightedDays(IEnumerable<(DateOnly Day,double Value)> input)
    {
        var groups=input.GroupBy(v=>v.Day).ToArray();
        var pairs=groups.SelectMany(g=>g.Select(x=>(x.Value,Weight:1.0/groups.Length/g.Count()))).OrderBy(x=>x.Value).ToArray();
        return Normalize(pairs);
    }
    private static Distribution Normalize(IEnumerable<(double Value,double Weight)> pairs)
    {
        var a=pairs.Where(x=>x.Weight>0).OrderBy(x=>x.Value).ToArray();
        var total=a.Sum(x=>x.Weight);var c=0.0;
        var p=new Weighted[a.Length];
        for(var i=0;i<a.Length;i++){c+=a[i].Weight/total;p[i]=new(a[i].Value,i==a.Length-1?1.0:c);}
        return new(p);
    }
    private static Prediction PredictR3(ExperimentalGridGap gap,List<Window> donors,double h,double x0,double x1)
    {
        var group=Group(h*60);
        var pool=donors.Where(w=>R3Durations.Contains(w.Duration)&&w.Group==group&&w.Active==(x0>100)).ToArray();
        var days=pool.Select(w=>w.Day).Distinct().Order().TakeLast(15).ToHashSet();
        if(days.Count<10)return Missing(gap,"R3","INSUFFICIENT_CALIBRATION_DAYS",days.Count);
        var d=WeightedDays(pool.Where(w=>days.Contains(w.Day)).Select(w=>
            (w.Day,Math.Max(0,((x0+x1)/2000+w.Rate-w.Bridge)*h))));
        return Result(gap,"R3",d,days.Count);
    }
    private static Window[] Choose(Window[] a,int duration,int band,bool weekend,int minimum,bool all)
    {
        var candidates=new[]{a.Where(w=>w.Duration==duration&&w.Band==band&&w.Weekend==weekend),
            a.Where(w=>w.Duration==duration&&w.Band==band),a.Where(w=>w.Duration==duration),
            a.Where(w=>w.Group==Group(duration)),a.AsEnumerable()};
        foreach(var p in candidates.Take(all?5:4))
        {
            var v=p.ToArray();if(DayCount(v)>=minimum)return v;
        }
        return [];
    }
    private static Prediction PredictR4(ExperimentalGridGap gap,List<Window> donors,double h,double x0,double x1,DateTimeOffset start)
    {
        var duration=Durations.OrderBy(d=>Math.Abs(d-h*60)).ThenBy(d=>d).First();
        var band=start.Hour/6*6;var count=(x0>0?1:0)+(x1>0?1:0);
        var windows=donors.Where(w=>w.Band>=0).GroupBy(w=>(w.Day,w.Duration,w.Band))
            .SelectMany(g=>g.Where((_,i)=>i%3==0)).ToArray();
        var recentDays=windows.Select(w=>w.Day).Distinct().Order().TakeLast(60).ToHashSet();
        var full=windows.Where(w=>w.BoundaryCount==count).ToArray();
        var recent=full.Where(w=>recentDays.Contains(w.Day)).ToArray();
        var probability=Choose(recent,duration,band,Weekend(start),20,true);
        if(count==0&&probability.Length==0)return Missing(gap,"R4","INSUFFICIENT_ZERO_PROBABILITY_DAYS");
        double p0=0;
        if(count==0)
            p0=probability.GroupBy(w=>w.Day).Average(g=>g.Count(w=>w.Rate==0)/(double)g.Count());
        var mag=Choose(recent.Where(w=>w.Rate>0).ToArray(),duration,band,Weekend(start),10,false);
        if(mag.Length==0)mag=Choose(full.Where(w=>w.Rate>0).ToArray(),duration,band,Weekend(start),10,true);
        var n=DayCount(mag);
        if(n<10)return Missing(gap,"R4","INSUFFICIENT_POSITIVE_DAYS",n);
        var positive=mag.GroupBy(w=>w.Day).SelectMany(g=>g.Select(w=>(
            Value:Math.Max(1e-12,count==0?w.Rate:w.Rate-w.Bridge+(x0+x1)/2000)*h,
            Weight:(1-p0)/n/g.Count())));
        var dist=Normalize(positive.Append((0.0,p0)));
        return Result(gap,"R4",dist,count==0?Math.Min(DayCount(probability),n):n);
    }
    private static Episode[] Episodes(Sample[] a)
    {
        var episodes=new List<Episode>();
        for(var i=0;i<a.Length;)
        {
            var start=i;var state=a[i].W>0?1:0;
            while(i+1<a.Length&&Regular(a[i],a[i+1])&&(a[i+1].W>0?1:0)==state)i++;
            var left=start==0||!Regular(a[start-1],a[start]);
            var right=i==a.Length-1||!Regular(a[i],a[i+1]);
            episodes.Add(new(start,i,state,left,right));i++;
        }
        return episodes.ToArray();
    }
    private static Prediction PredictR5(ExperimentalGridGap gap,Sample[] history,Sample[] observed,
        double hours,double x0,double x1)
    {
        if(history.Length<2)return Missing(gap,"R5","INSUFFICIENT_HISTORY");
        var episodes=Episodes(history);
        var hazard=new double[2,Cap];var ages=new double[2,Cap];var support=int.MaxValue;
        for(var state=0;state<2;state++)
        {
            var usable=episodes.Where(e=>e.State==state&&!e.Left).ToArray();
            var complete=usable.Where(e=>!e.Right).ToArray();
            support=Math.Min(support,complete.Select(e=>history[e.A].Day).Distinct().Count());
            if(support<10)return Missing(gap,"R5","INSUFFICIENT_COMPLETE_EPISODE_DATES",support);
            var exposure=usable.Sum(e=>e.B-e.A+(!e.Right?1:0));
            var global=(complete.Length+.5)/(exposure+1.0);
            foreach(var e in usable)
            {
                var length=e.B-e.A+1;
                for(var i=0;i<Math.Min(length,Cap);i++)ages[state,i]++;
                ages[state,Cap-1]+=Math.Max(0,length-Cap);
            }
            for(var b=0;b<AgeBins.Length-1;b++)
            {
                var lo=AgeBins[b];var hi=AgeBins[b+1];
                var risk=usable.Sum(e=>Math.Max(0,Math.Min(e.B-e.A+(!e.Right?1:0),hi-1)-lo+1));
                var ev=complete.Count(e=>e.B-e.A+1>=lo&&e.B-e.A+1<hi);
                var p=Math.Clamp((ev+20*global)/(risk+20),1e-7,1-1e-7);
                for(var i=lo;i<hi&&i<=Cap;i++)hazard[state,i-1]=p;
            }
        }
        var positive=episodes.Where(e=>e.State==1&&!e.Left&&e.B-e.A+1>=3).ToArray();
        if(positive.Length==0)return Missing(gap,"R5","INSUFFICIENT_POSITIVE_EPISODES",support);
        var means=WeightedDays(positive.Select(e=>(history[e.A].Day,
            history.Skip(e.A).Take(e.B-e.A+1).Average(s=>s.W))));
        var windows=BuildWindows(history,true).ToArray();
        var residual=windows.Where(w=>w.Duration>180).ToArray();
        if(DayCount(residual)<10)residual=windows;
        support=Math.Min(support,DayCount(residual));
        if(support<10)return Missing(gap,"R5","INSUFFICIENT_RESIDUAL_DATES",support);
        var residualDist=WeightedDays(residual.Select(w=>(w.Day,w.InteriorResidual)));
        var k=Math.Clamp((int)Math.Round(hours*12,MidpointRounding.AwayFromZero),2,96);
        var startState=x0>0?1:0;var endState=x1>0?1:0;
        // Estimate the age of the observed start episode without using
        // any samples after the gap's start.
        var startIndex=Array.FindLastIndex(observed,s=>s.Utc==gap.StartUtc);
        if(startIndex<0)return Missing(gap,"R5","MISSING_OBSERVED_START",support);
        var episodeStart=startIndex;
        while(episodeStart>0&&Regular(observed[episodeStart-1],observed[episodeStart])&&
              (observed[episodeStart-1].W>0?1:0)==startState)episodeStart--;
        var known=episodeStart>0&&Regular(observed[episodeStart-1],observed[episodeStart]);
        var lower=Math.Min(startIndex-episodeStart+1,Cap)-1;
        var f=new double[k+1,2,Cap];
        for(var age=0;age<Cap;age++)f[0,endState,age]=1;
        for(var rem=1;rem<=k;rem++)
            for(var s=0;s<2;s++)
                for(var age=0;age<Cap;age++)
                    f[rem,s,age]=(1-hazard[s,age])*f[rem-1,s,Math.Min(age+1,Cap-1)]
                        +hazard[s,age]*f[rem-1,1-s,0];
        var weights=new double[Cap];var total=0.0;
        for(var age=lower;age<Cap;age++)
            weights[age]=known?(age==lower?1:0):ages[startState,age];
        if(weights.Sum()==0)weights[lower]=1;
        for(var age=0;age<Cap;age++){weights[age]*=f[k,startState,age];total+=weights[age];}
        if(total<=0)return Missing(gap,"R5","INSUFFICIENT_CONDITIONED_STATE",support);
        var rng=new Random(unchecked((int)(gap.StartUtc.UtcDateTime.Ticks ^ gap.EndUtc.UtcDateTime.Ticks)));
        var draws=new double[R5Draws];var path=new int[k+1];
        for(var n=0;n<R5Draws;n++)
        {
            var u=rng.NextDouble()*total;var age=0;
            while(age<Cap-1&&(u-=weights[age])>=0)age++;
            var s=startState;path[0]=s;
            for(var step=0;step<k;step++)
            {
                var rem=k-step;
                var flip=hazard[s,age]*f[rem-1,1-s,0]/Math.Max(f[rem,s,age],1e-300);
                if(rng.NextDouble()<Math.Clamp(flip,0,1)){s=1-s;age=0;}
                else age=Math.Min(age+1,Cap-1);
                path[step+1]=s;
            }
            if(path[k]!=endState)return Missing(gap,"R5","CONDITIONING_NUMERICAL_FAILURE",support);
            var active=path.Skip(1).Take(k-1).Sum();
            var same=path.All(v=>v==1);
            var first=0; if(startState==1)while(first<k-1&&path[first+1]==1)first++;
            var last=0; if(endState==1&&!same)while(last<k-1&&path[k-1-last]==1)last++;
            var middle=active-first-last;
            var eps=residualDist.Quantile(rng.NextDouble());var level=means.Quantile(rng.NextDouble());
            var firstLevel=same?(x0+x1)/2:x0;
            draws[n]=(.5*(x0+x1)+first*Math.Max(0,firstLevel+eps)
                +last*Math.Max(0,x1+eps)+middle*level)*hours/k/1000;
        }
        var d=Normalize(draws.Select(v=>(v,1.0/R5Draws)));
        // Both-zero endpoints are retained for inspection only, excluded
        // from aggregate and explicitly marked as a known structural risk.
        var risky=x0==0&&x1==0;
        return Result(gap,"R5",d,support,risky?"EXPERIMENTAL_BOTH_ZERO_WARNING":
            "EXPERIMENTAL_PENDING_VALIDATION",!risky);
    }
    private static double LinearQ(double[] a,double q)
    {
        var p=(a.Length-1)*q;var lo=(int)Math.Floor(p);var hi=(int)Math.Ceiling(p);
        return a[lo]+(a[hi]-a[lo])*(p-lo);
    }
}
