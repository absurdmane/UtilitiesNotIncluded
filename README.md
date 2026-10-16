# Utilities Not Included for Obenseuer

A BepInEx 6 (Unity Mono) plugin for **Obenseuer**. Electricity and water in the Tenement come from the upgrades
you actually bought: every device, lamp, fridge and tap looks at the room it stands in and works only as well as
that room's utilities allow. Nothing is written to the game's saves, so the mod is safe to remove.

## What it does

**Electricity**

- Electric workstations, machines, fridges and lamps need power in their room. Fuel-burning devices (furnaces,
  the generator), candles, lanterns and fire don't.
- An apartment needs its own electricity upgrade and a powered building. The basement, workshop, sauna,
  greenhouse and mine run on building power. The stairwell has lights but no outlets (configurable).
- Unpowered lamps really go dark: the light and the glow of the shade, the bulbs, decoration light garlands.
  Switchable lamps keep their switch position.

| Building electricity | Effect |
|---|---|
| None | nothing electric works |
| Shit | works, but electric devices get ×0.85 equipment quality, lights flicker together now and then, the breaker trips from time to time |
| Good | stable power, vanilla balance, no outages |
| Best | industrial equipment (stations with the Industrial trait) works |

- **Outages.** On Shit electricity the breaker trips about once per in-game day. The building grid goes dead
  until you reset the breaker at the electrical panel in the basement. Long processes pause and resume.
- **Mini Generator.** A running generator powers its room (Shit quality) and keeps going through outages.

**Water.** Taps give the water of their room's upgrade level: none, Dirty, Clean, and for Best Pure Water
(configurable). An apartment tap gives the lower of the building's and the apartment's level. No water in the mine.

**Why it doesn't work.** Station panels get a lightning icon: normal when powered (source and tier in the tooltip),
orange on unstable voltage, red with the reason when unpowered.

Objects outside the Tenement are left as in vanilla, and so is anything whose room the mod can't tell.

## Install

1. Install [BepInEx 6 (Unity Mono)](https://builds.bepinex.dev/projects/bepinex_be) into the game folder.
2. Put `UtilitiesNotIncluded.dll` into `BepInEx/plugins/UtilitiesNotIncluded/`.
3. Start the game once; settings appear in `BepInEx/config/obenseuer.absurdmane.utilitiesnotincluded.cfg`.

Works on existing saves. The outage state is kept in a small file of its own next to the save.

## Configuration

| Section | Key | Default | Meaning |
|---|---|---|---|
| Electricity | `IndustrialRequiredPowerLevel` | `Best` | tier industrial stations need |
| | `ShitElectricityQuality` | `0.85` | equipment quality multiplier on Shit power |
| | `LightsNeedPower` | `true` | electric lights go dark without power |
| | `ExtraElectricLights` | `lamp_001, Fluorescent_light, elevator_001_light, lamp_mushroom, desk_lamp_, decoration_lights` | name parts of lights to treat as electric although the game doesn't |
| | `NonElectricLights` | `lantern, mushrooms, Barrelstove, candle, oil_lamp` | name parts of lights never treated as electric; wins over the list above |
| | `FridgesNeedPower` | `true` | fridges stop cooling without power |
| | `StairwellOutlets` | `false` | devices in the stairwell get building power |
| | `GlowingFurniture` | `Decoration Lights, decoration_lights` | furniture that glows without a light source and goes dark without power |
| | `FixLampGlow` | `true` | a switched-off lamp stops glowing on every mesh (fixes the Wall Light's second bulb) |
| | `ShitLightsFlicker` | `true` | lights flicker on Shit power |
| | `FlickersPerMinute` / `FlickerDepth` / `FlickerBurstSeconds` / `FlickerSpeed` | `8` / `0.15` / `0.6` / `30` | how often, how deep, how long and how fast |
| Generator | `PowersDevices` | `true` | a running Mini Generator powers its room |
| | `PowerLevel` | `Shit` | its power tier |
| Outages | `Enabled` / `PerDay` / `MinHoursBetween` | `true` / `1` / `6` | breaker trips on Shit electricity |
| Water | `Enabled` | `true` | taps follow the water upgrades |
| | `BestWater` | `Pure` | what the Best upgrade gives: Clean, Pure or Spring |
| Mod | `Enabled` | `true` | off = the mod does nothing (no patches), as in vanilla; needs a restart |

When reporting a problem, please attach `BepInEx/LogOutput.log` and say where it happened (room, device or lamp)
and your building's electricity and water levels.

## Building

Requirements: .NET SDK 6+ and the game with BepInEx 6 installed.

1. Copy `local.props.example` to `local.props` and set `GameDir` to your Obenseuer folder.
2. `dotnet build UtilitiesNotIncluded -c Release` (add `-p:Deploy=true` to copy the DLL into the game's plugins folder).

## License

MIT © absurdmane, see [LICENSE](LICENSE).
