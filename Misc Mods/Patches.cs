using System;
using System.Reflection;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Misc_Mods
{
    internal class Patches
    {
        [HarmonyPatch(typeof(Tank), "OnSpawn")]
        private static class Tank_OnSpawn
        {
            public static void Postfix(Tank __instance)
            {
                __instance.airSpeedDragFactor = Class1.TechDrag * 0.0005f;
                __instance.airSpeedAngularDragFactor = Class1.TechDrag * 0.0005f;
            }
        }

        [HarmonyPatch(typeof(FanJet), "FixedUpdate")]
        private static class FanJet_FixedUpdate
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                int counter = FixedUpdatePatcher(ref codes, T_Class1.GetField("FanJetMultiplier"));
                Console.WriteLine("Injected " + counter + " force multipliers in FanJet.FixedUpdate()");
                counter = FixedUpdatePatcher_FanJet(ref codes, T_Class1.GetField("FanJetVelocityRestraint"));
                Console.WriteLine("Injected " + counter + " velocity alternators in FanJet.FixedUpdate()");
                return codes;
            }
        }

        [HarmonyPatch(typeof(BoosterJet), "FixedUpdate")]
        private static class BoosterJet_FixedUpdate
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                int counter = FixedUpdatePatcher(ref codes, T_Class1.GetField("BoosterJetMultiplier"));
                Console.WriteLine("Injected " + counter + " force multipliers in BoosterJet.FixedUpdate()");
                return codes;
            }
        }

        [HarmonyPatch(typeof(ModuleWing), "FixedUpdate")]
        private static class ModuleWing_FixedUpdate
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                int counter = FixedUpdatePatcher(ref codes, T_Class1.GetField("ModuleWingMultiplier"));
                Console.WriteLine("Injected " + counter + " force multipliers in ModuleWing.FixedUpdate()");
                return codes;
            }
        }

        const BindingFlags b = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;

        static readonly Type T_Class1 = typeof(Class1),
            T_Transform = typeof(Transform),
            T_Rbody = typeof(Rigidbody),
            T_Vector3 = typeof(Vector3),
            T_ForceMode = typeof(ForceMode),
            T_float = typeof(float),
            T_FanJet = typeof(FanJet);
        static readonly MethodInfo Rigidbody_AddForceAtPosition_1 = T_Rbody.GetMethod("AddForceAtPosition", new Type[] { T_Vector3, T_Vector3 }),
            Rigidbody_get_velocity = T_Rbody.GetMethod("get_velocity"),
            Rigidbody_AddForceAtPosition_2 = T_Rbody.GetMethod("AddForceAtPosition", new Type[] { T_Vector3, T_Vector3, T_ForceMode }),
            Vector3_op_Multiply_VF = T_Vector3.GetMethod("op_Multiply", new Type[] { T_Vector3, T_float }),
            Vector3_op_Subtraction = T_Vector3.GetMethod("op_Subtraction"),
            Vector3_Project = T_Vector3.GetMethod("Project"),
            Transform_get_forward = T_Transform.GetMethod("get_forward"),
            FanJet_get_AbsSpinRateCurrent = T_FanJet.GetMethod("get_AbsSpinRateCurrent");
        static readonly FieldInfo FanJet_m_Effector = T_FanJet.GetField("m_Effector", b);

        private static int FixedUpdatePatcher(ref List<CodeInstruction> codes, FieldInfo staticFieldMultiplier)
        {
            int counter = 0;
            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.opcode == OpCodes.Callvirt)
                {
                    MethodInfo operand = (MethodInfo)code.operand;
                    if (operand == Rigidbody_AddForceAtPosition_1 ||
                        operand == Rigidbody_AddForceAtPosition_2)
                    {
                        //Step back 1 to modify the first parameter. The 2nd one is loaded after this
                        codes.Insert(i - 1, new CodeInstruction(OpCodes.Ldsfld, staticFieldMultiplier)); // Pushes float on stack
                        codes.Insert(i + 0, new CodeInstruction(OpCodes.Call, Vector3_op_Multiply_VF)); // Multiplies with stack
                        i += 2;
                        counter++;
                    }
                }
            }
            return counter;
        }

        private static int FixedUpdatePatcher_FanJet(ref List<CodeInstruction> codes, FieldInfo staticFieldMultiplier)
        {
            int counter = 0;
            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.opcode == OpCodes.Callvirt)
                {
                    MethodInfo operand = (MethodInfo)code.operand;
                    if (operand == Rigidbody_AddForceAtPosition_1 ||
                        operand == Rigidbody_AddForceAtPosition_2)
                    {
                        //Same as before function, starting before the 2nd parameter
                        //> force
                        codes.Insert(i - 1, new CodeInstruction(OpCodes.Ldloc_1)); // Pushes local variable #2 (which should be 'rigidbody') on stack
                                                                                   //> force, rbody
                        codes.Insert(i + 0, new CodeInstruction(OpCodes.Callvirt, Rigidbody_get_velocity)); // Push velocity from 'rigidbody' on stack
                                                                                                            //> force, velocity
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldarg_0)); // Put 'this' on stack
                                                                                   //> force, velocity, this
                        codes.Insert(i + 2, new CodeInstruction(OpCodes.Ldfld, FanJet_m_Effector)); // Push field on stack
                                                                                                    //> force, velocity, effector
                        codes.Insert(i + 3, new CodeInstruction(OpCodes.Callvirt, Transform_get_forward)); // Push forward vector from 'effector'
                                                                                                           //> force, velocity, direction
                        codes.Insert(i + 4, new CodeInstruction(OpCodes.Call, Vector3_Project)); // Project velocity to forward
                                                                                                 //> force, proj_vel
                        codes.Insert(i + 5, new CodeInstruction(OpCodes.Ldarg_0)); // Put 'this' on stack
                                                                                   //> force, proj_vel, this
                        codes.Insert(i + 6, new CodeInstruction(OpCodes.Call, FanJet_get_AbsSpinRateCurrent)); // Push the absolute spin rate
                                                                                                               //> force, proj_vel, abs_spin
                        codes.Insert(i + 7, new CodeInstruction(OpCodes.Call, Vector3_op_Multiply_VF)); // Multiplies with stack, push result
                                                                                                        //> force, scaled_vel
                        codes.Insert(i + 8, new CodeInstruction(OpCodes.Ldsfld, staticFieldMultiplier)); // Pushes static field on stack
                                                                                                         //> force, scaled_vel, multi
                        codes.Insert(i + 9, new CodeInstruction(OpCodes.Call, Vector3_op_Multiply_VF)); // Multiplies with stack, push result
                                                                                                        //> force, vel_strength
                        codes.Insert(i + 10, new CodeInstruction(OpCodes.Call, Vector3_op_Subtraction)); // Subtracts with stack, push result
                                                                                                         //> final_force
                                                                                                         // This is what will be used by the 'AddForceAtPosition' function
                        i += 12;
                        counter++;
                    }
                }
            }
            return counter;
        }
    }
}
