using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace TripleInfusion;

/// <summary>
/// A third infusion for spells that already have two elements.
///
/// Vanilla spells hold one ElementType, a fixed list of 20 (5 singles, 5 doubles, 10 pairs), so a spell can only
/// ever have two infusions. This mod keeps the pair as-is and stores a third element per player and spell.
/// On every hit, the pair's effect runs as normal, then the third element's own effect runs on top.
///
/// Everyone should have the mod: element effects run on whichever machine applies the damage (usually the host).
/// </summary>
[BepInPlugin(Guid, Name, Version)]
public class Plugin : BasePlugin
{
    public const string Guid = "girthy_mods.TripleInfusion";
    public const string Name = "TripleInfusion";
    public const string Version = "1.1.1";

    internal static new ManualLogSource Log;

    internal static ConfigEntry<bool> Enabled;
    internal static ConfigEntry<int> ThirdInfusionLevel;
    internal static ConfigEntry<bool> AllowRepeatElements;
    internal static ConfigEntry<int> Choices;

    public override void Load()
    {
        Log = base.Log;

        Enabled = Config.Bind("Third Infusion", "Enabled", true,
            "Offer a third infusion for spells that already have two elements.");
        ThirdInfusionLevel = Config.Bind("Third Infusion", "SpellLevel", 21,
            new ConfigDescription("Spell level for the third infusion (vanilla gives the first at 7 and the second at 14). It's offered at the first level-up at or above this.",
                new AcceptableValueRange<int>(2, 99)));
        AllowRepeatElements = Config.Bind("Third Infusion", "AllowRepeatElements", true,
            "Allow a third element the spell already has (e.g. Fire + Lightning + Fire). Off = only elements the spell doesn't have yet.");
        Choices = Config.Bind("Third Infusion", "Choices", 2,
            new ConfigDescription("How many elements to choose from in the third infusion shop (vanilla infusion shops offer 2).",
                new AcceptableValueRange<int>(1, 5)));

        var harmony = new Harmony(Guid);
        harmony.PatchAll(typeof(InfusionPatches));
        harmony.PatchAll(typeof(HostSettings));
        Diagnostics.Install();
        Log.LogInfo($"{Name} {Version} ready.");
    }
}
