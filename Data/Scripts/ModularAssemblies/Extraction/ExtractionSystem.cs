using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using System.Collections.Generic;

namespace NavalPowerSystems.Extraction
{
    public class ExtractionSystem
    {
        public readonly int AssemblyId;

        public IMyFunctionalBlock RigBase;
        public IMySlimBlock DrillHead;
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
            var slim = block.SlimBlock;

            if (subtype == "NPSExtractorOilDerrick") 
                RigBase = block as IMyFunctionalBlock;
            else if (subtype == "NPSExtractionDrillHead")
                DrillHead = slim;
            else if (subtype == "NPSExtractionDrillPipe")
                Pipes.Add(slim);
        }

        public void RemovePart(IMyCubeBlock block)
        {
            string subtype = block.BlockDefinition.SubtypeName;

            if (subtype == "NPSExtractorOilDerrick")
                RigBase = null;
            else if (subtype == "NPSExtractionDrillHead") 
                DrillHead = null;
            else if (subtype == "NPSExtractionDrillPipe")
                Pipes.Remove(block.SlimBlock);
        }
    }
}