using Cinemachine;
using DDoorDebug.Model;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;

namespace DDoorDebug.Patches
{
    public static class HarmonyPatches
    {
        [HarmonyPatch(typeof(UIMenuOptions), "doSetResolution")]
        static class PatchUIMenuOptions
        {
            static void Postfix(int x, int y)
            {
                if (DDoorDebugPlugin.instance != null)
                    DDoorDebugPlugin.instance.SetMatrix(x, y);
            }
        }

        [HarmonyPatch(typeof(PlayerGlobal), "Start")]
        static class PatchPlayerGlobal
        {
            static void Postfix(PlayerGlobal __instance, Damageable ___dmg, PlayerMovementControl ___moveControl, ref WeaponControl ___weaponControl, ref Rigidbody ___body)
            {
                if (DDoorDebugPlugin.instance.DData != null)
                {
                    DDoorDebugPlugin.instance.DData.dmgObject = ___dmg;
                    DDoorDebugPlugin.instance.DData.wpnObject = ___weaponControl;
                    DDoorDebugPlugin.instance.DData.movCtrlObject = ___moveControl;
                    DDoorDebugPlugin.instance.DData.plrRBody = ___body;
                }
            }
        }

        [HarmonyPatch(typeof(WeaponControl), "spawnWeapon")]
        static class PatchWeaponControl
        {
            static void Postfix(WeaponAttackReferences ___weaponAttacks)
            {
                if (DDoorDebugPlugin.instance.DData != null)
                    DDoorDebugPlugin.instance.DData.wpnRefs = ___weaponAttacks;
            }
        }

        [HarmonyPatch(typeof(WeaponOffhandControl), "spawnWeapon")]
        static class PatchWeaponOffhandControl
        {
            static void Postfix(_ArrowPower ___arrowPower)
            {
                if (DDoorDebugPlugin.instance.DData != null)
                    DDoorDebugPlugin.instance.DData.magicRefs = ___arrowPower;
            }
        }

        [HarmonyPatch(typeof(DamageableCharacter), "Start")]
        static class PatchDamageableCharacter
        {
            static void Postfix(float ___currentHealth, DamageableCharacter __instance)
            {
                if (DDoorDebugPlugin.instance.DData != null)
                    DDoorDebugPlugin.instance.AddDamageable(__instance);
            }
        }

        [HarmonyPatch(typeof(GameRoom), "EnterRoom")]
        static class PatchGameRoom
        {
            static void Postfix()
            {
                if (DDoorDebugPlugin.instance.DData != null)
                    DDoorDebugPlugin.instance.ClearAllCache();
            }
        }
        [HarmonyPatch(typeof(DamageableCharacter), "ReceiveDamage")]
        static class PatchDamageable
        {
            static void Postfix(ref bool __result, float dmg, float poiseDmg, Vector3 originPos, Vector3 hitPos, Damageable.DamageType type, float hitForce, DamageableCharacter __instance)
            {
                if (__result && __instance.gameObject != PlayerGlobal.instance.gameObject)
                    DDoorDebugPlugin.instance.DData.lastDamage = new DamageData(dmg, poiseDmg, type);
            }
        }

        [HarmonyPatch(typeof(DamageableCharacter), "Die")]
        static class PatchDamageableCharacter2
        {
            static void Postfix(DamageableCharacter __instance)
            {
                if (DDoorDebugPlugin.instance.DData != null)
                {
                    var c = DDoorDebugPlugin.instance.DData.damageables.Count;
                    for (int i = c - 1; i >= 0; i--)
                    {
                        var curr = DDoorDebugPlugin.instance.DData.damageables[i];
                        if (curr.instance == __instance)
                        {
                            curr.instance = null;
                            DDoorDebugPlugin.instance.DData.damageables[i] = null;
                            DDoorDebugPlugin.instance.DData.damageables.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(VignetteControl), "Start")]
        static class PatchVignetteControl
        {
            static void Postfix(VignetteControl __instance)
            {
                DDoorDebugPlugin.instance.Cache.mainCam = __instance.gameObject.GetComponent<Camera>();
                DDoorDebugPlugin.instance.Cache.cineBrain = __instance.gameObject.GetComponent<CinemachineBrain>();
                if (CameraMovementControl.instance)
                    DDoorDebugPlugin.instance.Cache.virtCam =  CameraMovementControl.instance.GetComponentInChildren<CinemachineVirtualCamera>();
                    
            }
        }

        [HarmonyPatch(typeof(GameSave), "Save")]
        static class PatchGameSave
        {
            static void Postfix()
            {
                DDoorDebugPlugin.instance.DData.lastSave = Time.realtimeSinceStartup;
            }
        }
        /*[HarmonyPatch(typeof(EnchantController), "DamageModifier", new Type[] { typeof(float) })]
        static class EnchantPatch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var newBody = new List<CodeInstruction>(15);
                foreach (var inst in instructions)
                {
                    if (inst.opcode == OpCodes.Newarr) break;
                    newBody.Add(inst);
                }
                newBody[newBody.Count - 1] = new CodeInstruction(OpCodes.Ldloc_0);
                newBody.Add(new CodeInstruction(OpCodes.Ret));
                return newBody;
            }
        }*/

        [HarmonyPatch(typeof(Weapon_Umbrella), "SetHolstered", new Type[] { typeof(bool) })]
        static class UmbrellaPatch
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var newBody = instructions.ToList();
                var found = 0;
                foreach (var inst in instructions)
                {
                    if (inst.opcode == OpCodes.Ldstr) break;
                    found++;
                }
                if (found > 0)
                    newBody.RemoveRange(3, 9);
                return newBody;
            }
        }
    }
}
