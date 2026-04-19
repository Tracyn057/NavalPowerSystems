
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;

namespace NavalPowerSystems.Common
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class NPSBuildInfoIntegration : MySessionComponentBase
    {
        private const long BuildInfoChannel = 11612;
        private const long BuildInfoModID = 514062285;
        private string NPS = "NavalPowerSystems";

        public override void BeforeStart()
        {
            if (MyAPIGateway.Utilities.IsDedicated) return;

            Utilities.RemoveControlsTanks();
            Utilities.RemoveActionsTanks();
        }
    }
}