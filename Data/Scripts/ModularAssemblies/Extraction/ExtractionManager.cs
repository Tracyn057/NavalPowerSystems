using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace NavalPowerSystems.Extraction
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    internal class ExtractionManager : MySessionComponentBase
    {
        public static ExtractionManager Instance = new ExtractionManager();
        public ModularDefinition ExtractionDefinition;
        public static Dictionary<int, ExtractionSystem> ExtractionSystems = new Dictionary<int, ExtractionSystem>();

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
            if (!ExtractionSystems.ContainsKey(assemblyId))
                ExtractionSystems.Add(assemblyId, new ExtractionSystem(assemblyId));

            if (block.BlockDefinition.SubtypeName == "NPSExtractionOilDerrick")
            {
                var logic = block.GameLogic?.GetAs<DerrickLogic>();
                logic._needsRefresh = true;
            }

            ExtractionSystems[assemblyId].AddPart(block);
        }

        public void OnPartRemove(int assemblyId, IMyCubeBlock block, bool isBasePart)
        {
            if (!ExtractionSystems.ContainsKey(assemblyId))
                return;

            if (!isBasePart)
            {
                if (!block.BlockDefinition.SubtypeName == "NPSExtractionOilDerrick")
                {
                    var logic = block.GameLogic?.GetAs<DerrickLogic>();
                    logic._needsRefresh = true;
                }
                ExtractionSystems[assemblyId].RemovePart(block);
            }
            else
            {
                ExtractionSystems[assemblyId].RemovePart(block);
            }
        }
    }
}