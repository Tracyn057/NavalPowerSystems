using NavalPowerSystems.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NavalPowerSystems.Drivetrain
{
    internal class DrivetrainSystem
    {
        public readonly int AssemblyId;
        private static ModularDefinitionApi ModularApi => NavalPowerSystems.ModularDefinition.ModularApi;



        public DrivetrainSystem(int id)
        {
            AssemblyId = id;
        }

        
    }
}
