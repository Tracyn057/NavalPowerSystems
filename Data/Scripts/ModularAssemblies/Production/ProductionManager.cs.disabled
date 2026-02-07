using System.Collections.Generic;
using VRage.Game.ModAPI;

namespace NavalPowerSystems.Production
{
    internal class ProductionManager
    {
        public static ProductionManager Instance = new ProductionManager();

        public ModularDefinition ProductionDefinition;
        public static Dictionary<int, ProductionSystem> ProductionSystems = new Dictionary<int, ProductionSystem>();

        public void Load()
        {
            Instance = this;
        }

        public void Unload()
        {
            Instance = null;
        }

        public void OnPartAdd(int assemblyId, IMyCubeBlock block, bool isBasePart)
        {
            if (!ProductionSystems.ContainsKey(assemblyId))
                ProductionSystems.Add(assemblyId, new ProductionSystem(assemblyId));

            var subtype = block.BlockDefinition.SubtypeName;

            if (subtype == "NPSProductionOilCracker" || subtype == "NPSProductionFuelRefinery")
            {
                var logic = block.GameLogic?.GetAs<ProductionLogic>();
                logic._needsRefresh = true;
            }

            ProductionSystems[assemblyId].AddPart(block);
        }

        public void OnPartRemove(int assemblyId, IMyCubeBlock block, bool isBasePart)
        {
            if (!ProductionSystems.ContainsKey(assemblyId))
                return;

            if (!isBasePart)
                ProductionSystems[assemblyId].RemovePart(block);

        }
        
    }
}