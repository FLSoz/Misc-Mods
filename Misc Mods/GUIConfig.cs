﻿using System;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Misc_Mods
{
    public class GUIConfig : MonoBehaviour
    {
        GameObject GUIDisp;
        
        internal static readonly string TTSteamDir = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name == "Assembly-CSharp").First().Location
            .Replace("Assembly-CSharp.dll", ""), @"../../"
        ));

        public void Start()
        {
            GUIDisp = new GameObject();
            GUIDisp.AddComponent<GUIDisplay>().inst = this;
            GUIDisp.SetActive(false);
        }

        private Rect WindowRect = new Rect(0, 0, 800, 400);
        private Vector2 ScrollPos = Vector2.zero;
        private bool ShowGUI = false;
        private Visible module;

        private void Update()
        {
            if (!Singleton.Manager<ManPointer>.inst.DraggingItem && Input.GetMouseButtonDown(1))
            {
                try
                {
                    module = Singleton.Manager<ManPointer>.inst.targetVisible;
                }
                catch
                {
                    //Console.WriteLine(e);
                    //module = null;
                }
            }

            try
            {
                if (Input.GetKeyDown(KeyCode.BackQuote))
                {
                    ShowGUI = !ShowGUI;
                    GUIDisp.SetActive(ShowGUI);
                    if (ShowGUI == false)
                    {
                        Class1.config.WriteConfigJsonFile();
                        module = null;
                        log = "Right-click on a block to select it here";
                    }
                }
            }
            catch (Exception E)
            {
                Console.WriteLine("EXCEPTION: " + E.Message + "\n" + E.StackTrace);
            }
        }

        private void MiscPage(int ID)
        {
            ScrollPos = GUILayout.BeginScrollView(ScrollPos);
            GUILayout.Label("Selected Block: " + (module ? module.name : "None"));
            GUILayout.Label(log);
            GUILayout.BeginVertical("Model Exporter", GUI.skin.window);
            ExportPage(ID);
            GUILayout.EndVertical();
            GUILayout.Space(16);
            GUILayout.BeginVertical("Block JSON Dumper", GUI.skin.window);
            BlockInfoDumperPage(ID);
            GUILayout.EndVertical();
            GUILayout.Space(16);
            GUILayout.BeginVertical("World Multiplier", GUI.skin.window);
            WorldMultiplierPage(ID);
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        public static string log = "";

        const string regex = @"^[\w\-. ]+$";
        internal static string SafeName(string source)
        {
            string result = source;
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                result = result.Replace(c, '_');
            }
            return result;
        }

        private void ExportPage(int ID)
        {
            try
            {
                if (Singleton.playerTank != null)
                {
                    //if (GUILayout.Button("Export Player Tech Model (with GrabIt Tools)"))
                    //{
                    //    ObjGrabItExporter.ExportWithGrabIt(Singleton.playerTank.gameObject);
                    //    log = "Processing, please be patient...";
                    //}
                    if (GUILayout.Button("Export Player Tech Model"))
                    {
                        string path = Path.Combine(TTSteamDir, "_Export/Techs");
                        string Total = LocalObjExporter.DoExport(Singleton.playerTank.trans);
                        if (!System.IO.Directory.Exists(path))
                        {
                            System.IO.Directory.CreateDirectory(path);
                        }
                        string safeName = SafeName(Singleton.playerTank.name);
                        System.IO.File.WriteAllText(path + "/" + safeName + ".obj", Total);
                        log = "Exported " + safeName + ".obj to " + path;
                    }
                }
                if (module != null)
                {
                    if (GUILayout.Button("Export " + module.type.ToString() + " Model"))
                    {
                        string path = Path.Combine(TTSteamDir, "_Export/Models");
                        string Total = LocalObjExporter.DoExport(module.transform);
                        if (!System.IO.Directory.Exists(path))
                        {
                            System.IO.Directory.CreateDirectory(path);
                        }
                        string safeName = SafeName(module.name);
                        System.IO.File.WriteAllText(path + "/" + safeName + ".obj", Total);
                        log = "Exported " + safeName + ".obj to " + path;
                    }
                    if (GUILayout.Button("Export Parts of Selected " + module.type.ToString()))
                    {
                        string path = Path.Combine(TTSteamDir, "_Export/ModelParts", SafeName(module.name));
                        if (!System.IO.Directory.Exists(path))
                            System.IO.Directory.CreateDirectory(path);

                        var fireData = module.GetComponent<FireData>();
                        if (fireData != null)
                        {
                            if (fireData.m_BulletPrefab != null)
                            {
                                foreach (var mf in fireData.m_BulletPrefab.GetComponentsInChildren<MeshFilter>())
                                {
                                    if (!System.IO.Directory.Exists(path + "/bullet"))
                                        System.IO.Directory.CreateDirectory(path + "/bullet");
                                    System.IO.File.WriteAllText(path + "/bullet/" + SafeName(mf.mesh.name) + ".obj", LocalObjExporter.MeshToString(mf.mesh, mf.mesh.name, Vector3.one, Vector3.zero, Quaternion.identity));
                                }
                            }
                            if (fireData.m_BulletCasingPrefab != null)
                            {
                                foreach (var mf in fireData.m_BulletCasingPrefab.GetComponentsInChildren<MeshFilter>())
                                {
                                    if (!System.IO.Directory.Exists(path + "/casing"))
                                        System.IO.Directory.CreateDirectory(path + "/casing");
                                    System.IO.File.WriteAllText(path + "/casing/" + SafeName(mf.mesh.name) + ".obj", LocalObjExporter.MeshToString(mf.mesh, mf.mesh.name, Vector3.one, Vector3.zero, Quaternion.identity));
                                }
                            }
                        }
                        foreach (var mf in module.GetComponentsInChildren<MeshFilter>())
                        {
                            System.IO.File.WriteAllText(path + "/" + SafeName(mf.mesh.name) + ".obj", LocalObjExporter.MeshToString(mf.mesh, mf.mesh.name, Vector3.one, Vector3.zero, Quaternion.identity));
                        }

                        log = "Exported individual .obj files to " + path;
                    }
                    if (GUILayout.Button("Export all textures"))
                    {
                        string path = Path.Combine(TTSteamDir, "_Export/Textures", SafeName(module.name));
                        if (!System.IO.Directory.Exists(path))
                            System.IO.Directory.CreateDirectory(path);

                        Texture2D original = ManUI.inst.GetSprite(module.m_ItemType).texture;
                        Texture2D copy = duplicateTexture(original);
                        System.IO.File.WriteAllBytes(path + "/icon.png", copy.EncodeToPNG());

                        if (module.type == ObjectTypes.Block)
                        {
                            var type = ManSpawn.inst.GetCorporation(module.block.BlockType);
                            var maintex = ManCustomSkins.inst.GetSkinTexture(type, 0);

                            System.IO.File.WriteAllBytes(path + "/" + SafeName(type.ToString()) + "_1.png", duplicateTexture(maintex.m_Albedo).EncodeToPNG());
                            System.IO.File.WriteAllBytes(path + "/" + SafeName(type.ToString()) + "_2.png", duplicateTexture(maintex.m_Metal).EncodeToPNG());
                            System.IO.File.WriteAllBytes(path + "/" + SafeName(type.ToString()) + "_3.png", duplicateTexture(maintex.m_Emissive).EncodeToPNG());
                        }

                        Dictionary<Texture, string> buffer = new Dictionary<Texture, string>();
                        List<Vector2> invalid = new List<Vector2>();

                        Console.WriteLine("Dumping textures of " + module.name + "...");
                        var fireData = module.GetComponent<FireData>();
                        if (fireData != null)
                        {
                            if (fireData.m_BulletPrefab != null)
                            {
                                Console.WriteLine("FireData - Bullet " + fireData.m_BulletPrefab.name + "...");
                                foreach (var mr in fireData.m_BulletPrefab.GetComponentsInChildren<Renderer>())
                                {
                                    var mat = mr.material;
                                    Console.WriteLine($"- {mr.sharedMaterial.name} - [ {mat.shader.name} ] - {mat.name}");
                                    var tex1 = mat.mainTexture;
                                    if (tex1 != null)
                                        buffer.Add(tex1, mr.name + "_" + tex1.name + "_bullet_1.png");
                                    var tex2 = mat.GetTexture("_MetallicGlossMap");
                                    if (tex2 != null)
                                        buffer.Add(tex2, mr.name + "_" + tex2.name + "_bullet_2.png");
                                    var tex3 = mat.GetTexture("_EmissionMap");
                                    if (tex3 != null)
                                        buffer.Add(tex3, mr.name + "_" + tex3.name + "_bullet_3.png");
                                }
                            }
                            if (fireData.m_BulletCasingPrefab != null)
                            {
                                Console.WriteLine("FireData - Casing " + fireData.m_BulletCasingPrefab.name + "...");
                                foreach (var mr in fireData.m_BulletCasingPrefab.GetComponentsInChildren<Renderer>())
                                {
                                    var mat = mr.material;
                                    Console.WriteLine($"- {mr.sharedMaterial.name} - [ {mat.shader.name} ] - {mat.name}");
                                    var tex1 = mat.mainTexture;
                                    if (tex1 != null)
                                        buffer.Add(tex1, mr.name + "_" + tex1.name + "_casing_1.png");
                                    var tex2 = mat.GetTexture("_MetallicGlossMap");
                                    if (tex2 != null)
                                        buffer.Add(tex2, mr.name + "_" + tex2.name + "_casing_2.png");
                                    var tex3 = mat.GetTexture("_EmissionMap");
                                    if (tex3 != null)
                                        buffer.Add(tex3, mr.name + "_" + tex3.name + "_casing_3.png");
                                }
                            }
                        }

                        Console.WriteLine("Renderers...");
                        foreach (var mr in module.GetComponentsInChildren<Renderer>(true))
                        {
                            var mat = mr.material;
                            Console.WriteLine($"- {mr.sharedMaterial.name} - [ {mat.shader.name} ] - {mat.name}");

                            foreach (string key in mat.shaderKeywords)
                                if (key == "_SKINS") // Do not dump
                                {
                                    // Skin textures are oddly sized, this is as a filter to catch any potential leaks
                                    invalid.Add(new Vector2(mat.mainTexture.width, mat.mainTexture.height));

                                    goto skipdump; // 'continue' would not work here
                                }

                            var tex1 = mat.mainTexture;
                            if (tex1 != null && !QuickCompare(buffer, tex1))//!buffer.ContainsKey(tex1))
                                buffer.Add(tex1, mr.name + "_" + tex1.name + "_1.png");
                            var tex2 = mat.GetTexture("_MetallicGlossMap");
                            if (tex2 != null && !QuickCompare(buffer, tex2))//buffer.ContainsKey(tex2))
                                buffer.Add(tex2, mr.name + "_" + tex2.name + "_2.png");
                            var tex3 = mat.GetTexture("_EmissionMap");
                            if (tex3 != null && !QuickCompare(buffer, tex3))//buffer.ContainsKey(tex3))
                                buffer.Add(tex3, mr.name + "_" + tex3.name + "_3.png");

                            skipdump:;
                        }

                        Console.WriteLine("Projectors...");
                        foreach (var mr in module.GetComponentsInChildren<Projector>(true))
                        {
                            var mat = mr.material;
                            Console.WriteLine($"- [ {mat.shader.name} ] - {mat.name}");

                            // Doubt there'd be any skin-locked projections going on here

                            //foreach (string key in mat.shaderKeywords)
                            //    if (key == "_SKINS") // Do not dump
                            //    {
                            //        // Skin textures are oddly sized, this is as a filter to catch any potential leaks
                            //        invalid.Add(new Vector2(mat.mainTexture.width, mat.mainTexture.height));

                            //        goto skipdump; // 'continue' would not work here
                            //    }

                            var tex1 = mat.mainTexture;
                            if (tex1 != null)
                                buffer.Add(tex1, mr.name + "_" + tex1.name + "_Projector_1.png");
                            var tex2 = mat.GetTexture("_MetallicGlossMap");
                            if (tex2 != null)
                                buffer.Add(tex2, mr.name + "_" + tex2.name + "_Projector_2.png");
                            var tex3 = mat.GetTexture("_EmissionMap");
                            if (tex3 != null)
                                buffer.Add(tex3, mr.name + "_" + tex3.name + "_Projector_3.png");

                            //skipdump:;
                        }
                        int count = 0;
                        foreach (var tex in buffer)
                        {
                            if (invalid.Contains(new Vector2(tex.Key.width, tex.Key.height))) continue;
                            System.IO.File.WriteAllBytes(path + "/" + SafeName(tex.Value), duplicateTexture(tex.Key).EncodeToPNG());
                            count++;
                        }

                        log = $"Exported {count} .png file{(count != 1 ? "s" : "")} to " + path;
                    }
                }
            }
            catch (Exception E)
            {
                log = E.Message;
                Console.WriteLine(E.Message);
                Console.WriteLine(E.StackTrace);
            }
        }

        static bool QuickCompare(Dictionary<Texture, string> A, Texture B)
        {
            foreach (var a in A.Keys)
                if (a.GetHashCode() == B.GetHashCode()) return true;
            return false;
        }

        private void BlockInfoDumperPage(int ID)
        {
            try
            {
                if (GUILayout.Button("Export BlockInfoDump.JSON"))
                {
                    log = "Logged " + BlockInfoDumper.Dump().ToString() + " blocks to file";
                }
                if (module != null)
                {
                    if (GUILayout.Button("Export " + module.type.ToString() + " JSON"))
                    {
                        string path = Path.Combine(TTSteamDir, "_Export", module.type.ToString() + "Json");
                        BlockInfoDumper.DeepDumpClassCache.Clear();
                        BlockInfoDumper.CachedTransforms.Clear();
                        ArbitraryGODumper dumper = new ArbitraryGODumper(module.gameObject);
                        string Total = dumper.Dump();
                        if (!System.IO.Directory.Exists(path))
                        {
                            System.IO.Directory.CreateDirectory(path);
                        }
                        string safeName = SafeName(module.name);
                        System.IO.File.WriteAllText(path + "/" + safeName + ".json", Total);
                        log = "Exported " + safeName + ".json to " + path;
                    }
                    if (GUILayout.Button("Export DETAILED " + module.type.ToString() + " JSON"))
                    {
                        string path = Path.Combine(TTSteamDir, "_Export", module.type.ToString() + "Json");
                        BlockInfoDumper.DeepDumpClassCache.Clear();
                        BlockInfoDumper.CachedTransforms.Clear();
                        ArbitraryGODumper dumper = new ArbitraryGODumper(module.gameObject);
                        dumper.showTypeInformation = true;
                        dumper.DumpExternal = true;
                        string Total = dumper.Dump();
                        if (!System.IO.Directory.Exists(path))
                        {
                            System.IO.Directory.CreateDirectory(path);
                        }
                        string safeName = SafeName(module.name);
                        System.IO.File.WriteAllText(path + "/" + safeName + ".json", Total);
                        log = "Exported " + safeName + ".json to " + path;
                    }
                    if (module.type == ObjectTypes.Block)
                    {
                        if (GUILayout.Button("Export Block Prefab JSON"))
                        {
                            string path = Path.Combine(TTSteamDir, "_Export/BlockJson");
                            BlockInfoDumper.DeepDumpClassCache.Clear();
                            BlockInfoDumper.CachedTransforms.Clear();
                            ArbitraryGODumper dumper = new ArbitraryGODumper(ManSpawn.inst.GetBlockPrefab((BlockTypes)module.ItemType).gameObject);
                            string Total = dumper.Dump();
                            if (!System.IO.Directory.Exists(path))
                            {
                                System.IO.Directory.CreateDirectory(path);
                            }
                            string safeName = SafeName(module.name);
                            System.IO.File.WriteAllText(path + "/" + safeName + "_prefab.json", Total);
                            log = "Exported " + safeName + "prefab_.json to " + path;
                        }
                        if (GUILayout.Button("Export DETAILED Block Prefab JSON"))
                        {
                            string path = Path.Combine(TTSteamDir, "_Export/BlockJson");
                            BlockInfoDumper.DeepDumpClassCache.Clear();
                            BlockInfoDumper.CachedTransforms.Clear();
                            ArbitraryGODumper dumper = new ArbitraryGODumper(ManSpawn.inst.GetBlockPrefab((BlockTypes)module.ItemType).gameObject);
                            dumper.DumpExternal = true;
                            dumper.showTypeInformation = true;
                            string Total = dumper.Dump();
                            if (!System.IO.Directory.Exists(path))
                            {
                                System.IO.Directory.CreateDirectory(path);
                            }
                            string safeName = SafeName(module.name);
                            System.IO.File.WriteAllText(path + "/" + safeName + "_prefab.json", Total);
                            log = "Exported " + safeName + "prefab_.json to " + path;
                        }

                        var fireData = module.GetComponent<FireData>();
                        if (fireData != null && GUILayout.Button("Export FireData Projectile JSON"))
                        {
                            string path = Path.Combine(TTSteamDir, "_Export/BlockJson");
                            bool nothing = true;
                            log = "Exported ";
                            if (fireData.m_BulletPrefab != null)
                            {
                                nothing = false;
                                BlockInfoDumper.DeepDumpClassCache.Clear();
                                BlockInfoDumper.CachedTransforms.Clear();
                                ArbitraryGODumper dumper = new ArbitraryGODumper(fireData.m_BulletPrefab.gameObject);
                                dumper.showTypeInformation = true;
                                string Total = dumper.Dump();
                                if (!System.IO.Directory.Exists(path))
                                {
                                    System.IO.Directory.CreateDirectory(path);
                                }
                                string safeName = SafeName(module.name);
                                System.IO.File.WriteAllText(path + "/" + safeName + "_BulletPrefab.json", Total);
                                log += safeName + "_BulletPrefab.json";
                            }
                            if (fireData.m_BulletCasingPrefab != null)
                            {
                                if (!nothing)
                                    log += "\nand ";
                                nothing = false;
                                BlockInfoDumper.DeepDumpClassCache.Clear();
                                BlockInfoDumper.CachedTransforms.Clear();
                                ArbitraryGODumper dumper = new ArbitraryGODumper(fireData.m_BulletCasingPrefab.gameObject);
                                dumper.showTypeInformation = true;
                                string Total = dumper.Dump();
                                if (!System.IO.Directory.Exists(path))
                                {
                                    System.IO.Directory.CreateDirectory(path);
                                }
                                string safeName = SafeName(module.name);
                                System.IO.File.WriteAllText(path + "/" + safeName + "_CasingPrefab.json", Total);
                                log += safeName + "_CasingPrefab.json";
                            }
                            if (nothing)
                                log += "nothing";

                            log += " to " + path;
                        }
                    }

                    Tank player = Singleton.playerTank;
                    if (player != null)
                    {
                        if (GUILayout.Button("Export Player Tank JSON"))
                        {
                            string path = Path.Combine(TTSteamDir, "_Export");
                            BlockInfoDumper.DeepDumpClassCache.Clear();
                            BlockInfoDumper.CachedTransforms.Clear();
                            ArbitraryGODumper dumper = new ArbitraryGODumper(player.gameObject);
                            string Total = dumper.Dump();
                            if (!System.IO.Directory.Exists(path))
                            {
                                System.IO.Directory.CreateDirectory(path);
                            }
                            System.IO.File.WriteAllText(path + "/PlayerTank.json", Total);
                            log = "Exported PlayerTank.json to " + path;
                        }
                        if (GUILayout.Button("Export DETAILED Player Tank JSON"))
                        {
                            string path = Path.Combine(TTSteamDir, "_Export");
                            BlockInfoDumper.DeepDumpClassCache.Clear();
                            BlockInfoDumper.CachedTransforms.Clear();
                            ArbitraryGODumper dumper = new ArbitraryGODumper(player.gameObject);
                            dumper.DumpExternal = true;
                            dumper.showTypeInformation = true;
                            string Total = dumper.Dump();
                            if (!System.IO.Directory.Exists(path))
                            {
                                System.IO.Directory.CreateDirectory(path);
                            }
                            System.IO.File.WriteAllText(path + "/PlayerTank.json", Total);
                            log = "Exported PlayerTank.json to " + path;
                        }
                    }
                }
            }
            catch (Exception E)
            {
                log = E.Message;
                Console.WriteLine(E.Message);
                Console.WriteLine(E.StackTrace);
            }
        }

        string fjm, fjr, mwm, bjm, wd, td, esr;
        private void WorldMultiplierPage(int ID)
        {
            try
            {
                TextSliderPair("Enemy spawn rate (seconds between): ", ref esr, ref ManPop.inst.m_MinPeriodBetweenSpawns, 0f, 120f, false);
                if (GUILayout.Button("Force enemy spawn now"))
                    ManPop.inst.DebugForceSpawn();

                GUILayout.Space(16);

                TextSliderPair("Turbine Strength: ", ref fjm, ref Class1.FanJetMultiplier, 0f, 2f, false);
                TextSliderPair("Turbine Velocity Limiter: ", ref fjr, ref Class1.FanJetVelocityRestraint, 0f, 25f, false, 1f);
                TextSliderPair("Wing Strength: ", ref mwm, ref Class1.ModuleWingMultiplier, 0f, 2f, false);
                TextSliderPair("Booster Strength: ", ref bjm, ref Class1.BoosterJetMultiplier, 0f, 2f, false);
                if (TextSliderPair("Tech Drag: ", ref td, ref Class1.TechDrag, 0, 10f, false, 0.005f))
                {
                    ResetTechDrag();
                }
                if (TextSliderPair("World Drag: ", ref wd, ref Class1.WorldDrag, 0f, 10f, false, 0.005f))
                {
                    ResetWorldDrag();
                }
            }
            catch (Exception E)
            {
                log = E.Message;
                Console.WriteLine(E.Message);
                Console.WriteLine(E.StackTrace);
            }
        }
        public static void ResetMultipliers()
        {
            ResetTechDrag();
            ResetWorldDrag();
        }
        public static void ResetTechDrag()
        {
            foreach (var tank in FindObjectsOfType<Tank>())
            {
                tank.airSpeedDragFactor = Class1.TechDrag * 0.0005f;
                tank.airSpeedAngularDragFactor = Class1.TechDrag * 0.0005f;
            }
        }
        public static void ResetWorldDrag()
        {
            Globals.inst.airSpeedDrag = Class1.WorldDrag;
            foreach (var rbody in FindObjectsOfType<Rigidbody>())
            {
                rbody.drag = Globals.inst.airSpeedDrag;
            }
        }

        public static bool TextSliderPair(string label, ref string input, ref float value, float min, float max, bool clampText, float round = 0.05f) // Copied from Control Block Overhaul branch
        {
            GUILayout.Label(label + value.ToString());

            GUILayout.BeginHorizontal();
            GUI.changed = false;
            bool Changed = false;
            if (input == null) input = value.ToString();
            input = GUILayout.TextField(input, GUILayout.MaxWidth(80));
            if (GUI.changed && float.TryParse(input, out float sValue))
            {
                if (clampText)
                    sValue = Mathf.Clamp(sValue, min, max);
                Changed = sValue != value;
                value = sValue;
            }

            GUI.changed = false;
            var tValue = Mathf.Round(GUILayout.HorizontalSlider(value, min, max) / round) * round;
            if (GUI.changed)
            {
                input = tValue.ToString();
                Changed |= tValue != value;
                value = tValue;
            }
            GUILayout.EndHorizontal();
            return Changed;
        }

        static Texture2D duplicateTexture(Texture source) // https://stackoverflow.com/a/44734346
        {
            RenderTexture renderTex = RenderTexture.GetTemporary(
                        source.width,
                        source.height,
                        0,
                        RenderTextureFormat.Default,
                        RenderTextureReadWrite.Linear);

            Graphics.Blit(source, renderTex);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;
            Texture2D readableText = new Texture2D(source.width, source.height);
            readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableText.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);
            return readableText;
        }

        internal class GUIDisplay : MonoBehaviour
        {
            public GUIConfig inst;
            public void OnGUI()
            {
                inst.WindowRect = GUI.Window(51809, inst.WindowRect, inst.MiscPage, "Misc Configuration");
            }
        }
    }
}