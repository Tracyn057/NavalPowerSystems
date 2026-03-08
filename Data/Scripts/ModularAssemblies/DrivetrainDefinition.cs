using NavalPowerSystems.Drivetrain;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRageMath;
using static NavalPowerSystems.Communication.DefinitionDefs;

namespace NavalPowerSystems
{
    internal partial class ModularDefinition
    {
        // You can declare functions in here, and they are shared between all other ModularDefinition files.
        // However, for all but the simplest of assemblies it would be wise to have a separate utilities class.

        // This is the important bit.
        private ModularPhysicalDefinition DrivetrainDefinition => new ModularPhysicalDefinition
        {
            // Unique name of the definition.
            Name = "Drivetrain_Definition",

            //Boop
            OnInit = null,

            // Triggers whenever a new part is added to an assembly.
            OnPartAdd = DrivetrainManager.OnPartAdd,

            // Triggers whenever a part is removed from an assembly.
            OnPartRemove = DrivetrainManager.OnPartRemove,

            // Triggers whenever a part is destroyed, just after OnPartRemove.
            OnPartDestroy = DrivetrainManager.OnPartDestroy,

            OnAssemblyClose = DrivetrainManager.OnAssemblyClose,

            // Optional - if this is set, an assembly will not be created until a baseblock exists.
            BaseBlockSubtype = null,

            // All SubtypeIds that can be part of this assembly.
            AllowedBlockSubtypes = new[]
            {
                //Engines
                "NPSDieselTurbine2MW",
                "NPSDieselTurbine5MW",
                "NPSDieselTurbine12MW",
                "NPSDieselTurbine25MW",
                "NPSDieselTurbine40MW",
                "NPSDieselEngine500KW",
                "NPSDieselEngine15MW",
                "NPSDieselEngine25MW",

                //Gearboxes
                "NPSDrivetrainMRG",

                //Driveshafts
                "NPSDrivetrainDriveshaft",
                "NPSDrivetrainDriveshaftMarked",
                "NPSDrivetrainDriveshaftLong",
                "NPSDrivetrainDriveshaftLongMarked",
                "NPSDrivetrainLinearGearbox1",
                "NPSDrivetrainLinearGearbox2",
                "NPSDrivetrainTubeSealEnclosed1",
                "NPSDrivetrainTubeSealSlope1",
                "NPSDrivetrainTubeSealSlope1Corner",
                "NPSDrivetrainTubeSealSlope2",
                "NPSDrivetrainTubeSealSlope2Corner",
                "NPSDrivetrainEndTubeV1",

                //Propellers
                "NPSDrivetrainProp34",
                "NPSDrivetrainProp44",
                "NPSDrivetrainProp54",

            },

            // Allowed connection directions & whitelists, measured in blocks.
            // If an allowed SubtypeId is not included here, connections are allowed on all sides.
            // If the connection type whitelist is empty, all allowed subtypes may connect on that side.
            AllowedConnections = new Dictionary<string, Dictionary<Vector3I, string[]>>
            {
               
            },
        };
    }
}
