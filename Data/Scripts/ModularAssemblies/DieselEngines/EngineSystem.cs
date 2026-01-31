using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace NavalPowerSystems.DieselEngines
{
    internal class EngineSystem
    {
        public readonly int AssemblyId;
        public IMyFunctionalBlock Controller;
        public EngineLogic Logic;
        public float TargetThrottle { get; private set; } = 0f;
        public void SetTargetThrottle(float throttle) => TargetThrottle = throttle;
        public float TargetSpeedMS = 0f;
        public string UserSpeedInput = "0";
        public float CruiseThrottle = 0f;

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

        public void ParseSpeedInput(string input)
        {
            float parsedValue;
            string cleanInput = input.ToLower().Trim();

            if (cleanInput.Contains("kn") || cleanInput.Contains("kts"))
            {
                string numericPart = cleanInput.Replace("kn", "").Replace("kts", "").Trim();
                if (float.TryParse(numericPart, out parsedValue))
                    TargetSpeedMS = parsedValue * 0.514444f;
            }
            else if (float.TryParse(cleanInput, out parsedValue))
            {
                TargetSpeedMS = parsedValue;
            }
        }
    }
}
