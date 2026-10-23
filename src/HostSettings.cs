using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Unity.Collections;
using Unity.Netcode;

namespace TripleInfusion;

/// <summary>
/// The host's [Third Infusion] settings apply to everyone. At run start the host sends them to every client
/// (Netcode named message); clients use them for that run instead of their own config file (which isn't changed).
/// In single player, and on the host, the local config is used.
/// </summary>
[HarmonyPatch]
internal static class HostSettings
{
    private const string MessageName = "girthy_mods.TripleInfusion.Settings";
    private static CustomMessagingManager.HandleNamedMessageDelegate handler; // kept alive for IL2CPP
    private static Dictionary<string, string> synced;

    internal static bool Enabled => synced != null && synced.TryGetValue("Enabled", out var v) && bool.TryParse(v, out var b) ? b : Plugin.Enabled.Value;
    internal static int SpellLevel => Int("SpellLevel", Plugin.ThirdInfusionLevel.Value);
    internal static bool AllowRepeat => synced != null && synced.TryGetValue("AllowRepeat", out var v) && bool.TryParse(v, out var b) ? b : Plugin.AllowRepeatElements.Value;
    internal static int Choices => Int("Choices", Plugin.Choices.Value);

    private static int Int(string key, int local) =>
        synced != null && synced.TryGetValue(key, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : local;

    // Clients: listen as soon as the run's RunManager exists (before the host's message arrives).
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.OnNetworkSpawn))]
    [HarmonyPostfix]
    private static void Listen()
    {
        try
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer || nm.CustomMessagingManager == null) return;
            handler ??= DelegateSupport.ConvertDelegate<CustomMessagingManager.HandleNamedMessageDelegate>(new Action<ulong, FastBufferReader>(OnReceived));
            nm.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, handler);
        }
        catch (Exception ex) { Plugin.Log.LogError($"Host settings (listen): {ex}"); }
    }

    private static void OnReceived(ulong sender, FastBufferReader reader)
    {
        try
        {
            reader.ReadValueSafe(out string payload, false);
            var d = new Dictionary<string, string>();
            foreach (var part in payload.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq > 0) d[part.Substring(0, eq)] = part.Substring(eq + 1);
            }
            synced = d;
            Plugin.Log.LogInfo($"Using the host's third-infusion settings: {payload}");
        }
        catch (Exception ex) { Plugin.Log.LogError($"Host settings (receive): {ex}"); }
    }

    // Host: send when the run starts (execute stage of the server RPC).
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.StartRunOnServerRpc))]
    [HarmonyPrefix]
    private static void StartRun_Prefix(RunManager __instance, out bool __state)
    {
        __state = false;
        try { __state = __instance.__rpc_exec_stage == NetworkBehaviour.__RpcExecStage.Execute; } catch { }
    }

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.StartRunOnServerRpc))]
    [HarmonyPostfix]
    private static void StartRun_Postfix(bool __state)
    {
        if (!__state) return;
        try
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || nm.CustomMessagingManager == null) return;
            var c = CultureInfo.InvariantCulture;
            string payload = string.Join(";",
                "Enabled=" + Plugin.Enabled.Value,
                "SpellLevel=" + Plugin.ThirdInfusionLevel.Value.ToString(c),
                "AllowRepeat=" + Plugin.AllowRepeatElements.Value,
                "Choices=" + Plugin.Choices.Value.ToString(c));
            var writer = new FastBufferWriter(Encoding.UTF8.GetByteCount(payload) * 2 + 64, Allocator.Temp, -1);
            try
            {
                writer.WriteValueSafe(payload, false);
                nm.CustomMessagingManager.SendNamedMessageToAll(MessageName, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
            finally { writer.Dispose(); }
        }
        catch (Exception ex) { Plugin.Log.LogError($"Host settings (send): {ex}"); }
    }

    // Back to the local config after the run.
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.OnDestroy))]
    [HarmonyPostfix]
    private static void Clear() => synced = null;
}
