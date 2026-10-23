# Changelog

## 1.0.2
- Fix: once a spell had a third infusion, the game kept throwing "NotImplementedException" (hundreds of times per run in the log). The game also combines a pair with a new element, such as FireDouble + Fire, and that wasn't covered. Any combination that adds up to 3 or more base elements now keeps the spell's pair.

## 1.0.1
- Fix: the mod didn't load ("Error loading [TripleInfusion]"). It hooked a RunManager method the game doesn't have.
- Fix: the third-infusion shop couldn't read which spell it was for. That field is now read directly.

## 1.0.0
- First release: a third infusion shop at a later spell level (21 by default). The third element's effect stacks on top of the spell's two-element combo.
