using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace NavalPowerSystems.Production
{
    public class ProductionSystem
    {
        public readonly int AssemblyId;

        public IMyGasTank InputTank;
        public IMyCubeBlock RefineryBlock;
        public bool _needsRefresh { get; set; }

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

            _needsRefresh = true;

            if (subtype == "NPSProductionCrudeInput" || subtype == "NPSProductionFuelInput") InputTank = tank;
            else if (subtype == "NPSProductionOilCracker" || subtype == "NPSProductionFuelRefinery") RefineryBlock = block;
        }

        public void RemovePart(IMyCubeBlock block)
        {
            string subtype = block.BlockDefinition.SubtypeName;

            _needsRefresh = true;

            if (subtype == "NPSProductionCrudeInput" || subtype == "NPSProductionFuelInput") InputTank = null;
            else if (subtype == "NPSProductionOilCracker" || subtype == "NPSProductionFuelRefinery") RefineryBlock = null;
        }
    }
}