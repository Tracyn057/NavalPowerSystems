using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.Components;
using VRage.Utils;

namespace Humanoid.GimbalJetThruster
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class GimbalJetThrusterMod : MySessionComponentBase
    {
        public static GimbalJetThrusterMod Instance;

        public override void LoadData()
        {
            Instance = this;
        }

        protected override void UnloadData()
        {
            Instance = null;
        }
    }
}
