using NavalPowerSystems.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Components;

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
    }
}
