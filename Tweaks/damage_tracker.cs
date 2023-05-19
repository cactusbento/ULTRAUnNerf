using HarmonyLib;
using System;
using System.Timers;
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
    [TweakMetadata("damageTracker", $"{Extension.GUID}.damageTracker", "Track the damage you deal.", $"{Extension.GUID}.ext_page", 0, null)]
    public class damageTracker : Tweak // All tweaks must inherit `Tweak`.
    {
        private Harmony harmony = new($"{Extension.GUID}.damageTracker");
        private static ManualLogSource L;

		protected const float LIFETIME = 1.5f;
		private static Timer dictTimer;

		// Here a Dictinoary is used to keep track of 
		// the damage done in the last LIFETIME.
		// Key: Struct
		//     WEAPON: The weapon used to damage the target.
		//     MULTIPLIER: Multiplier for the damage done.
		// Value: Int
		//     HITAMOUNT: The amount of times the weapon hits.
		protected struct DictKey {
			public string WEAPON;
			public float MULTIPLIER;
			
			public DictKey(string W, float M) {
				WEAPON = W; MULTIPLIER = M;
			}
		}
		protected static Dictionary<DictKey, int> DamageTrackerDict = new Dictionary<DictKey, int>();
		
        // Runtime Determined:
		protected static float TotalDamage = 0f;

        public damageTracker() {}

		// Executes when combo lifetime is over.
        private void OnTimedEvent(System.Object source, ElapsedEventArgs e) {
			// Print out damage output from each hit type
			foreach (KeyValuePair<DictKey, int> kvp in DamageTrackerDict) {
				float keyTotal = kvp.Key.MULTIPLIER * kvp.Value;
				L.LogInfo(kvp.Key.WEAPON + ": " + kvp.Key.MULTIPLIER.ToString() + " * " + kvp.Value.ToString() + " = " + keyTotal.ToString());
				MonoSingleton<SubtitleController>.Instance
					.DisplaySubtitle(kvp.Key.WEAPON + ": " + kvp.Key.MULTIPLIER.ToString() + " * " + kvp.Value.ToString() + " = " + keyTotal.ToString(), null);
				TotalDamage += keyTotal;
			}
			
			if (DamageTrackerDict.Count > 0) {
				L.LogInfo("Total Damage: " + TotalDamage.ToString());
				MonoSingleton<SubtitleController>.Instance
					.DisplaySubtitle("Total Damage: " + TotalDamage.ToString(), null);
			}

        	DamageTrackerDict.Clear();
        	TotalDamage = 0f;
        }

        public override void OnTweakEnabled() {
            base.OnTweakEnabled();
        	L = new ManualLogSource("UKUnNerf");
        	BepInEx.Logging.Logger.Sources.Add(L);

        	// Setup Timer 
        	dictTimer = new Timer(LIFETIME * 1000);
        	dictTimer.Elapsed += OnTimedEvent;
        	dictTimer.AutoReset = false;

            harmony.PatchAll(typeof(damageTrackerPatches));
        }

        public override void OnTweakDisabled() {
            base.OnTweakDisabled();
            harmony.UnpatchSelf();
        }

        public class damageTrackerPatches
        {
            [HarmonyPatch(typeof(EnemyIdentifier), "DeliverDamage"), HarmonyPostfix]
            private static void PostDeliverDamage(EnemyIdentifier __instance, ref float multiplier, 
            		ref float critMultiplier, ref GameObject sourceWeapon, ref bool ignoreTotalDamageTakenMultiplier) {


            	float localMult = multiplier;

				if (!ignoreTotalDamageTakenMultiplier) {
					localMult *= __instance.totalDamageTakenMultiplier;
				}

				localMult /= __instance.totalHealthModifier;
				if (__instance.weaknesses.Length != 0) {
					for (int i = 0; i < __instance.weaknesses.Length; i++) {
						if (__instance.hitter == __instance.weaknesses[i]) {
							localMult *= __instance.weaknessMultipliers[i];
						}
					}
				}

				if (__instance.burners.Count > 0 && __instance.hitter != "fire" && __instance.hitter != "explosion" && __instance.hitter != "ffexplosion") {
					localMult *= 1.5f;
				}

				if (!__instance.dead) {
					// L.LogInfo( __instance.hitter.ToUpper() + ": " + localMult.ToString() + " -> " + __instance.fullName.ToUpper() );

					DictKey newKey = new DictKey(__instance.hitter, localMult);
					if (localMult > 0f) {
						if (!DamageTrackerDict.ContainsKey(newKey)) {
							DamageTrackerDict.Add(newKey, 1);
						} else {
							DamageTrackerDict[newKey]++;
						}
					}

					dictTimer.Stop();
					dictTimer.Start();
				}

            }
        }
    }
}
