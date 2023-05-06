# UnNerfer

This is a BepInEx mod utilizing HarmonyX to inject custom behavior to [ULTRAKILL](https://devilmayquake.com).

Runtime Dependencies: [wafflethings/UltraTweaker](https://github.com/wafflethings/UltraTweaker)

---

Before building, ensure that:

1. `Libs/BepInEx` is symlinked to `ULTRAKILL/BepInEx/core`
2. `Libs/Managed` is symlinked to `ULTRAKILL/ULTRAKILL_Data/Managed`

Run `dotnet build` to build.

---

## Current Tweaks

* Sharpshooter
    * Bounces - How many times the Sharpshooter Fire2 bounces.
* Alt Marksman
    * Hit Increase - Adds an additional X amount of hits for each coin hit (I think).
* Better Autoaim - Makes coins and grenades the highest priority target for the auto aim.
    * Coin Only - Make the auto aim only target coins.
    * Grenades Only - Make the auto aim only target grenades(Core and Rocket).
        * The "Only" options can be used together.

---

Before you ask, yes, `Better Autoaim` makes railcoining and other coin tricks possible with 100% auto aim.
