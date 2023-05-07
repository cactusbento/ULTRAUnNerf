using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UltraTweaker.Handlers;
using UltraTweaker.Tweaks;
using UnityEngine;
using BepInEx.Logging;

namespace Extension.Tweaks
{
    // All the metadata for the tweak. Needed for the mod to find the tweak.
    [TweakMetadata("Alt Marksman", $"{Extension.GUID}.Marksman", "This modifies the Alt Marksman.", $"{Extension.GUID}.ext_page", 0, null, false)]
    public class Marksman : Tweak // All tweaks must inherit `Tweak`.
    {
        private Harmony harmony = new($"{Extension.GUID}.Marksman");
        private static ManualLogSource L;
        // Consts

        // Setting Variables
        protected static int shotAddCount;
		
		// Tweak Working Variables
		protected static GameObject sourceWeapon = null;
		protected static int sourceWeaponVariation;
		protected static bool sourceWeaponAlt;

		protected static int coinsHit;
		
        // Runtime Determined

        // All subsettings must be set in the constructor.
        public Marksman()
        {
        	L = new ManualLogSource("UKUnNerf");

            Subsettings = new()
            {
                { "am_hit_times", 
                	new IntSubsetting(this, new Metadata("Hit Increase", "am_hit_times", "How many hits each coin adds."),
                    new SliderIntSubsettingElement("{0}"), 0, 5, 0) }
            };
        }

        public override void OnTweakEnabled()
        {
            base.OnTweakEnabled();
            BepInEx.Logging.Logger.Sources.Add(L);
            
            shotAddCount = Subsettings["am_hit_times"].GetValue<int>();

            harmony.PatchAll(typeof(MarksmanPatches));
        }

        public override void OnTweakDisabled()
        {
            base.OnTweakDisabled();
            BepInEx.Logging.Logger.Sources.Remove(L);

            harmony.UnpatchSelf();
        }

        // This will update the value when it is changed.
        public override void OnSubsettingUpdate()
        {
            shotAddCount = Subsettings["am_hit_times"].GetValue<int>();
        }

        public class MarksmanPatches
        {
        	// Check if the shot comes from ALT revolver 
        	// Records the data for later use
            [HarmonyPatch(typeof(Revolver), "Shoot"), HarmonyPrefix]
            private static void PreShoot(Revolver __instance) {
				sourceWeapon = __instance.gc.currentWeapon;
				sourceWeaponVariation = __instance.gunVariation;
				sourceWeaponAlt = __instance.altVersion;

				// Gun -> coin 0 
				coinsHit = 0;
				// L.LogInfo("Revolver Fire: " + coinsHit.ToString());
            }

            // When a coin is hit with the sourceWeapon (Alt Marksman),
            // create another beam. (This will create new beams exponentially)
            [HarmonyPatch(typeof(Coin), nameof(Coin.DelayedReflectRevolver)), HarmonyPostfix]
            private static void PostDelayedReflectRevolver(Coin __instance) {
				MonoBehaviour baseInstance = __instance as MonoBehaviour;
				coinsHit++;
				// L.LogInfo("Coin Hit: " + coinsHit.ToString());

				if(__instance.sourceWeapon == sourceWeapon && sourceWeaponVariation == 1 && sourceWeaponAlt) {
					// int hitsAdded = 0;
					int hitsToAdd = (int)Math.Pow(coinsHit, shotAddCount) / 2;

					for (int i = 0; i < hitsToAdd; i++) {
						// hitsAdded = hitsAdded + 1;
						baseInstance.Invoke("ReflectRevolver", 0.1f);
					}

					// L.LogInfo("Hits Added: " + hitsAdded.ToString());
				}
            }
        }
    }
}
