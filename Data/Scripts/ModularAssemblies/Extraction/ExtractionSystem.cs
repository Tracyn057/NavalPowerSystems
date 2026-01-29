using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using NavalPowerSystems.Communication;
using System.Collections.Generic;

namespace NavalPowerSystems.Extraction
{
    public class ExtractionSystem
    {
        public readonly int AssemblyId;
        private static ModularDefinitionApi ModularApi => NavalPowerSystems.ModularDefinition.ModularApi;

        public IMyFunctionalBlock RigBase;
        public IMySlimBlock DrillHead;
        public List<IMySlimBlock> Pipes = new List<IMySlimBlock>();
        public IMyGasTank OutputTank;

        public bool IsAssemblyComplete => RigBase != null && DrillHead != null && RigBase.IsWorking;

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
            else if (subtype == "NPSExtractionCrudeOutput") 
                OutputTank = tank;
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
            else if (subtype == "NPSExtractionCrudeOutput") 
                OutputTank = null;
            else if (subtype == "NPSExtractionDrillHead") 
                DrillHead = null;
            else if (subtype == "NPSExtractionDrillPipe")
                Pipes.Remove(block.SlimBlock);
        }
    }
}