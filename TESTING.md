# Utilities Not Included — test plan

Build and install: `dotnet build -c Release -p:Deploy=true` in `UtilitiesNotIncluded/UtilitiesNotIncluded/`.
Log: `BepInEx\LogOutput.log` — `Utilities Not Included 0.1.0 loaded`, and any error mentioning `UtilitiesNotIncluded`.
Config: `BepInEx\config\obenseuer.absurdmane.utilitiesnotincluded.cfg`.

v0.1.0 was written before any runtime dump: room detection and taps are educated guesses. Run the DevDump (F9) in the same save
and send it along with the results — most FAILs will be explained by it.

DevMenu (F9) makes this quick: set levels in its Tenement tab, then press "Refresh power now" in its Mods tab (UNI section)
or use Tools → "Reload game". The Inspect key (DevMenu config, none by default) on an object saves its path and components to `BepInEx\DevMenu\inspect-latest.txt`.

Mark each line: OK / FAIL + a note. No polling: power is re-evaluated 2 frames after a level loads (plus a safety pass 1 s later), and right after furniture is placed/taken,
a generator starts/stops, or the config changes.

## 0. Startup

- [ ] Log has `loaded`, no errors. Config has sections Electricity, Generator, Water.
- [ ] Outside the Tenement (other levels, street) everything is vanilla.

## 1. Electricity — devices (needs a save without electricity, and one with)

- [ ] No building electricity: an electric station in the apartment (stove, workbench, ...) doesn't craft; the power icon flashes (vanilla).
- [ ] Its panel shows a **red lightning** icon; tooltip "No power" + reason ("The building has no electricity" / "The apartment has no electricity").
- [ ] Building Shit + apartment On: the station works. Panel: normal lightning "Power: building grid (Shit)" + orange "Unstable voltage".
- [ ] The Efficiency icon on Shit is ~15% lower than before the mod (compare with the mod removed or `ShitElectricityQuality = 1`).
- [ ] Building Good: normal lightning, no orange icon, efficiency as vanilla.
- [ ] Fuel devices (furnace, Mini Generator, ...) are not affected and show no lightning.
- [ ] A long process (brewing/distilling with power) pauses when power goes away and resumes when it's back.
- [ ] Industrial station (lathe / press — whichever has the Industrial icon) below Best: red lightning, "Industrial equipment needs Best electricity", doesn't work.
- [ ] Basement devices: work on building electricity alone (apartment electricity doesn't matter).
- [ ] Stairwell: a device placed there (if the game allows) has no power; tooltip "No outlets in the stairwell".
- [ ] Shop (#2): note whether it follows its own upgrades or the building's.
- [ ] Mine: a device built there runs on building electricity (current rule); note if that feels wrong.
- [ ] Mine: a tap there gives no water (decided: no pipes in the mine).
- [ ] Unpowered device: its power button doesn't turn blue and plays no power-on sound; the cable icon flashes red. With the switch
      left "on", the device starts by itself (button blue) as soon as power comes (grid or generator).

## 1a. Tenants' apartments

- [ ] A tenant's apartment with electricity Off: its lamps are dark; with On (and building power) they light. Outage: dark.
- [ ] Its tap follows the apartment's water level. Residents still behave normally (move in, pay, complain as before).

## 2. Generator

- [ ] Run a Mini Generator in the apartment: every electric device and lamp of the apartment gets power, "Power: Mini Generator (Shit)",
      wherever it stands in the apartment. The stairwell / basement don't.
- [ ] Generator in the basement: the whole basement is powered, the apartment isn't.
- [ ] The biogas compressor in the basement, if running, powers nothing.
- [ ] Fuel runs out → the devices lose power within a second.
- [ ] Move a running generator: the devices keep / get power within a second (fix: refresh when it restores its fuel).

## 2a. Outages (Shit electricity)

- [ ] DevMenu → Mods → Utilities Not Included → "trip the breaker": notification "Power outage"; devices, lamps, fridges of the
      whole building go off (apartment, basement, stairwell). A running generator's room keeps power. Status line says DOWN.
- [ ] Device panel tooltip: "Power outage: reset the breaker at the electrical panel in the basement".
- [ ] Basement panel (smallelectricalbox): looking at it shows "Electrical panel (Reset the breaker)"; use it → "Power restored",
      everything comes back. With power on, the panel shows no prompt.
- [ ] Save during an outage, load: still out. Reset, save, load: power on. (Check also with EvenFasterSaveAndLoad.)
- [ ] Building electricity Good/Best: "trip" does nothing; upgrading during an outage ends it.
- [ ] Natural outages: with `PerDay = 10`, `MinHoursBetween = 0` and time ×60 (DevMenu Tools) one comes within a few in-game hours,
      not in the first 10 in-game minutes after loading. Default settings: about one per in-game day, never closer than 6 h.

## 3. Fridges

- [ ] Without power the fridge panel has no snowflake; food spoils at room speed. With power: snowflake back.
- [ ] Remove the mod (delete the dll), load the same save: the fridge cools (nothing stuck off).

## 4. Lights

- [ ] Without power electric lamps in the apartment are off; candles/fire still burn. With power they come back.
- [ ] Built-in lamps of the apartment (e.g. `lamp_008_toggle` of the walls upgrade) go off without power and come back with it
      (fix 2026-10-16: they were seen as outside). Note any lamp that still stays on (inspect it).
- [ ] Built-in devices and taps of the apartment upgrades now follow the apartment's power/water too: note anything odd.
- [ ] After loading a save without power: lamps don't flash on and devices don't hum briefly as the loading screen fades.
- [ ] Walking between rooms/floors doesn't turn unpowered lamps back on.
- [ ] Without power the shade/bulb doesn't look lit: lamps without a switch (Ceiling Light, built-in ceiling lights) and switch lamps.
      With power back they glow again; a switch lamp turned off by its switch stays dark.
- [ ] Stairwell (Decent, Fine) and the roof greenhouse (Fine) during an outage: `lamp_001` and `Fluorescent_light_001` lamps go dark
      too (light and shade), via `ExtraElectricLights`.
- [ ] Tenant apartment with Makeshift walls during an outage: the red kerosene lantern keeps glowing (`NonElectricLights`).
- [ ] Wall Light: switched off, and unpowered during an outage, both bulbs go dark; switched back on, both glow (`FixLampGlow`).
- [ ] Decoration lights (all three sizes) go dark during an outage and come back (animated ones resume their pattern) after the reset (`GlowingFurniture`).
- [ ] Shit building electricity: lamps flicker now and then in short bursts, all in sync; DevMenu UNI status shows "flickering N". With Good power or `ShitLightsFlicker` off they are steady and at full brightness (no leftover dimming).
- [ ] Flip a lamp's switch while unpowered: the light stays off and the shade stays dark. Power back: it lights up as switched.
- [ ] Nothing else near a lamp goes dark (screens, signs, other glowing things in the same furniture / parent).

## 5. Water

- [ ] No water upgrade: the apartment sink gives nothing (can't fill a bottle).
- [ ] Dirty level → Dirty Water; Good → Clean Water; Best → Pure Water (config `BestWater`; Best may not be in the game yet).
- [ ] Both tap kinds: a sink that pours from the scene source, and one with its own water (Commercial Kitchen Table With Sink,
      always Clean in vanilla).
- [ ] Apartment water lower than building: the sink gives the apartment's level.
- [ ] The sink's name/label shows the right liquid.
- [ ] Taps outside the Tenement are unchanged.
- [ ] Save, load: still correct. No new objects / errors after saving (the stand-in sources must not be saved).

## 6. Performance

- [ ] FPS in the apartment and the stairwell with the mod vs without (same spot, same save). Note the difference.
- [ ] No periodic hitches (the mod doesn't poll). A short one right after placing furniture is expected.
- [ ] Busy scene (many placed devices / storages): no stutter when opening station panels or the sink.

## 7. Other mods

- [ ] Unclicker: clicking a recipe on an unpowered station does nothing.
- [ ] EvenFasterSaveAndLoad: save/load works as before.
