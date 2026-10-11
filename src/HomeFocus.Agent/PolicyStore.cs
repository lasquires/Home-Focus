using System.Text.Json;
using HomeFocus.Shared;

namespace HomeFocus.Agent;

internal sealed class PolicyStore
{
    readonly object gate = new();
    public HomeFocusConfig Config { get; private set; }
    public AgentState State { get; private set; }

    public PolicyStore()
    {
        Directory.CreateDirectory(HomeFocusPaths.ProgramDataDir);
        Config = Load<HomeFocusConfig>(HomeFocusPaths.ConfigPath) ?? HomeFocusConfig.CreateDefault();
        State = Load<AgentState>(HomeFocusPaths.StatePath) ?? new AgentState();
        SaveConfig(); SaveState();
    }

    static T? Load<T>(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonDefaults.Options) : default; }
        catch { return default; }
    }

    public void ReplaceConfig(HomeFocusConfig config)
    {
        lock (gate) { Config = config; SaveConfig(); }
    }

    public void MutateState(Action<AgentState> action)
    {
        lock (gate) { action(State); SaveState(); }
    }

    public void SaveConfig() => AtomicWrite(HomeFocusPaths.ConfigPath, JsonSerializer.Serialize(Config, JsonDefaults.Options));
    public void SaveState() => AtomicWrite(HomeFocusPaths.StatePath, JsonSerializer.Serialize(State, JsonDefaults.Options));

    static void AtomicWrite(string path, string text)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, text);
        File.Move(tmp, path, true);
    }
}
