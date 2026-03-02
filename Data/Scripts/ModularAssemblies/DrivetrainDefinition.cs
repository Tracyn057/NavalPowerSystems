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
                "NPSDrivetrainShaftTubeEndVertical",
                "NPSDrivetrainShaftTubeEndL",
                "NPSDrivetrainTubeSealSlope2",

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
                //Gearboxes
                {
                    "NPSDrivetrainMRG", new Dictionary<Vector3I, string[]>
                    {
                        [new Vector3I(0,-1,-2)] = AllowedDriveshaftConnections,
                        [new Vector3I(-1,1,2)] = AllowedEngineConnections,
                        [new Vector3I(1,1,2)] = AllowedEngineConnections

                    }
                },
                //Engines
                {
                    "NPSDrivetrainDriveshaft", new Dictionary<Vector3I, string[]>
                    {
                        [Vector3I.Forward] = AllowedDriveshaftConnections,
                        [Vector3I.Backward] = AllowedDriveshaftConnections,
                    }
                },
                //Driveshafts
                {
                    "NPSDrivetrainDriveshaft", new Dictionary<Vector3I, string[]>
                    {
                        [Vector3I.Forward] = AllowedDriveshaftConnections,
                        [Vector3I.Backward] = AllowedDriveshaftConnections,
                    }
                },
                {
                    "NPSDrivetrainShaftTubeEndVertical", new Dictionary<Vector3I, string[]>
                    {
                        [Vector3I.Forward] = AllowedPropellerConnections,
                        [Vector3I.Backward] = AllowedDriveshaftConnections,
                    }
                },
                {
                    "NPSDriveshaftTube", new Dictionary<Vector3I, string[]>
                    {
                        [Vector3I.Forward] = AllowedDriveshaftConnections,
                        [Vector3I.Forward] = AllowedPropellerConnections,
                        [Vector3I.Backward] = AllowedDriveshaftConnections,
                    }
                },
                //Propellers
                {
                    "NPSDrivetrainProp43", new Dictionary<Vector3I, string[]>
                    {
                        [Vector3I.Backward] = AllowedDriveshaftConnections,
                    }
                },
                {
                    "NPSDrivetrainProp44", new Dictionary<Vector3I, string[]>
                    {
                        [Vector3I.Backward] = AllowedDriveshaftConnections,
                    }
                },
                {
                    "NPSDrivetrainProp45", new Dictionary<Vector3I, string[]>
                    {
                        [Vector3I.Backward] = AllowedDriveshaftConnections,
                    }
                },
            },
        };

        private static readonly string[] AllowedDriveshaftConnections =
        {
            "NPSDrivetrainDriveshaft",

            "NPSDrivetrainShaftTubeEndVertical",
            "NPSDrivetrainShaftTubeEndL",
            "NPSDrivetrainTubeSealSlope2",
        };

        private static readonly string[] AllowedPropellerConnections =
        {
            "NPSDrivetrainProp34",
            "NPSDrivetrainProp44",
            "NPSDrivetrainProp54",
        };

        private static readonly string[] AllowedEngineConnections =
        {
            "NPSDieselTurbine2MW",
            "NPSDieselTurbine5MW",
            "NPSDieselTurbine12MW",
            "NPSDieselTurbine25MW",
            "NPSDieselTurbine40MW",
            "NPSDieselEngine500KW",
            "NPSDieselEngine15MW",
            "NPSDieselEngine25MW",
        };
        private static readonly string[] AllowedGearboxConnections =
        {
            "NPSDrivetrainMRG",
        };
    }
}
