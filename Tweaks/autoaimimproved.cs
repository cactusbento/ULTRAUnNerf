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
    [TweakMetadata("AutoAim", $"{Extension.GUID}.autoaim", "Improves auto aim priority.", $"{Extension.GUID}.ext_page", 2)]
    public class AutoAim : Tweak // All tweaks must inherit `Tweak`.
    {
        private Harmony harmony = new($"{Extension.GUID}.autoaim");
        // Consts

        // Setting Variables
        private static bool coinOnlyMode;
        private static bool coreEjectOnlyMode;
		
		// Setting Working Variables
		
        // Runtime Determined

        // All subsettings must be set in the constructor.
        public AutoAim()
        {
            // Caches the TestIcon from the Assets bundle, so that it doesn't have to be loaded more than once.
            AssetHandler.CacheAsset<Sprite>("TestIcon", Extension.Assets);
            Subsettings = new() {
				{
					"coin_only",
					new BoolSubsetting(this, 
							new Metadata("Coin Only Mode", "coin_only", "Auto Aim only points to coins"),
							new BoolSubsettingElement(), false)
				},
				{
					"core_only",
					new BoolSubsetting(this, 
							new Metadata("Grenade Only Mode", "core_only", "Target core ejects as well/standalone"),
							new BoolSubsettingElement(), false)
				}
            };
        }

        public override void OnTweakEnabled()
        {
            base.OnTweakEnabled();
            coinOnlyMode = Subsettings["coin_only"].GetValue<bool>();
            coreEjectOnlyMode = Subsettings["core_only"].GetValue<bool>();
            harmony.PatchAll(typeof(AutoAimPatches));
        }

        public override void OnTweakDisabled()
        {
            base.OnTweakDisabled();

            harmony.UnpatchSelf();
        }

        public override void OnSubsettingUpdate() {
            coinOnlyMode = Subsettings["coin_only"].GetValue<bool>();
            coreEjectOnlyMode = Subsettings["core_only"].GetValue<bool>();
            Console.WriteLine("Coin Only Mode: "+ coinOnlyMode.ToString());
            Console.WriteLine("Grenade Only Mode: "+ coreEjectOnlyMode.ToString());
        }

        public static class AutoAimPatches
        {

			[HarmonyPatch(typeof(CameraFrustumTargeter), "Update"), HarmonyPrefix]
			public static bool PreUpdate(CameraFrustumTargeter __instance){
				var setter = __instance.GetType().GetMethod("set_CurrentTarget", BindingFlags.NonPublic | BindingFlags.Instance);

				if ((coreEjectOnlyMode == true || coinOnlyMode == true) && __instance.CurrentTarget != null) {
					string tag = __instance.CurrentTarget.tag.ToString();
					Coin actCoin;
					Grenade actGrenade;
					if (!( __instance.CurrentTarget.TryGetComponent<Coin>(out actCoin) || 
								__instance.CurrentTarget.TryGetComponent<Grenade>(out actGrenade) )) {
						setter.Invoke(__instance, new object[]{null});
						Traverse.Create(__instance).Field("IsAutoAimed").SetValue(false);
						return false;
					} else {
						return true;
					}
				} else {
					return true;
				}
			}

        	// This Forces the auto aim to aim at coins only
        	// (Only if there are coins of course.)
            [HarmonyPatch(typeof(CameraFrustumTargeter), "Update"), HarmonyPostfix]
            public static void PostUpdate(CameraFrustumTargeter __instance) {
				var setter = __instance.GetType().GetMethod("set_CurrentTarget", BindingFlags.NonPublic | BindingFlags.Instance);

				
				// Remove non-coin targets if coinOnly
				if ((coinOnlyMode || coreEjectOnlyMode) && __instance.CurrentTarget != null) {
					string currTag = __instance.CurrentTarget.tag.ToString();
					if (!(currTag.Equals("Coin") || currTag.Equals("Grenade")) ) {
						setter.Invoke(__instance, new object[]{null});
						Traverse.Create(__instance).Field("IsAutoAimed").SetValue(false);
					}
				}

				// Create working variables
				float distToXHair = float.PositiveInfinity;
				Collider endCollider = null;

            	// Extract the private variables from the class
            	Collider[] targets = Traverse.Create(__instance).Field("targets").GetValue() as Collider[];
            	Camera camera = Traverse.Create(__instance).Field("camera").GetValue<Camera>();
            	Bounds bounds = Traverse.Create(__instance).Field("bounds").GetValue<Bounds>();
            	float maxHorAim = Traverse.Create(__instance).Field("maxHorAim").GetValue<float>();
				LayerMask occolusionMask = Traverse.Create(__instance).Field("occolusionMask").GetValue<LayerMask>();
				LayerMask mask = Traverse.Create(__instance).Field("mask").GetValue<LayerMask>();

				// Recalc visible targets
				int numTargets = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, targets, __instance.transform.rotation, mask.value);
				
				// Extract coins from the target list into its own list
				// Coin class only applies to active coins 
				// Collider.tag only applies to phys objs
				List<Collider> coinsAndGrenadeColliders = new List<Collider>();
				for (int i = 0; i < numTargets; i++) {
					Coin activeCoin;
					Grenade activeGrenade;
					if ( targets[i].TryGetComponent<Coin>(out activeCoin)  || targets[i].TryGetComponent<Grenade>(out activeGrenade)) {
						coinsAndGrenadeColliders.Add(targets[i]);
					}
				}

				// Do Nothing if there are no coins.
				if (coinsAndGrenadeColliders.Count == 0) {
					return;
				}

				// coinsAndGrenadeColliders.Reverse();
				// RaycastHit[] occulders = Traverse.Create(__instance).Field("occluders").GetValue() as RaycastHit[];

				foreach (Collider targ in coinsAndGrenadeColliders) {
					// foreach(RaycastHit occ in occulders) {
					// 	if (occ.collider != null) {
					// 		Console.WriteLine("LOS broken by: " +
					// 				occ.collider.tag.ToString());
					// 		
					// 		string tag = occ.collider.tag.ToString();
					// 		if (tag.Equals("Enemy")) {
					// 			Console.WriteLine("LOS broken by: " +
					// 					occ.collider.ToString());
					// 		}
					// 		goto END_FOR;
					// 	}
					// }

					Vector3 vec2d = camera.WorldToViewportPoint(targ.bounds.center);
					float cdXH = Vector3.Distance(vec2d, new Vector2(0.5f, 0.5f));
					if (
							vec2d.x <= 0.5f + maxHorAim / 2f &&
							vec2d.x >= 0.5f - maxHorAim / 2f &&
							vec2d.y <= 0.5f + maxHorAim / 2f &&
							vec2d.y >= 0.5f - maxHorAim / 2f &&
							vec2d.z >= 0f && cdXH < distToXHair
						) {

						distToXHair = cdXH;
						endCollider = targ;

					}
					// END_FOR:;
				}

				
				// IF coin exist proceed as normal
				// ELSE, IF coinonlymode, do not aim.
				if (endCollider != null) {
					setter.Invoke(__instance, new object[]{endCollider});
					Traverse.Create(__instance).Field("IsAutoAimed").SetValue(true);
				}
            }
        }
    }
}
