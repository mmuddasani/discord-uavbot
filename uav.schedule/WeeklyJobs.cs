using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace uav.Schedule;

public abstract class WeeklyJobs : Job
{
    protected record JobDescription(DayOfWeek Day, string Name, Func<Task> Action, TimeOnly? Time = null) : IWeeklySchedulable;
    protected abstract ICollection<JobDescription> jobDescriptions { get; }

    protected virtual DateTime Now => DateTime.UtcNow;

    public override string Name => NextJobDescription().Name;

    private JobDescription NextJobDescription()
    {
        return jobDescriptions.NextOccuring(Now);
    }

    public override DateTimeOffset NextJobTime()
    {
        var now = Now;
        var nextJob = NextJobDescription();

        return nextJob.NextTime(now);
    }

    private JobDescription ThisJobDescription()
    {
        var now = Now;
        var nowTimeOnly = TimeOnly.FromDateTime(now);
        return jobDescriptions.LastOrDefault(j => j.HasPassed(now)) ?? jobDescriptions.Last();
    }

    public override Task Run()
    {
        var job = ThisJobDescription();
        return job.Action();
    }
}