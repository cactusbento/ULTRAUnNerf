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
		private static uint targetBitFlag = 0b000;
			// 0b0001 = coin
			// 0b0010 = core
			// 0b0011 = coin & core
		private static bool isVisible = false;
		
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

        public static void updateBitFlags() {
			if (coinOnlyMode) {
				targetBitFlag = targetBitFlag | 0b0001;
			} else {
				targetBitFlag = targetBitFlag & 0b1110;
			}

			if (coreEjectOnlyMode) {
				targetBitFlag = targetBitFlag | 0b0010;
			} else {
				targetBitFlag = targetBitFlag & 0b1101;
			}

        }

        public override void OnTweakEnabled()
        {
            base.OnTweakEnabled();
            coinOnlyMode = Subsettings["coin_only"].GetValue<bool>();
            coreEjectOnlyMode = Subsettings["core_only"].GetValue<bool>();

			updateBitFlags();

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
            updateBitFlags();
            Console.WriteLine("targetBitFlag: " + Convert.ToString(targetBitFlag, 2));
            Console.WriteLine("Coin Only Mode: "+ coinOnlyMode.ToString());
            Console.WriteLine("Grenade Only Mode: "+ coreEjectOnlyMode.ToString());
        }

        public static class AutoAimPatches
        {

			[HarmonyPatch(typeof(CameraFrustumTargeter), "Update"), HarmonyPrefix]
			public static bool PreUpdate(CameraFrustumTargeter __instance){
				var setterCT = __instance.GetType().GetMethod("set_CurrentTarget", BindingFlags.NonPublic | BindingFlags.Instance);
				var setterAA = __instance.GetType().GetMethod("set_IsAutoAimed", BindingFlags.NonPublic | BindingFlags.Instance);

				// Coin | Core 
				//    0 | 0    Do Nothing 
				//    0 | 1    Stop Aiming at Enemy / coin
				//    1 | 0    Stop Aiming at Enemy / grenade
				//    1 | 1    Stop Aiming at Enemy
				if (__instance.CurrentTarget != null) {
					Coin actCoin = null;
					Grenade actGrenade = null;

					bool gotCoin = __instance.CurrentTarget.TryGetComponent<Coin>(out actCoin);
					bool gotNade = __instance.CurrentTarget.TryGetComponent<Grenade>(out actGrenade);

					//If Coin is hanging stop looking at it
					// RemoveOnTime tmpChk;
					// if (gotCoin && __instance.CurrentTarget.TryGetComponent<RemoveOnTime>(out tmpChk) ) {
					// 	setter.Invoke(__instance, new object[]{null});
					// 	Traverse.Create(__instance).Field("IsAutoAimed").SetValue(false);
					// 	return false;
					// }
					
					// Remove Non-Coin
					if (targetBitFlag == 0b0001 && !gotCoin) {
						// Console.WriteLine("[PREFIX ] Removing Non-Coin");
						setterCT.Invoke(__instance, new object[]{null});
						setterAA.Invoke(__instance, new object[]{false});
						return false;
					}

					// Remove Non-Nade
					if (targetBitFlag == 0b0010 && !gotNade) {
						// Console.WriteLine("[PREFIX ] Removing Non-Nade");
						setterCT.Invoke(__instance, new object[]{null});
						setterAA.Invoke(__instance, new object[]{false});
						return false;
					}

					// Remove Non-Coin and Non-Nade
					if ( (targetBitFlag != 0b0000) && !gotNade && !gotCoin) {
						// Console.WriteLine("[PREFIX ] Removing Non-Coin and Non-Nade");
						setterCT.Invoke(__instance, new object[]{null});
						setterAA.Invoke(__instance, new object[]{false});
						return false;
					}
				
					// IF coin, and coin velocity == 0; Cull it
					if ( (gotCoin || gotNade) && __instance.CurrentTarget != null) {
						Rigidbody rb = __instance.CurrentTarget.GetComponent<Rigidbody>();
						bool isStill = rb.velocity.Equals(Vector3.zero);
						if (isStill) {
							setterCT.Invoke(__instance, new object[]{null});
							setterAA.Invoke(__instance, new object[]{false});
						}

						return true;
					}


					return false;
				} else {
					return true;
				}
			}

        	// This Forces the auto aim to aim at coins only
        	// (Only if there are coins of course.)
            [HarmonyPatch(typeof(CameraFrustumTargeter), "Update"), HarmonyPostfix]
            public static void PostUpdate(CameraFrustumTargeter __instance) {
				var setterCT = __instance.GetType().GetMethod("set_CurrentTarget", BindingFlags.NonPublic | BindingFlags.Instance);
				var setterAA = __instance.GetType().GetMethod("set_IsAutoAimed", BindingFlags.NonPublic | BindingFlags.Instance);

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
				
				// Remove non-coin/core targets if coinOnly / coreOnly
				if (__instance.CurrentTarget != null) {
					Coin actCoin = null;
					Grenade actGrenade = null;


					bool gotCoin = __instance.CurrentTarget.TryGetComponent<Coin>(out actCoin);
					bool gotNade = __instance.CurrentTarget.TryGetComponent<Grenade>(out actGrenade);

					// Remove Non-Coin
					if (targetBitFlag == 0b0001 && !gotCoin) {
						// Console.WriteLine("[POSTFIX] Removing Non-Coin");
						setterCT.Invoke(__instance, new object[]{null});
						setterAA.Invoke(__instance, new object[]{false});
					} 

					// Remove Non-Nade
					if (targetBitFlag == 0b0010 && !gotNade) {
						// Console.WriteLine("[POSTFIX] Removing Non-Coin and Non-Nade");
						setterCT.Invoke(__instance, new object[]{null});
						setterAA.Invoke(__instance, new object[]{false});
					} 

					// Remove Non-Coin and Non-Nade
					if (targetBitFlag != 0b0000 && !gotCoin && !gotNade) {
						// Console.WriteLine("[POSTFIX] Removing Non-Coin and Non-Nade");
						setterCT.Invoke(__instance, new object[]{null});
						setterAA.Invoke(__instance, new object[]{false});
					}

					// IF coin, and coin velocity == 0; Cull it
					if (__instance.CurrentTarget != null) {
						if (gotCoin || gotNade) {
							Rigidbody rb = __instance.CurrentTarget.GetComponent<Rigidbody>();
							bool isStill = rb.velocity.Equals(Vector3.zero);
							if (isStill) {
								setterCT.Invoke(__instance, new object[]{null});
								setterAA.Invoke(__instance, new object[]{false});
							}
						} else {
							Vector3 vec2d = camera.WorldToViewportPoint(__instance.CurrentTarget.bounds.center);
							if (!(
									vec2d.x <= 0.5f + maxHorAim / 2f &&
									vec2d.x >= 0.5f - maxHorAim / 2f &&
									vec2d.y <= 0.5f + maxHorAim / 2f &&
									vec2d.y >= 0.5f - maxHorAim / 2f &&
									vec2d.z >= 0f
								)) {
								isVisible = false;
							} else {
								isVisible = true;
							}
							if (!isVisible) {
								setterCT.Invoke(__instance, new object[]{null});
								setterAA.Invoke(__instance, new object[]{false});
							}

						}
					}


					// Stop locking onto coins that are inactive
					// Console.WriteLine("CurrentTarget: " + __instance.CurrentTarget.ToString());
				}
				// SKIP_CHECK:;



				// Recalc visible targets
				int numTargets = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, targets, __instance.transform.rotation, mask.value);
				
				// Extract coins from the target list into its own list
				// Coin class only applies to active coins 
				// Collider.tag only applies to phys objs
				List<Collider> coinsAndGrenadeColliders = new List<Collider>();
				for (int i = 0; i < numTargets; i++) {
					Coin activeCoin = null;
					Grenade activeGrenade = null;

					bool gotCoin = targets[i].TryGetComponent<Coin>(out activeCoin) ;
					bool gotNade = targets[i].TryGetComponent<Grenade>(out activeGrenade);

					// RemoveOnTime tmpChk;
					// if (__instance.CurrentTarget.TryGetComponent<RemoveOnTime>(out tmpChk) ) {
					// 	continue;
					// }

					// IF coins only and gotCoin
					if (targetBitFlag == 0b0001 && gotCoin) {
						coinsAndGrenadeColliders.Add(targets[i]);
					}

					// IF core only and gotCore
					if (targetBitFlag == 0b0010 && gotNade) {
						coinsAndGrenadeColliders.Add(targets[i]);
					}

					// IF coin and core only and gotCore or gotCoin
					if (targetBitFlag == 0b0011 && ( gotCoin || gotNade )){
						coinsAndGrenadeColliders.Add(targets[i]);
					}

					// IF all and gotCoin or gotCore 
					if (targetBitFlag == 0b0000 && (gotCoin || gotNade) ) {
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
					setterCT.Invoke(__instance, new object[]{endCollider});
					setterAA.Invoke(__instance, new object[]{true});
				}
            }
        }
    }
}
