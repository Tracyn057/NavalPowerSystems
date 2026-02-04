using NavalPowerSystems.Communication;
using NavalPowerSystems.Production;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace NavalPowerSystems.Drivetrain
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    internal class DrivetrainManager : MySessionComponentBase
    {
        public static DrivetrainManager Instance = new DrivetrainManager();
        public ModularDefinition DrivetrainDefinition;
        public static Dictionary<int, DrivetrainSystem> DrivetrainSystems = new Dictionary<int, DrivetrainSystem>();
        private static ModularDefinitionApi ModularApi => NavalPowerSystems.ModularDefinition.ModularApi;

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
            if (!DrivetrainSystems.ContainsKey(assemblyId))
                DrivetrainSystems.Add(assemblyId, new DrivetrainSystem(assemblyId));

            DrivetrainSystems[assemblyId].AddPart(block);
        }

        public void OnPartRemove(int assemblyId, IMyCubeBlock block, bool isBasePart)
        {
            if (!DrivetrainSystems.ContainsKey(assemblyId))
                return;

            if (!isBasePart)
                DrivetrainSystems[assemblyId].RemovePart(block);

        }
    }
}
