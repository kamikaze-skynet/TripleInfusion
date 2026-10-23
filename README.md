# TripleInfusion

A [BepInEx](https://github.com/BepInEx/BepInEx) IL2CPP mod for **The Spell Brigade** that lets spells take a third infusion.

Get it on Thunderstore, or see [thunderstore/README.md](thunderstore/README.md) for the player-facing description and config.

## How it works

The game stores a spell's element as a single `ElementType` from a fixed list (5 singles, 5 doubles, 10 pairs), so a third element can't be stored as a new combo. Instead the mod keeps the pair and records the third element per player and spell:

| Step | Hook | What the mod does |
|---|---|---|
| Offer | `InfusionStoreAvailabilityChecker.InfusionStoreShouldBeOffered` (postfix) | At `SpellLevel` or above, if the local spell has two core elements and no third, opens an infusion shop. |
| Shop | `InfusionInventoryGenerator.Generate` (postfix) | Fills that shop with the game's own infusion items for the spell, limited to unlocked core elements. |
| Choose | `PlayerStats.AddInfusion` (prefix) | The game broadcasts infusions to every player (`SendSpellInfusionRpc`), so this runs everywhere. For a spell that already has a pair, records the third element instead of combining. |
| Hit | `ElementEffectExecutor.Execute` (postfix) | For a `PlayerSpellDamageInfo` whose owner and spell have a third element, runs the executor again with that element. |
| Safety | `ElementCombiner.GetElement` (prefix) | Vanilla throws for 3-element lists (the shop card asks for one); answers with the first two instead. |

Data resets when `RunManager` despawns.

## Building

Requires the .NET 6 SDK and a game install with BepInEx IL2CPP 6.0.0-be.788 that has been run once (so `BepInEx/interop` exists).

```powershell
.\package.ps1 -GameDir "D:\SteamLibrary\steamapps\common\The Spell Brigade"
```

This builds `src/TripleInfusion.csproj` and writes `dist/TripleInfusion-<version>.zip`, ready to upload to Thunderstore.

## License

MIT, see [LICENSE](LICENSE).
