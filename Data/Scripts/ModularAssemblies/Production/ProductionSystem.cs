using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace NavalPowerSystems.Production
{
    public class ProductionSystem
    {
        public readonly int AssemblyId;

        public IMyGasTank CrudeInput;
        public IMyGasTank FuelInput;
        public IMyCubeBlock RefineryBlock;

        public ProductionSystem(int id)
        {
            AssemblyId = id;
        }

        public void AddPart(IMyCubeBlock block)
        {
            if (block == null)
                return;

            string subtype = block.BlockDefinition.SubtypeName;
            var tank = block as IMyGasTank;

            if (subtype == "NPSProductionCrudeInput") CrudeInput = tank;
            else if (subtype == "NPSProductionFuelInput") FuelInput = tank;
            else if (subtype == "NPSProductionOilCracker" || subtype == "NPSProductionFuelRefinery")
                RefineryBlock = block;
        }

        public void RemovePart(IMyCubeBlock block)
        {
            string subtype = block.BlockDefinition.SubtypeName;

            if (subtype == "NPSProductionCrudeInput") CrudeInput = null;
            else if (subtype == "NPSProductionFuelInput") FuelInput = null;
            else if (subtype == "NPSProductionOilCracker" || subtype == "NPSProductionFuelRefinery")
                RefineryBlock = null;
        }
    }
}