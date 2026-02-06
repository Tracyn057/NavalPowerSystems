using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using System.Collections.Generic;

namespace NavalPowerSystems.Extraction
{
    public class ExtractionSystem
    {
        public readonly int AssemblyId;

        public IMyFunctionalBlock RigBase;
        public IMyTerminalBlock DrillHead;
        public List<IMySlimBlock> Pipes = new List<IMySlimBlock>();

        public ExtractionSystem(int id)
        {
            AssemblyId = id;
        }

        public void AddPart(IMyCubeBlock block)
        {
            if (block == null)
                return;

            string subtype = block.BlockDefinition.SubtypeName;
            var tank = block as IMyGasTank;

            if (subtype == "NPSExtractionOilDerrick") 
                RigBase = block as IMyFunctionalBlock;
            else if (subtype == "NPSExtractionDrillHead")
                DrillHead = block as IMyTerminalBlock;
            else if (subtype == "NPSExtractionDrillPipe")
                Pipes.Add(block.SlimBlock);
        }

        public void RemovePart(IMyCubeBlock block)
        {
            string subtype = block.BlockDefinition.SubtypeName;

            if (subtype == "NPSExtractionOilDerrick")
                RigBase = null;
            else if (subtype == "NPSExtractionDrillHead") 
                DrillHead = null;
            else if (subtype == "NPSExtractionDrillPipe")
                Pipes.Remove(block.SlimBlock);
        }
    }
}