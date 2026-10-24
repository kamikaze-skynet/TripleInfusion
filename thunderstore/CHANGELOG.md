# Changelog

## 1.1.2
- Fix: the remaining "NotImplementedException" errors (found with 1.1.1's logging). They came from lightning ricochets: the third element's effect briefly changed the hit's element, and ricochets read it a moment later when it was back to the pair. The third element now runs on its own copy of the hit.

## 1.1.1
- Fix: more "NotImplementedException" errors with third infusions. Some game code (random element picking) combines elements directly, skipping the path fixed in 1.0.2, so that's covered too.
- If the game still logs that error, the BepInEx log now shows where it came from (first 5 per session).

## 1.1.0
- The host's `[Third Infusion]` settings now apply to everyone in the run (enabled, spell level, repeat elements, choices). Players' own config files are left alone and used again after the run.

## 1.0.2
- Fix: once a spell had a third infusion, the game kept throwing "NotImplementedException" (hundreds of times per run in the log). The game also combines a pair with a new element, such as FireDouble + Fire, and that wasn't covered. Any combination that adds up to 3 or more base elements now keeps the spell's pair.

## 1.0.1
- Fix: the mod didn't load ("Error loading [TripleInfusion]"). It hooked a RunManager method the game doesn't have.
- Fix: the third-infusion shop couldn't read which spell it was for. That field is now read directly.

## 1.0.0
- First release: a third infusion shop at a later spell level (21 by default). The third element's effect stacks on top of the spell's two-element combo.
