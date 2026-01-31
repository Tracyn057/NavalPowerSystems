using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using static NavalPowerSystems.Config;
using static VRage.Game.MyObjectBuilder_BehaviorTreeDecoratorNode;

namespace NavalPowerSystems.DieselEngines
{
    internal class EngineSystem
    {
        public readonly int AssemblyId;

        public IMyFunctionalBlock Controller;
        public EngineLogic Logic;

        public EngineSystem(int id)
        {
            AssemblyId = id;
            Logic = new EngineLogic(id);
        }

        public void AddPart(IMyCubeBlock block)
        {
            if (block == null) return;

            string subtype = block.BlockDefinition.SubtypeName;
            var engine = block as IMyGasTank;
            if (engine != null)
            {
               
            }

            if (subtype == "NPSEnginesController")
            {
                Controller = block as IMyFunctionalBlock;
            }
            else if (EngineSettings.ContainsKey(subtype))
            {
                var stats = EngineSettings[subtype];
                var table = (stats.Type == EngineType.Turbine) ? TurbineEngineConfigs.TurbineFuelTable : DieselEngineConfigs.DieselFuelTable;

                Logic.AddEngine(block as IMyGasTank, stats, table);
            }
            
        }

        public void RemovePart(IMyCubeBlock block)
        {
            string subtype = block.BlockDefinition.SubtypeName;

            if (subtype == "NPSEnginesController")
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
