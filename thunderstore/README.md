# TripleInfusion

Spells can be infused with up to **three** elements.

In the base game a spell gets an infusion at level 7 and a second one at level 14, and that's the limit. With this mod, once a spell has both infusions, a later level-up (level 21 by default) opens one more infusion shop for that spell.

## How it works

- You pick a third element (Fire, Lightning, Acid, Ice or Dark) from the shop, just like a normal infusion.
- The spell keeps its two-element combo, and the third element's own effect is added **on top** on every hit. For example, Fire + Lightning + Ice burns and shocks like Fire + Lightning, and also chills like Ice.
- You can pick an element the spell already has (Fire + Lightning + Fire) to double up on it. You can turn this off in the config.
- Locked elements stay locked: the shop only offers elements you've unlocked.

## Multiplayer

**Everyone in the lobby should have this mod.** Element effects are applied by whichever game handles the hit, so a player without the mod won't see or apply third elements.

Works with MoreWizards and StarterSpells.

## Config

**The host's settings apply to everyone.** At the start of each run the host's `[Third Infusion]` settings are sent to all players, so everyone gets the third infusion at the same spell level with the same choices.

In your mod manager's config editor (or `BepInEx/config`), section `[Third Infusion]`:

| Setting | Default | What it does |
|---|---|---|
| Enabled | true | Turn the third infusion on or off. |
| SpellLevel | 21 | Spell level for the third infusion shop (vanilla infusions are at 7 and 14). It's offered at the first level-up at or above this. |
| AllowRepeatElements | true | Allow a third element the spell already has. |
| Choices | 2 | How many elements the shop offers (1–5). |

## Known limits

- The spell's look and icon still show the two-element combo. The third element shows in its hits, not its visuals.
- In the third infusion shop, the card's preview shows the current combo rather than all three elements.
- The third infusion doesn't count as an "infusion" for artifacts or challenges that track infusions.
