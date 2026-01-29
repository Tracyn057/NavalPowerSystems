using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using NavalPowerSystems.Communication;

namespace NavalPowerSystems.Production
{
    public class ProductionSystem
    {
        public readonly int AssemblyId;
        private static ModularDefinitionApi ModularApi => NavalPowerSystems.ModularDefinition.ModularApi;

        public IMyGasTank CrudeInput;
        public IMyGasTank FuelOutput;
        public IMyGasTank FuelInput;
        public IMyGasTank DieselOutput;
        public IMyCubeBlock RefineryBlock;

        public bool IsCrackerFunctional => RefineryBlock?.BlockDefinition.SubtypeName == "NPSProductionOilCracker" && CrudeInput != null && FuelOutput != null;
        public bool IsRefineryFunctional => RefineryBlock?.BlockDefinition.SubtypeName == "NPSProductionFuelRefinery" && FuelInput != null && DieselOutput != null;
        public bool IsFunctional => IsCrackerFunctional || IsRefineryFunctional;

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
            else if (subtype == "NPSProductionFuelOutput") FuelOutput = tank;
            else if (subtype == "NPSProductionFuelInput") FuelInput = tank;
            else if (subtype == "NPSProductionDieselOutput") DieselOutput = tank;
            else if (subtype == "NPSProductionOilCracker" || subtype == "NPSProductionFuelRefinery")
                RefineryBlock = block;
        }

        public void RemovePart(IMyCubeBlock block)
        {
            string subtype = block.BlockDefinition.SubtypeName;

            if (subtype == "NPSProductionCrudeInput") CrudeInput = null;
            else if (subtype == "NPSProductionFuelOutput") FuelOutput = null;
            else if (subtype == "NPSProductionFuelInput") FuelInput = null;
            else if (subtype == "NPSProductionDieselOutput") DieselOutput = null;
            else if (subtype == "NPSProductionOilCracker" || subtype == "NPSProductionFuelRefinery")
                RefineryBlock = null;
        }
    }
}