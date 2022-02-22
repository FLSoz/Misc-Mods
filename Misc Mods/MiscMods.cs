using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Misc_Mods
{
    public class MiscMods : ModBase
    {
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

        public override void EarlyInit()
        {
            Class1.SetupAssets();
        }
    }
}
