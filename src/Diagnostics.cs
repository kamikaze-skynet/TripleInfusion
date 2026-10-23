using System;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace TripleInfusion;

/// <summary>
/// The game logs some errors without saying where they came from. For NotImplementedException (the error the
/// game throws for element combinations it doesn't support) this writes the stack trace to the BepInEx log,
/// a few times per session, so the source can be found.
/// </summary>
internal static class Diagnostics
{
    private static Application.LogCallback callback; // kept alive for IL2CPP
    private static int logged;
    private static float nextAllowed;

    internal static void Install()
    {
        try
        {
            callback = DelegateSupport.ConvertDelegate<Application.LogCallback>(new Action<string, string, LogType>(OnLog));
            Application.add_logMessageReceived(callback);
        }
        catch (Exception ex) { Plugin.Log.LogWarning($"Diagnostics unavailable: {ex.Message}"); }
    }

    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        try
        {
            if (logged >= 5 || (type != LogType.Exception && type != LogType.Error)) return;
            if (condition == null || !condition.Contains("NotImplementedException")) return;
            if (Time.unscaledTime < nextAllowed) return;
            nextAllowed = Time.unscaledTime + 5f;
            logged++;
            Plugin.Log.LogWarning($"Where the game's '{condition}' came from ({logged}/5):\n{stackTrace}");
        }
        catch { }
    }
}
