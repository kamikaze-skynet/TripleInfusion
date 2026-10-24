using System;
using System.Collections.Generic;
using HarmonyLib;
using Unity.Netcode;
using UObject = UnityEngine.Object;
using URandom = UnityEngine.Random;
using Il2CppList = Il2CppSystem.Collections.Generic;

namespace TripleInfusion;

/// <summary>
/// The four pieces, each hooked where the game already does the vanilla version:
///  1. Offer:  InfusionStoreAvailabilityChecker.InfusionStoreShouldBeOffered (vanilla: spell level 7 / 14).
///  2. Shop:   InfusionInventoryGenerator.Generate, filled from the game's own infusion items (StoreItemDatabase).
///  3. Choose: PlayerStats.AddInfusion runs on every player's game when someone infuses (the game broadcasts it).
///             Vanilla would combine three elements and throw; we record the third element instead.
///  4. Hit:    ElementEffectExecutor.Execute runs the pair's effect; we run the third element's effect after it.
/// Plus: ElementCombiner.GetElement tolerates 3-element lists (the shop card's tooltip asks for one).
/// </summary>
[HarmonyPatch]
internal static class InfusionPatches
{
    // Third element per (player, spell). Filled on every modded player's game from the game's own infusion broadcast.
    private static readonly Dictionary<(ulong client, SpellType spell), ElementType> third = new();
    // Local spells with a third-infusion shop on the way (so it isn't offered twice before the choice lands).
    private static readonly HashSet<SpellType> pending = new();

    private static Il2CppList.List<StoreItem> allInfusionItems;
    private static PlayerStats localStats;

    private static readonly ElementType[] CoreElements =
        { ElementType.Fire, ElementType.Lightning, ElementType.Acid, ElementType.Ice, ElementType.Dark };

    // ---------- helpers ----------

    private static int CoreCount(ElementType element)
    {
        if (element == ElementType.None) return 0;
        try { return ElementCombiner.GetCoreElements(element)?.Count ?? 0; } catch { return 0; }
    }

    private static List<ElementType> Cores(ElementType element)
    {
        var list = new List<ElementType>();
        try
        {
            var cores = ElementCombiner.GetCoreElements(element);
            if (cores != null) for (int i = 0; i < cores.Count; i++) list.Add(cores[i]);
        }
        catch { }
        return list;
    }

    private static PlayerStats LocalStats
    {
        get
        {
            if (localStats != null && !localStats.WasCollected) return localStats;
            localStats = null;
            try
            {
                var nm = NetworkManager.Singleton;
                bool offline = nm == null || !nm.IsListening;
                foreach (var s in UObject.FindObjectsOfType<PlayerStats>())
                    if (s != null && (s.IsOwner || offline)) { localStats = s; break; }
            }
            catch { }
            return localStats;
        }
    }

    private static ulong LocalClientId
    {
        get { try { return NetworkManager.Singleton?.LocalClientId ?? 0UL; } catch { return 0UL; } }
    }

    internal static bool TryGetThird(ulong client, SpellType spell, out ElementType element) =>
        third.TryGetValue((client, spell), out element);

    // ---------- 1. offer ----------

    [HarmonyPatch(typeof(InfusionStoreAvailabilityChecker), nameof(InfusionStoreAvailabilityChecker.InfusionStoreShouldBeOffered))]
    [HarmonyPostfix]
    private static void ShouldOffer_Postfix(SpellType spell, int spellLevel, ref bool __result)
    {
        if (__result || !HostSettings.Enabled || spellLevel < HostSettings.SpellLevel) return;
        try
        {
            var stats = LocalStats;
            if (stats == null) return;
            if (CoreCount(stats.GetElement(spell)) < 2) return;     // needs both vanilla infusions first
            if (third.ContainsKey((LocalClientId, spell)) || pending.Contains(spell)) return;

            pending.Add(spell);
            __result = true;
            Plugin.Log.LogInfo($"Third infusion offered for {spell} (level {spellLevel}).");
        }
        catch (Exception ex) { Plugin.Log.LogError($"Offer: {ex}"); }
    }

    // ---------- 2. shop ----------

    [HarmonyPatch(typeof(StoreItemDatabase), nameof(StoreItemDatabase.GetSpellInfusionItems))]
    [HarmonyPostfix]
    private static void CacheInfusionItems(Il2CppList.List<StoreItem> __result)
    {
        if (__result != null && __result.Count > 0) allInfusionItems = __result;
    }

    [HarmonyPatch(typeof(InfusionInventoryGenerator), nameof(InfusionInventoryGenerator.Generate))]
    [HarmonyPostfix]
    private static void Generate_Postfix(InfusionInventoryGenerator __instance, ref Il2CppList.List<StoreItem> __result)
    {
        if (!HostSettings.Enabled) return;
        try
        {
            // spell is a SpellType? field: read it raw (the interop getter for nullable fields throws).
            if (!Raw.TryGetNullable4(__instance, "spell", out int spellBits)) return;
            SpellType spell = (SpellType)spellBits;
            if (!pending.Contains(spell)) return; // not a third-infusion shop

            var stats = LocalStats;
            if (stats == null || allInfusionItems == null) return;
            var current = Cores(stats.GetElement(spell));
            if (current.Count < 2) return;

            var unlocks = __instance.elementUnlockMap;
            var options = new List<StoreItem>();
            for (int i = 0; i < allInfusionItems.Count; i++)
            {
                var item = allInfusionItems[i]?.TryCast<SpellInfusionItem>();
                if (item == null || item.Configuration == null || item.Configuration.SpellType != spell) continue;
                ElementType e = item.Element;
                if (Array.IndexOf(CoreElements, e) < 0) continue;                       // base elements only
                if (!HostSettings.AllowRepeat && current.Contains(e)) continue;
                if (unlocks != null && unlocks.ContainsKey(e) && !unlocks[e]) continue;  // respect element unlocks
                if (options.Exists(o => o.TryCast<SpellInfusionItem>().Element == e)) continue;
                options.Add(allInfusionItems[i]);
            }
            if (options.Count == 0)
            {
                Plugin.Log.LogWarning($"Third infusion for {spell}: no infusion items found, keeping the game's shop.");
                pending.Remove(spell);
                return;
            }

            // Random pick, like the vanilla shop.
            for (int i = options.Count - 1; i > 0; i--)
            {
                int j = URandom.Range(0, i + 1);
                (options[i], options[j]) = (options[j], options[i]);
            }
            var result = new Il2CppList.List<StoreItem>();
            for (int i = 0; i < Math.Min(HostSettings.Choices, options.Count); i++) result.Add(options[i]);
            __result = result;
        }
        catch (Exception ex) { Plugin.Log.LogError($"Shop: {ex}"); }
    }

    // ---------- 3. choose (runs on every player's game via the game's own infusion broadcast) ----------

    [HarmonyPatch(typeof(PlayerStats), nameof(PlayerStats.AddInfusion))]
    [HarmonyPrefix]
    private static bool AddInfusion_Prefix(PlayerStats __instance, SpellType spell, ElementType newElement)
    {
        if (!HostSettings.Enabled) return true;
        try
        {
            if (CoreCount(__instance.GetElement(spell)) < 2) return true; // 1st or 2nd infusion: vanilla

            ulong owner = __instance.OwnerClientId;
            third[(owner, spell)] = newElement;
            if (__instance.IsOwner) pending.Remove(spell);
            Plugin.Log.LogInfo($"Third infusion: player {owner} {spell} + {newElement} (now {__instance.GetElement(spell)} + {newElement}).");
            return false; // keep the pair as the spell's element
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Choose: {ex}");
            return true;
        }
    }

    // The game combines elements as a list like [current pair, new element], e.g. [FireDouble, Fire] for the
    // third-infusion shop card, and in other places once a spell has a third element. That's 3 base elements, so
    // vanilla throws NotImplementedException. If the list adds up to 3+ base elements, answer with the spell's
    // pair (the first element that is already a pair), else with the first two base elements combined.
    [ThreadStatic] private static bool combining;
    private static readonly Dictionary<ElementType, List<ElementType>> coreCache = new();

    // Base elements of an element (Fire -> [Fire], FireDouble -> [Fire, Fire]); 20 element types, cached.
    private static List<ElementType> CachedCores(ElementType e)
    {
        if (coreCache.TryGetValue(e, out var c)) return c;
        c = e == ElementType.None ? new List<ElementType>() : Cores(e);
        if (c.Count == 0 && e != ElementType.None) c.Add(e);
        coreCache[e] = c;
        return c;
    }

    [HarmonyPatch(typeof(ElementCombiner), nameof(ElementCombiner.GetElement))]
    [HarmonyPrefix]
    private static bool GetElement_Prefix(Il2CppList.List<ElementType> elements, ref ElementType __result) =>
        Tolerant(elements, ref __result);

    // Some game code (e.g. RandomElementDeterminer) calls GetCombinedElement directly, skipping GetElement.
    [HarmonyPatch(typeof(ElementCombiner), nameof(ElementCombiner.GetCombinedElement))]
    [HarmonyPrefix]
    private static bool GetCombinedElement_Prefix(Il2CppList.List<ElementType> elements, ref ElementType __result) =>
        Tolerant(elements, ref __result);

    /// <summary>False (with a result) when the list adds up to 3+ base elements; true = let the game handle it.</summary>
    private static bool Tolerant(Il2CppList.List<ElementType> elements, ref ElementType __result)
    {
        if (combining || elements == null || elements.Count < 2) return true;
        combining = true;
        try
        {
            // Fast path (runs often): add up base elements from the cache, no allocations.
            int total = 0;
            for (int i = 0; i < elements.Count; i++) total += CachedCores(elements[i]).Count;
            if (total < 3) return true; // vanilla can handle it

            ElementType firstPair = ElementType.None;
            ElementType c0 = ElementType.None, c1 = ElementType.None;
            int n = 0;
            for (int i = 0; i < elements.Count; i++)
            {
                var c = CachedCores(elements[i]);
                if (c.Count >= 2 && firstPair == ElementType.None) firstPair = elements[i];
                foreach (var core in c) { if (n == 0) c0 = core; else if (n == 1) c1 = core; n++; }
            }
            if (firstPair != ElementType.None) { __result = firstPair; return false; }
            var firstTwo = new Il2CppList.List<ElementType>();
            firstTwo.Add(c0);
            firstTwo.Add(c1);
            __result = ElementCombiner.GetElement(firstTwo);
            return false;
        }
        catch
        {
            __result = elements[0];
            return false;
        }
        finally { combining = false; }
    }

    // ---------- 4. hit: run the third element's effect after the pair's ----------

    [ThreadStatic] private static bool applyingThird;

    [HarmonyPatch(typeof(ElementEffectExecutor), nameof(ElementEffectExecutor.Execute))]
    [HarmonyPostfix]
    private static void Execute_Postfix(ElementEffectExecutor __instance, float damage, DamageInfo damageInfo, HealthContainer targetHealthContainer)
    {
        if (applyingThird || damageInfo == null || third.Count == 0) return;
        try
        {
            var spellHit = damageInfo.TryCast<PlayerSpellDamageInfo>();
            if (spellHit == null) return;
            if (!third.TryGetValue((spellHit.OwnerClientId, spellHit.Spell), out ElementType extra)) return;
            if (targetHealthContainer == null || targetHealthContainer.WasCollected) return;

            // Run the third element on its own copy of the hit. Effects can hold on to the hit and use it later
            // (lightning ricochets read its element a moment after), so the original must never be changed.
            var copy = damageInfo.ShallowClone();
            if (copy == null) return;
            copy.Element = extra;
            applyingThird = true;
            try { __instance.Execute(damage, copy, targetHealthContainer); }
            finally { applyingThird = false; }
        }
        catch (Exception ex) { Plugin.Log.LogError($"Hit: {ex}"); }
    }

    // ---------- reset between runs ----------

    [HarmonyPatch(typeof(RunManager), nameof(RunManager.OnDestroy))] // RunManager has no OnNetworkDespawn of its own
    [HarmonyPostfix]
    private static void Reset()
    {
        third.Clear();
        pending.Clear();
        localStats = null;
    }
}
