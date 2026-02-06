using NavalPowerSystems.Communication;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;

namespace NavalPowerSystems.Drivetrain
{
    internal class DrivetrainSystem
    {
        public readonly int AssemblyId;
        public IMyFunctionalBlock Gearbox;
        public List<IMyFunctionalBlock> Inputs = new List<IMyFunctionalBlock>();
        public List<IMyFunctionalBlock> Outputs = new List<IMyFunctionalBlock>();
        public List<IMySlimBlock> Driveshafts = new List<IMySlimBlock>();


        public DrivetrainSystem(int id)
        {
            AssemblyId = id;
        }

        public void AddPart(IMyCubeBlock block)
        {
            if (block == null)
                return;

            string subtype = block.BlockDefinition.SubtypeName;
            var part = block as IMyFunctionalBlock;

            if (subtype == "NPSDrivetrainMRG")
                Gearbox = part;
            else if (Config.PropellerSubtypes.Contains(subtype))
                Outputs.Add((part));
            else if (subtype == "NPSDrivetrainClutch" || subtype == "NPSDrivetrainDirectDrive")
                Inputs.Add((part));
            else if (Config.DriveshaftSubtypes.Contains(subtype))
                Driveshafts.Add((block.SlimBlock));
        }

        public void RemovePart(IMyCubeBlock block)
        {
            string subtype = block.BlockDefinition.SubtypeName;

            if (subtype == "NPSDrivetrainMRG")
                Gearbox = null;
            else if (Config.PropellerSubtypes.Contains(subtype))
                Outputs.Clear() ;
            else if (subtype == "NPSDrivetrainClutch" || subtype == "NPSDrivetrainDirectDrive")
                Inputs.Clear();
            else if (Config.DriveshaftSubtypes.Contains(subtype))
                Driveshafts.Clear();
        }


    }
}
