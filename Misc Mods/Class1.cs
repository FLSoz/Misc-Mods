using ModHelper;
using System;
using UnityEngine;
using HarmonyLib;
using System.Reflection;

namespace Misc_Mods
{
    internal static class Tony
    {
        public static void PokeTony() => Update();
        static void Update() => ExamineKneecaps();
        static void ExamineKneecaps() => throw new Exception("Kneecaps not found! Please reinstall kneecaps to proceed, else Tony shalln't  W-A-L-K");
    }
    public class Class1
    {
        public static ModConfig config;

        public static float FanJetMultiplier = 1f,
            FanJetVelocityRestraint = 0f,
            BoosterJetMultiplier = 1f,
            ModuleWingMultiplier = 1f;
        public static float WorldDrag = 1f,
            TechDrag = 1f;

        internal const string HarmonyID = "aceba1.ttmm.misc";
        internal static Harmony harmony = new Harmony(HarmonyID);

        internal static void SetupAssets()
        {
            config = new ModConfig();
            config.BindConfig<Class1>(null, "FanJetMultiplier");
            config.BindConfig<Class1>(null, "FanJetVelocityRestraint");
            config.BindConfig<Class1>(null, "BoosterJetMultiplier");
            config.BindConfig<Class1>(null, "ModuleWingMultiplier");
            config.BindConfig<Class1>(null, "WorldDrag");
            config.BindConfig<Class1>(null, "TechDrag");
            config.UpdateConfig += GUIConfig.ResetMultipliers;

            new GameObject().AddComponent<GUIConfig>();
            GUIConfig.ResetMultipliers();
        }

        internal static void ApplyPatches()
        {
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public static void Run()
        {
            SetupAssets();
            ApplyPatches();
        }
    }
}