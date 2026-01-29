using Draygo.BlockExtensionsAPI;
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
        public DefinitionExtensionsAPI DefExtensions;
        public IMyModContext ModCtx;

        public OilDetectorCore()
        {
            Instance = this;

            DefExtensions = new DefinitionExtensionsAPI(OnApiInit);
        }

        public override void BeforeStart()
        {
            ModCtx = base.ModContext;
            MyAPIGateway.Utilities.MessageEntered += OnMessageEntered;
        }

        private void OnApiInit()
        {
            MyLog.Default.WriteLineAndConsole("## OilDetector: DefAPI Handshake Successful.");
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
                sendToOthers = false; // Don't broadcast the command to the whole server

                OilMap.oilGenDebug = !OilMap.oilGenDebug;

                string status = OilMap.oilGenDebug ? "ENABLED" : "DISABLED";
                MyAPIGateway.Utilities.ShowMessage("OilSystem", $"Checkerboard Debug Mode: {status}");
            }
        }
    }
}