using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace NavalPowerSystems.DieselEngines
{
    internal class EngineSystem
    {
        public readonly int AssemblyId;
        public IMyFunctionalBlock Controller;
        public EngineLogic Logic;
        public float TargetThrottle { get; private set; } = 0f;
        public void SetTargetThrottle(float throttle) => TargetThrottle = throttle;

        public EngineSystem(int id)
        {
            AssemblyId = id;
            Logic = new EngineLogic(id);
        }

        public void AddPart(IMyCubeBlock block)
        {
            if (block == null) return;

            string subtype = block.BlockDefinition.SubtypeName;

            if (subtype == "NPSEnginesController")
            {
                Controller = block as IMyFunctionalBlock;
            }
            else if (Config.EngineSettings.ContainsKey(subtype))
            {
                var stats = Config.EngineSettings[subtype];
                var table = (stats.Type == Config.EngineType.Turbine) ? TurbineEngineConfigs.TurbineFuelTable : DieselEngineConfigs.DieselFuelTable;

                Logic.AddEngine(block as IMyGasTank, stats, table);
            }
        }

        public void RemovePart(IMyCubeBlock block)
        {
            if (block.BlockDefinition.SubtypeName == "NPSEnginesController")
                Controller = null;
            else 
                Logic.RemoveEngine(block);
        }

        public void Update()
        {
            if (Controller == null || !Controller.IsWorking) return;

            float targetInput = 0.5f;
            Logic.Update10(targetInput);
        }
    }
}
