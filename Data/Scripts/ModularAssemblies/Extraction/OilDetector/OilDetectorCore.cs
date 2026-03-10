using NavalPowerSystems.Extraction;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace OilExtraction.Detector
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class OilDetectorCore : MySessionComponentBase
    {
        public static OilDetectorCore Instance;
        public IMyModContext ModCtx;

        public OilDetectorCore()
        {
            Instance = this;
        }

        public override void BeforeStart()
        {
            ModCtx = base.ModContext;
            MyAPIGateway.Utilities.MessageEntered += OnMessageEntered;
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.MessageEntered -= OnMessageEntered;
            Instance = null;
        }

        private void OnMessageEntered(string messageText, ref bool sendToOthers)
        {
            if (messageText.ToLower().StartsWith("/oil debug"))
            {
                sendToOthers = false;

                OilMap.oilGenDebug = !OilMap.oilGenDebug;

                string status = OilMap.oilGenDebug ? "ENABLED" : "DISABLED";
                MyAPIGateway.Utilities.ShowMessage("OilSystem", $"Checkerboard Debug Mode: {status}");
            }
        }
    }
}