using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Misc_Mods
{
    public class MiscMods : ModBase
    {
        internal static Logger logger;

        internal static void ConfigureLogger()
        {
            logger = new Logger("MiscMods");
        }

        public override void DeInit()
        {
            Class1.harmony.UnpatchAll(Class1.HarmonyID);
        }

        public override void Init()
        {
            Class1.ApplyPatches();
        }

        public override bool HasEarlyInit()
        {
            return true;
        }

        bool Inited = false;
        public override void EarlyInit()
        {
            if (!Inited)
            {
                Class1.SetupAssets();
                ConfigureLogger();
                Inited = true;
            }
        }
    }
}
