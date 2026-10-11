using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeFocus.Shared;

public enum TaskKind { Ice, Typing }

public sealed class HomeFocusConfig
{
    public int SchemaVersion { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public string ParentPinSalt { get; set; } = "";
    public string ParentPinHash { get; set; } = "";
    public List<string> AlwaysAllowedDomains { get; set; } = new();
    public List<RewardGroup> RewardGroups { get; set; } = new();
    public List<TaskDefinition> Tasks { get; set; } = new();
    public List<PassageCollection> PassageCollections { get; set; } = new();
    public UpdateSettings Updates { get; set; } = new();
    public bool AllowLocalNetwork { get; set; } = false;

    public static HomeFocusConfig CreateDefault() => new()
    {
        Enabled = true,
        AlwaysAllowedDomains = new List<string>
        {
            "login.microsoftonline.com"
        },
        RewardGroups = new List<RewardGroup>
        {
            new() {
                Id = "youtube",
                Name = "YouTube",
                Domains = new List<string> {
                    "youtube.com", "youtu.be", "ytimg.com", "googlevideo.com",
                    "youtubei.googleapis.com", "youtube.googleapis.com"
                }
            }
        },
        PassageCollections = new List<PassageCollection>
        {
            new() {
                Id = "mindful",
                Name = "Mindful Use",
                Passages = new List<string> {
                    "I am choosing what I want to do before I open the internet, and I will stop when that purpose is complete.",
                    "A few intentional minutes are better than an hour I did not mean to spend. I can use this time on purpose.",
                    "I can enjoy this computer without letting every impulse decide what I do next."
                }
            },
            new() {
                Id = "scripture",
                Name = "Scripture Mastery",
                Passages = new List<string> {
                    "Add your own scripture mastery passages in Parent Settings.",
                    "Each passage is chosen randomly from the collection you create."
                }
            }
        },
        Tasks = new List<TaskDefinition>
        {
            new() {
                Id = "ice-youtube-10",
                Name = "Chip the Ice",
                Kind = TaskKind.Ice,
                RewardGroupId = "youtube",
                RewardMinutes = 10,
                IceClicks = 100,
                CooldownMinutes = 0,
                DailyRewardCapMinutes = 60
            },
            new() {
                Id = "typing-youtube-20",
                Name = "Scripture Typing",
                Kind = TaskKind.Typing,
                RewardGroupId = "youtube",
                RewardMinutes = 20,
                PassageCollectionId = "scripture",
                PassagesRequired = 2,
                CooldownMinutes = 0,
                DailyRewardCapMinutes = 60
            }
        }
    };
}

public sealed class RewardGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New reward";
    public List<string> Domains { get; set; } = new();
    public bool AllowAllInternet { get; set; } = false;
    public List<string> ProgramPaths { get; set; } = new();
}

public sealed class TaskDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New task";
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TaskKind Kind { get; set; } = TaskKind.Ice;
    public string RewardGroupId { get; set; } = "";
    public int RewardMinutes { get; set; } = 10;
    public int CooldownMinutes { get; set; } = 0;
    public int DailyRewardCapMinutes { get; set; } = 120;
    public int IceClicks { get; set; } = 100;
    public string PassageCollectionId { get; set; } = "mindful";
    public int PassagesRequired { get; set; } = 1;
}

public sealed class PassageCollection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New collection";
    public List<string> Passages { get; set; } = new();
}

public sealed class UpdateSettings
{
    public bool Enabled { get; set; } = true;
    public bool AutoInstall { get; set; } = true;
    public int CheckEveryHours { get; set; } = 6;
    public string ManifestUrl { get; set; } = "https://raw.githubusercontent.com/lasquires/Home-Focus/main/update-manifest.json";
}

public sealed class AccessGrant
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string RewardGroupId { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string SourceTaskId { get; set; } = "";
}

public sealed class CompletionRecord
{
    public string TaskId { get; set; } = "";
    public DateTimeOffset CompletedAt { get; set; }
    public int RewardMinutes { get; set; }
}

public sealed class AgentState
{
    public List<AccessGrant> Grants { get; set; } = new();
    public List<CompletionRecord> Completions { get; set; } = new();
    public DateTimeOffset? LastUpdateCheck { get; set; }
    public string? LastUpdateError { get; set; }
    public string? AvailableVersion { get; set; }
}

public sealed class AgentStatus
{
    public bool Enabled { get; set; }
    public bool PinConfigured { get; set; }
    public DateTimeOffset Now { get; set; } = DateTimeOffset.Now;
    public List<AccessGrant> ActiveGrants { get; set; } = new();
    public List<TaskAvailability> Tasks { get; set; } = new();
    public string? AvailableVersion { get; set; }
    public string? UpdateError { get; set; }
    public List<PassageCollection> PassageCollections { get; set; } = new();
}

public sealed class TaskAvailability
{
    public TaskDefinition Task { get; set; } = new();
    public bool Available { get; set; }
    public string Reason { get; set; } = "";
    public int RewardedTodayMinutes { get; set; }
}

public sealed class AgentRequest
{
    public string Type { get; set; } = "";
    public string? Pin { get; set; }
    public string? TaskId { get; set; }
    public string? GrantId { get; set; }
    public HomeFocusConfig? Config { get; set; }
}

public sealed class AgentResponse
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public AgentStatus? Status { get; set; }
    public HomeFocusConfig? Config { get; set; }
    public AccessGrant? Grant { get; set; }
}

public sealed class UpdateManifest
{
    public string Version { get; set; } = "";
    public string PackageUrl { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string? Notes { get; set; }
}

public static class PinHasher
{
    public static (string Salt, string Hash) Create(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, 120_000, HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public static bool Verify(string pin, string salt64, string hash64)
    {
        try
        {
            var salt = Convert.FromBase64String(salt64);
            var expected = Convert.FromBase64String(hash64);
            var actual = Rfc2898DeriveBytes.Pbkdf2(pin, salt, 120_000, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch { return false; }
    }
}

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
}

public static class DomainMatcher
{
    public static string Normalize(string domain)
    {
        domain = domain.Trim().TrimEnd('.').ToLowerInvariant();
        if (Uri.TryCreate(domain, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            domain = uri.Host.ToLowerInvariant();
        if (domain.StartsWith("*.")) domain = domain[2..];
        return domain;
    }

    public static bool Matches(string host, IEnumerable<string> rules)
    {
        host = Normalize(host);
        foreach (var raw in rules)
        {
            var rule = Normalize(raw);
            if (string.IsNullOrWhiteSpace(rule)) continue;
            if (host == rule || host.EndsWith("." + rule, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
