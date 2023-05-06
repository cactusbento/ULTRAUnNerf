using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UltraTweaker.Handlers;
using UltraTweaker.Tweaks;
using UnityEngine;

namespace Extension.Tweaks
{
    // All the metadata for the tweak. Needed for the mod to find the tweak.
    [TweakMetadata("SharpShooter", $"{Extension.GUID}.sharpshooter", "This modifies the sharpshooter.", $"{Extension.GUID}.ext_page", 0, null, false)]
    public class SharpShooter : Tweak // All tweaks must inherit `Tweak`.
    {
        private Harmony harmony = new($"{Extension.GUID}.sharpshooter");
        // Consts
        protected const int gunVariation = 2;

        // Setting Variables
        protected static int MaxRicochet;
		
		// Setting Working Variables
		protected static float RicochetDivider;
		protected static float ShotCharge;
		
        // Runtime Determined

        // All subsettings must be set in the constructor.
        public SharpShooter()
        {
            Subsettings = new()
            {
                { "max_ricochets", 
                	new IntSubsetting(this, new Metadata("Max Ricochets", "max_ricochets", "Changes how many ricochets you can get."),
                    new SliderIntSubsettingElement("{0}"), 3, 100, 1) }
            };
        }

        public override void OnTweakEnabled()
        {
            base.OnTweakEnabled();
            
            MaxRicochet = Subsettings["max_ricochets"].GetValue<int>();
            RicochetDivider = 100f/ MaxRicochet;
			
            harmony.PatchAll(typeof(SharpShooterPatches));
        }

        public override void OnTweakDisabled()
        {
            base.OnTweakDisabled();
            harmony.UnpatchSelf();
        }

        // This will update the value when it is changed.
        public override void OnSubsettingUpdate()
        {
            MaxRicochet = Subsettings["max_ricochets"].GetValue<int>();
            RicochetDivider = 100f/ MaxRicochet;
        }

        public class SharpShooterPatches
        {
            [HarmonyPatch(typeof(Revolver), "Shoot"), HarmonyPrefix]
            private static void PreShoot(Revolver __instance)
            {
            	RicochetDivider = 100f/ MaxRicochet;
				ShotCharge = __instance.pierceShotCharge;
            }

            [HarmonyPatch(typeof(Revolver), "Shoot"), HarmonyPostfix]
            private static void PostShoot(Revolver __instance) {
				RevolverBeam shot = GameObject.FindObjectOfType<RevolverBeam>();
				
				if (__instance.gunVariation == gunVariation) {
					shot.ricochetAmount = Mathf.Min(MaxRicochet, Mathf.FloorToInt(ShotCharge/RicochetDivider));
				}
            }
        }
    }
}
