# Changelog

## 1.1.0

- Power follows a changed upgrade at once: electricity set or removed (by the game or another tool) turns devices, fridges and lamps on or off right away, not after a reload.
- The Styrofoam Cooler keeps cooling without electricity; only fridges and freezers need power.
- TVs need power: without it they don't turn on and one that was on goes dark.
- Grow lamps: a terrarium, aquarium or lit growing box without power grows no faster than a plain bed, and bad power never makes it slower than no power.
- The lighting system was rewritten. Only electric lamps and decoration lights go dark without power, from a fixed list: fires, stoves, candles, lanterns and other glowing things are left alone. Settings `AddLights` / `RemoveLights` replace `ExtraElectricLights`, `NonElectricLights` and `GlowingFurniture`.
- Settings tidied up: regrouped into Devices, Fridges and Lights (Electricity is gone), fine-tuning options for flicker, lamp glow and outage spacing removed. Settings you changed under Electricity in 1.0.0 go back to their defaults.
- New `[Testing] PowerCutKey` (off by default): a key that cuts the building's power or brings it back, to see what works without electricity.
- Fixed: an FPS spike when placing furniture, and glowing mushrooms in a terrarium going dark after a reload.

## 1.0.0

- First release.
