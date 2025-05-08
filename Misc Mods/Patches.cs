using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

namespace Misc_Mods
{
    internal class Patches
    {
        // [HarmonyPatch(typeof(UIBlockInfoDisplay), "UpdateBlock", new Type[] { typeof(BlockTypes), typeof(bool) })]
        private static class DumpLoadingScreen
        {
            private static HashSet<UIBlockInfoDisplay> dumped = new HashSet<UIBlockInfoDisplay>();

            private static void DumpPNG(string path, Sprite sprite)
            {
                Texture2D tex = new Texture2D((int)sprite.rect.width, (int)sprite.rect.height);
                Graphics.CopyTexture(sprite.texture, tex);
                // tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }

            [HarmonyPriority(Priority.Low)]
            public static void Postfix(UIBlockInfoDisplay __instance)
            {
                if (!dumped.Contains(__instance))
                {
                    dumped.Add(__instance);

                    string path = Path.Combine(GUIConfig.TTSteamDir, "_Export/UI");
                    ArbitraryGODumper dumper = new ArbitraryGODumper(__instance.gameObject);
                    dumper.DumpExternal = true;
                    dumper.showTypeInformation = true;
                    string Total = dumper.Dump();
                    if (!System.IO.Directory.Exists(path))
                    {
                        System.IO.Directory.CreateDirectory(path);
                    }
                    string safeName = GUIConfig.SafeName($"UIBlockInfoDisplay_{dumped.Count}");
                    System.IO.File.WriteAllText(path + "/" + safeName + ".json", Total);

                    /*
                    Transform Background = __instance.loadingBar.transform.GetChild(1);
                    Transform Fill = Background.GetChild(0);
                    Image background = Background.GetComponent<Image>();
                    Image fill = Fill.GetComponent<Image>();
                    Texture2D fillTexture = (Texture2D) fill.sprite.texture;
                    Texture2D backgroundTexture = (Texture2D) background.sprite.texture;
                    DumpPNG(Path.Combine(path, "background.png"), background.sprite);
                    DumpPNG(Path.Combine(path, "fill.png"), fill.sprite);
                    */
                }
            }
        }

        [HarmonyPatch(typeof(Tank), "OnSpawn")]
        private static class Tank_OnSpawn
        {
            public static void Postfix(Tank __instance)
            {
                __instance.airSpeedDragFactor = Class1.TechDrag * 0.0005f;
                __instance.airSpeedAngularDragFactor = Class1.TechDrag * 0.0005f;
            }
        }
    }
}
