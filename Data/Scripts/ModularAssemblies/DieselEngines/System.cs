using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using NavalPowerSystems.Communication;
using System.Collections.Generic;
using EmptyKeys.UserInterface;

namespace NavalPowerSystems.DieselEngines
{
    public class EngineSystem
    {
        public readonly int AssemblyId;

        public IMyFunctionalBlock Controller;
        public IMyGasTank Engine;


        public EngineSystem(int id)
        {
            AssemblyId = id;
        }

        public void AddPart(IMyCubeBlock block)
        {
            if (block == null)
                return;

            string subtype = block.BlockDefinition.SubtypeName;
            var engine = block as IMyGasTank;

            if (subtype == "NPSEnginesController")
                Controller = block as IMyFunctionalBlock;
            else if (Config.Engines.Contains(subtype))
                Engine = engine;
        }

        public void RemovePart(IMyCubeBlock block)
        {
            string subtype = block.BlockDefinition.SubtypeName;

            if (subtype == "NPSEnginesController")
                Controller = null;
            else if (Config.Engines.Contains(subtype))
                Engine = null;
        }
    }
}