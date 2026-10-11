using HomeFocus.Shared;

namespace HomeFocus.Agent;

internal sealed class PolicyEngine
{
    readonly PolicyStore store;
    public PolicyEngine(PolicyStore store) => this.store = store;

    public bool HostAllowed(string host)
    {
        var c = store.Config;
        if (!c.Enabled) return true;
        if (DomainMatcher.Matches(host, c.AlwaysAllowedDomains)) return true;
        foreach (var grant in ActiveGrants())
        {
            var group = c.RewardGroups.FirstOrDefault(x => x.Id == grant.RewardGroupId);
            if (group is null) continue;
            if (group.AllowAllInternet || DomainMatcher.Matches(host, group.Domains)) return true;
        }
        return false;
    }

    public IReadOnlyList<AccessGrant> ActiveGrants()
    {
        var now = DateTimeOffset.Now;
        return store.State.Grants.Where(g => g.ExpiresAt > now).OrderBy(g => g.ExpiresAt).ToList();
    }

    public IReadOnlyList<string> ActiveProgramGrants()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var grant in ActiveGrants())
        {
            var group = store.Config.RewardGroups.FirstOrDefault(x => x.Id == grant.RewardGroupId);
            if (group is null) continue;
            foreach (var p in group.ProgramPaths.Where(File.Exists)) paths.Add(p);
        }
        return paths.ToList();
    }

    public AgentStatus Status()
    {
        PruneExpired();
        return new AgentStatus
        {
            Enabled = store.Config.Enabled,
            PinConfigured = !string.IsNullOrWhiteSpace(store.Config.ParentPinHash),
            Now = DateTimeOffset.Now,
            ActiveGrants = ActiveGrants().ToList(),
            Tasks = store.Config.Tasks.Select(GetAvailability).ToList(),
            AvailableVersion = store.State.AvailableVersion,
            UpdateError = store.State.LastUpdateError,
            PassageCollections = store.Config.PassageCollections
        };
    }

    public TaskAvailability GetAvailability(TaskDefinition task)
    {
        var now = DateTimeOffset.Now;
        var today = now.LocalDateTime.Date;
        var records = store.State.Completions.Where(x => x.TaskId == task.Id && x.CompletedAt.LocalDateTime.Date == today).ToList();
        var rewarded = records.Sum(x => x.RewardMinutes);
        if (task.DailyRewardCapMinutes > 0 && rewarded + task.RewardMinutes > task.DailyRewardCapMinutes)
            return new() { Task = task, Available = false, Reason = "Daily limit reached", RewardedTodayMinutes = rewarded };
        var last = records.OrderByDescending(x => x.CompletedAt).FirstOrDefault();
        if (last is not null && task.CooldownMinutes > 0)
        {
            var ready = last.CompletedAt.AddMinutes(task.CooldownMinutes);
            if (ready > now)
                return new() { Task = task, Available = false, Reason = $"Available at {ready.LocalDateTime:t}", RewardedTodayMinutes = rewarded };
        }
        return new() { Task = task, Available = true, Reason = "", RewardedTodayMinutes = rewarded };
    }

    public AccessGrant CompleteTask(string taskId)
    {
        var task = store.Config.Tasks.FirstOrDefault(x => x.Id == taskId) ?? throw new InvalidOperationException("Task not found.");
        var availability = GetAvailability(task);
        if (!availability.Available) throw new InvalidOperationException(availability.Reason);
        if (!store.Config.RewardGroups.Any(x => x.Id == task.RewardGroupId)) throw new InvalidOperationException("Reward group not found.");

        var now = DateTimeOffset.Now;
        var grant = new AccessGrant
        {
            RewardGroupId = task.RewardGroupId,
            StartedAt = now,
            ExpiresAt = now.AddMinutes(Math.Max(1, task.RewardMinutes)),
            SourceTaskId = task.Id
        };
        store.MutateState(s =>
        {
            s.Grants.Add(grant);
            s.Completions.Add(new CompletionRecord { TaskId = task.Id, CompletedAt = now, RewardMinutes = task.RewardMinutes });
        });
        return grant;
    }

    public void EndGrant(string grantId) => store.MutateState(s => s.Grants.RemoveAll(x => x.Id == grantId));

    public void PruneExpired()
    {
        var now = DateTimeOffset.Now;
        if (store.State.Grants.Any(x => x.ExpiresAt <= now))
            store.MutateState(s => s.Grants.RemoveAll(x => x.ExpiresAt <= now));
        var cutoff = now.AddDays(-35);
        if (store.State.Completions.Any(x => x.CompletedAt < cutoff))
            store.MutateState(s => s.Completions.RemoveAll(x => x.CompletedAt < cutoff));
    }
}
