using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;

namespace TripleInfusion;

/// <summary>
/// Names and card text for three-element spells. The game's card for a third infusion reads "Infuse this Corrosion
/// Spell with Ice to make Corrosion" (it only knows two-element combos). For a third-infusion card the text is
/// replaced with the three-element name and what each part does, e.g.
///   Infuse this Corrosion Spell with Ice to make Rimeblight.
///   Corrosion: makes them take more damage, weakens them (they deal less damage).
///   + Ice: slows them more and more.
/// Names go by the three base elements, in any order (Fire + Ice, then Acid = Acid + Ice, then Fire).
/// </summary>
[HarmonyPatch]
internal static class Names
{
    private const string NameColour = "#FFD24A";

    private static readonly Dictionary<string, string> TripleNames = new()
    {
        // three different elements
        [Key(ElementType.Fire, ElementType.Lightning, ElementType.Acid)] = "Cataclysm",
        [Key(ElementType.Fire, ElementType.Lightning, ElementType.Ice)] = "Tempest",
        [Key(ElementType.Fire, ElementType.Lightning, ElementType.Dark)] = "Doomstorm",
        [Key(ElementType.Fire, ElementType.Acid, ElementType.Ice)] = "Calamity",
        [Key(ElementType.Fire, ElementType.Acid, ElementType.Dark)] = "Pandemonium",
        [Key(ElementType.Fire, ElementType.Ice, ElementType.Dark)] = "Eclipse",
        [Key(ElementType.Lightning, ElementType.Acid, ElementType.Ice)] = "Maelstrom",
        [Key(ElementType.Lightning, ElementType.Acid, ElementType.Dark)] = "Scourge",
        [Key(ElementType.Lightning, ElementType.Ice, ElementType.Dark)] = "Oblivion",
        [Key(ElementType.Acid, ElementType.Ice, ElementType.Dark)] = "Pestilence",
        // a doubled element plus one more
        [Key(ElementType.Fire, ElementType.Fire, ElementType.Lightning)] = "Firestorm",
        [Key(ElementType.Fire, ElementType.Fire, ElementType.Acid)] = "Magma",
        [Key(ElementType.Fire, ElementType.Fire, ElementType.Ice)] = "Steamburst",
        [Key(ElementType.Fire, ElementType.Fire, ElementType.Dark)] = "Hellblaze",
        [Key(ElementType.Lightning, ElementType.Lightning, ElementType.Fire)] = "Sunstrike",
        [Key(ElementType.Lightning, ElementType.Lightning, ElementType.Acid)] = "Acid Rain",
        [Key(ElementType.Lightning, ElementType.Lightning, ElementType.Ice)] = "Blizzard",
        [Key(ElementType.Lightning, ElementType.Lightning, ElementType.Dark)] = "Nightstorm",
        [Key(ElementType.Acid, ElementType.Acid, ElementType.Fire)] = "Meltdown",
        [Key(ElementType.Acid, ElementType.Acid, ElementType.Lightning)] = "Overload",
        [Key(ElementType.Acid, ElementType.Acid, ElementType.Ice)] = "Rimeblight",
        [Key(ElementType.Acid, ElementType.Acid, ElementType.Dark)] = "Plague",
        [Key(ElementType.Ice, ElementType.Ice, ElementType.Fire)] = "Shatter",
        [Key(ElementType.Ice, ElementType.Ice, ElementType.Lightning)] = "Hailstorm",
        [Key(ElementType.Ice, ElementType.Ice, ElementType.Acid)] = "Black Ice",
        [Key(ElementType.Ice, ElementType.Ice, ElementType.Dark)] = "Grave Frost",
        [Key(ElementType.Dark, ElementType.Dark, ElementType.Fire)] = "Abyssfire",
        [Key(ElementType.Dark, ElementType.Dark, ElementType.Lightning)] = "Voidstorm",
        [Key(ElementType.Dark, ElementType.Dark, ElementType.Acid)] = "Decay",
        [Key(ElementType.Dark, ElementType.Dark, ElementType.Ice)] = "Deathchill",
    };

    // What one base element does on a hit (from the game's own element tooltips, shortened).
    private static readonly Dictionary<ElementType, string> BaseEffect = new()
    {
        [ElementType.Fire] = "burns them over time",
        [ElementType.Lightning] = "ricochets to nearby foes",
        [ElementType.Acid] = "makes them take more damage",
        [ElementType.Ice] = "slows them more and more",
        [ElementType.Dark] = "turns them against other monsters",
    };

    // The extra a two-element combo adds (the game's tooltips).
    private static readonly Dictionary<ElementType, string> ComboBonus = new()
    {
        [ElementType.FireDouble] = "burns can crit",
        [ElementType.LightningDouble] = "first hit calls down a lightning strike",
        [ElementType.FireLightning] = "ricochets burn too",
        [ElementType.AcidDouble] = "weakens them (they deal less damage)",
        [ElementType.AcidFire] = "first hit splashes nearby foes",
        [ElementType.AcidLightning] = "ricochets carry Acid",
        [ElementType.IceDouble] = "first hit can freeze them for 2 s",
        [ElementType.FireIce] = "first hit knocks them back",
        [ElementType.IceLightning] = "ricochets chill too",
        [ElementType.AcidIce] = "first hit pushes nearby foes away",
        [ElementType.DarkDouble] = "corrupted foes spread Dark",
        [ElementType.DarkFire] = "corrupted foes spread Fire",
        [ElementType.DarkLightning] = "ricochets carry Dark",
        [ElementType.AcidDark] = "corrupted foes spread Acid",
        [ElementType.DarkIce] = "corrupted foes spread Ice",
    };

    private static string Key(params ElementType[] cores) => string.Join("+", cores.Select(c => (int)c).OrderBy(i => i));

    /// <summary>The three-element name for a pair plus a third element, if there is one.</summary>
    internal static string NameFor(List<ElementType> pairCores, ElementType third)
    {
        var all = new List<ElementType>(pairCores) { third };
        return TripleNames.TryGetValue(Key(all.ToArray()), out var n) ? n : null;
    }

    /// <summary>What the pair does: each of its base elements, then the combo's extra.</summary>
    private static string PairEffects(ElementType pair, List<ElementType> cores)
    {
        var parts = new List<string>();
        foreach (var c in cores.Distinct()) if (BaseEffect.TryGetValue(c, out var e)) parts.Add(e);
        if (ComboBonus.TryGetValue(pair, out var b)) parts.Add(b);
        return string.Join(", ", parts);
    }

    // ---------- the shop card ----------

    // Cards are reused between shops: the description is ours only on a third-infusion card, so give it back to the
    // game's own text first.
    [HarmonyPatch(typeof(SpellInfusionCard), nameof(SpellInfusionCard.Setup))]
    [HarmonyPrefix]
    private static void Setup_Prefix(SpellInfusionCard __instance)
    {
        try
        {
            var d = __instance.localizedDescription;
            if (d != null && !d.enabled) d.enabled = true;
        }
        catch { }
    }

    [HarmonyPatch(typeof(SpellInfusionCard), nameof(SpellInfusionCard.Setup))]
    [HarmonyPostfix]
    private static void Setup_Postfix(SpellInfusionCard __instance, StoreItem item)
    {
        if (!HostSettings.Enabled) return;
        try
        {
            var infusion = item?.TryCast<SpellInfusionItem>();
            if (infusion == null || infusion.Configuration == null) return;
            var spell = infusion.Configuration.SpellType;
            if (!InfusionPatches.IsThirdInfusionShop(spell, out var pair, out var cores)) return;
            var third = infusion.Element;
            string name = NameFor(cores, third);
            if (name == null || !BaseEffect.TryGetValue(third, out var thirdEffect)) return;

            string pairName = __instance.FirstElement, pairHex = __instance.FirstElementHexadecimal;
            string thirdName = __instance.SecondElement, thirdHex = __instance.SecondElementHexadecimal;
            if (string.IsNullOrEmpty(pairName)) pairName = pair.ToString();
            if (string.IsNullOrEmpty(thirdName)) thirdName = third.ToString();
            if (string.IsNullOrEmpty(pairHex)) pairHex = "#FFFFFF";
            if (string.IsNullOrEmpty(thirdHex)) thirdHex = "#FFFFFF";

            string text =
                $"Infuse this <b><color={pairHex}>{pairName}</color></b> Spell with <b><color={thirdHex}>{thirdName}</color></b> to make <b><color={NameColour}>{name}</color></b>.\n" +
                $"<size=78%>\n<b><color={pairHex}>{pairName}</color></b>: {PairEffects(pair, cores)}.\n" +
                $"<b><color={thirdHex}>+ {thirdName}</color></b>: {thirdEffect}.</size>";

            var d = __instance.localizedDescription;
            if (d == null) return;
            var tmp = d.GetComponent<TMP_Text>() ?? d.GetComponentInChildren<TMP_Text>(true);
            if (tmp == null) return;
            d.enabled = false; // or the game's text comes back when the string refreshes
            tmp.text = text;
        }
        catch (Exception ex) { Plugin.Log.LogWarning($"Third infusion card text: {ex.Message}"); }
    }
}
