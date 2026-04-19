using System.Collections.Generic;
using VRageMath;
using static NavalPowerSystems.Communication.DefinitionDefs;
using NavalPowerSystems.Drivetrain_V2;

namespace NavalPowerSystems
{
    internal partial class ModularDefinition
    {
        // You can declare functions in here, and they are shared between all other ModularDefinition files.
        // However, for all but the simplest of assemblies it would be wise to have a separate utilities class.

        // This is the important bit.
        private ModularPhysicalDefinition DrivetrainDefinition_V2 => new ModularPhysicalDefinition
        {
            // Unique name of the definition.
            Name = "Drivetrain_Definition_V2",

            //Boop
            OnInit = null,

            // Triggers whenever a new part is added to an assembly.
            OnPartAdd = DrivetrainManager_V2.OnPartAdd,

            // Triggers whenever a part is removed from an assembly.
            OnPartRemove = DrivetrainManager_V2.OnPartRemove,

            // Triggers whenever a part is destroyed, just after OnPartRemove.
            OnPartDestroy = DrivetrainManager_V2.OnPartDestroy,

            OnAssemblyClose = DrivetrainManager_V2.OnAssemblyClose,

            // Optional - if this is set, an assembly will not be created until a baseblock exists.
            BaseBlockSubtype = null,

            // All SubtypeIds that can be part of this assembly.
            AllowedBlockSubtypes = new[]
            {
                //Engines
                "NPS_Turbine_LM2500",
                "NPS_Turbine_LM2500Plus",
                "NPS_Turbine_LM2500PlusG4",

                //Gearboxes
                "NPS_Gearbox_MRG",
                "NPS_Gearbox_DoublePlanetary",

                //Driveshafts
                "NPS_Driveshaft",
                "NPS_Driveshaft_Marked",
                "NPS_Driveshaft_Long",
                "NPS_Driveshaft_Long_Marked",
                "NPS_Driveshaft_LinearGearbox1",
                "NPS_Driveshaft_LinearGearbox2",
                "NPS_Driveshaft_TubeSealEnclosed1",
                "NPS_Driveshaft_TubeSealSlope1",
                "NPS_Driveshaft_TubeSealSlope1Corner",
                "NPS_Driveshaft_TubeSealSlope2",
                "NPS_Driveshaft_TubeSealSlope2Corner",
                "NPS_Driveshaft_EndTubeV1",

                //Propellers
                "NPS_Propeller_4m3b",
                "NPS_Propeller_4m4b",
                "NPS_Propeller_4m5b"

            },

            // Allowed connection directions & whitelists, measured in blocks.
            // If an allowed SubtypeId is not included here, connections are allowed on all sides.
            // If the connection type whitelist is empty, all allowed subtypes may connect on that side.
            AllowedConnections = new Dictionary<string, Dictionary<Vector3I, string[]>>
            {
                //Engines
                {
                    "NPS_Turbine_LM2500", new Dictionary<Vector3I, string[]>()
                    {
                        [new Vector3I(0, 0, -2)] = AllowedEngineOutputConnections
                    }
                },
                {
                    "NPS_Turbine_LM2500Plus", new Dictionary<Vector3I, string[]>()
                    {
                        [new Vector3I(0, 0, -2)] = AllowedEngineOutputConnections
                    }
                },
                {
                    "NPS_Turbine_LM2500PlusG4", new Dictionary<Vector3I, string[]>()
                    {
                        [new Vector3I(0, 0, -2)] = AllowedEngineOutputConnections
                    }
                },

                //Gearboxes
                {
                    //Needs refiguring
                    "NPS_Gearbox_MRG", new Dictionary<Vector3I, string[]>()
                    {
                        [new Vector3I(1, 0.5f, -2)] = AllowedGearboxConnections,
                        [new Vector3I(-1, 0.5f, -2)] = AllowedGearboxConnections
                    }
                },
                {
                    "NPS_Gearbox_DoublePlanetary", new Dictionary<Vector3I, string[]>()
                    {
                        [new Vector3I(0, 0, -1f)] = AllowedGearboxConnections,
                        [new Vector3I(0, 0, 2f)] = AllowedGearboxConnections
                    }
                },

                //Propellers
                {
                    "NPS_Propeller_4m3b", new Dictionary<Vector3I, string[]>()
                    {
                        [new Vector3I(0, 0, 1)] = AllowedPropellerInputConnections
                    }
                },
                {
                    "NPS_Propeller_4m4b", new Dictionary<Vector3I, string[]>()
                    {
                        [new Vector3I(0, 0, 1)] = AllowedPropellerInputConnections
                    }
                },
                {
                    "NPS_Propeller_4m5b", new Dictionary<Vector3I, string[]>()
                    {
                        [new Vector3I(0, 0, 1)] = AllowedPropellerInputConnections
                    }
                },

                //Driveshafts
                {
                    "NPS_Driveshaft", new Dictionary<Vector3I, string[]>()
                    {
                        [new Vector3I(0, 0, 1)] = AllowedDriveshaftConnections,
                        [new Vector3I(0, 0, -1)] = AllowedDriveshaftConnections,
                    }
                },
                {
                    "NPS_Driveshaft_Marked", new Dictionary<Vector3I, string[]>()
                    {
                        [new Vector3I(0, 0, 1)] = AllowedDriveshaftConnections,
                        [new Vector3I(0, 0, -1)] = AllowedDriveshaftConnections,
                    }
                },
                {
                    "NPS_Driveshaft_Long", new Dictionary<Vector3I, string[]>()
                    {
                        [new Vector3I(0, 0, 1)] = AllowedDriveshaftConnections,
                        [new Vector3I(0, 0, -1)] = AllowedDriveshaftConnections,
                    }
                },
                {
                    "NPS_Driveshaft_Long_Marked", new Dictionary<Vector3I, string[]>()
                    {
                        [new Vector3I(0, 0, 1)] = AllowedDriveshaftConnections,
                        [new Vector3I(0, 0, -1)] = AllowedDriveshaftConnections,
                    }
                },
                {
                    "NPS_Driveshaft_LinearGearbox1", new Dictionary<Vector3I, string[]>()
                    {
                        [new Vector3I(0, 0, 1)] = AllowedDriveshaftConnections,
                        [new Vector3I(0, 1, -1)] = AllowedDriveshaftConnections,
                    }
                },
                {
                    "NPS_Driveshaft_LinearGearbox2", new Dictionary<Vector3I, string[]>()
                    {
                        [new Vector3I(0, 0, 1)] = AllowedDriveshaftConnections,
                        [new Vector3I(0, 2, -1)] = AllowedDriveshaftConnections,
                    }
                },
                {
                    "NPS_Driveshaft_TubeSealEnclosed1", new Dictionary<Vector3I, string[]>()
                    {
                        [new Vector3I(0, 0, 1)] = AllowedDriveshaftConnections,
                        [new Vector3I(0, 0, -1)] = AllowedDriveshaftConnections,
                    }
                },
                {
                    "NPS_Driveshaft_TubeSealSlope1", new Dictionary<Vector3I, string[]>()
                    {
                        [new Vector3I(0, 0, 1)] = AllowedDriveshaftConnections,
                        [new Vector3I(0, 0, -1)] = AllowedDriveshaftConnections,
                    }
                },
                {
                    "NPS_Driveshaft_TubeSealSlope1Corner", new Dictionary<Vector3I, string[]>()
                    {
                        [new Vector3I(0, 0, 1)] = AllowedDriveshaftConnections,
                        [new Vector3I(0, 0, -1)] = AllowedDriveshaftConnections,
                    }
                },
                {
                    "NPS_Driveshaft_TubeSealSlope2", new Dictionary<Vector3I, string[]>()
                    {
                        [new Vector3I(0, 0, 1)] = AllowedDriveshaftConnections,
                        [new Vector3I(0, 0, -1)] = AllowedDriveshaftConnections,
                    }
                },
                {
                    "NPS_Driveshaft_TubeSealSlope2Corner", new Dictionary<Vector3I, string[]>()
                    {
                        [new Vector3I(0, 0, 3)] = AllowedDriveshaftConnections,
                        [new Vector3I(0, 0, -2)] = AllowedDriveshaftConnections,
                    }
                },
                {
                    "NPS_Driveshaft_EndTubeV1", new Dictionary<Vector3I, string[]>()
                    {
                        [new Vector3I(0, 0, 2)] = AllowedDriveshaftConnections,
                        [new Vector3I(0, 0, -1)] = AllowedPropellersOnly,
                    }
                },
            },
        };

        private static readonly string[] AllowedEngineOutputConnections =
        {
            //Gearboxes
            "NPS_Gearbox_MRG",
            "NPS_Gearbox_DoublePlanetary"
        };

        private static readonly string[] AllowedGearboxConnections =
        {
            //Engines
            "NPS_Turbine_MT7",
            "NPS_Turbine_LM2500",
            "NPS_Turbine_LM2500Plus",
            "NPS_Turbine_LM2500PlusG4",
            "NPS_Turbine_MT30",

            //Gearboxes
            "NPS_Gearbox_MRG",
            "NPS_Gearbox_DoublePlanetary",

            //Driveshafts
            "NPS_Driveshaft",
            "NPS_Driveshaft_Marked",
            "NPS_Driveshaft_Long",
            "NPS_Driveshaft_Long_Marked",
            "NPS_Driveshaft_LinearGearbox1",
            "NPS_Driveshaft_LinearGearbox2",
            "NPS_Driveshaft_TubeSealEnclosed1",
            "NPS_Driveshaft_TubeSealSlope1",
            "NPS_Driveshaft_TubeSealSlope1Corner",
            "NPS_Driveshaft_TubeSealSlope2",
            "NPS_Driveshaft_TubeSealSlope2Corner",
            "NPS_Driveshaft_EndTubeV1",

            //Propellers
            "NPS_Propeller_4m3b",
            "NPS_Propeller_4m4b",
            "NPS_Propeller_4m5b"
        };

        private static readonly string[] AllowedPropellerInputConnections =
        {
            //Gearboxes
            "NPS_Gearbox_MRG",
            "NPS_Gearbox_DoublePlanetary",

            //Driveshafts
            "NPS_Driveshaft",
            "NPS_Driveshaft_Marked",
            "NPS_Driveshaft_Long",
            "NPS_Driveshaft_Long_Marked",
            "NPS_Driveshaft_LinearGearbox1",
            "NPS_Driveshaft_LinearGearbox2",
            "NPS_Driveshaft_TubeSealEnclosed1",
            "NPS_Driveshaft_TubeSealSlope1",
            "NPS_Driveshaft_TubeSealSlope1Corner",
            "NPS_Driveshaft_TubeSealSlope2",
            "NPS_Driveshaft_TubeSealSlope2Corner",
            "NPS_Driveshaft_EndTubeV1",
        };

        private static readonly string[] AllowedDriveshaftConnections =
        {
            //Gearboxes
            "NPS_Gearbox_MRG",
            "NPS_Gearbox_DoublePlanetary",

            //Driveshafts
            "NPS_Driveshaft",
            "NPS_Driveshaft_Marked",
            "NPS_Driveshaft_Long",
            "NPS_Driveshaft_Long_Marked",
            "NPS_Driveshaft_LinearGearbox1",
            "NPS_Driveshaft_LinearGearbox2",
            "NPS_Driveshaft_TubeSealEnclosed1",
            "NPS_Driveshaft_TubeSealSlope1",
            "NPS_Driveshaft_TubeSealSlope1Corner",
            "NPS_Driveshaft_TubeSealSlope2",
            "NPS_Driveshaft_TubeSealSlope2Corner",
            "NPS_Driveshaft_EndTubeV1",

            //Propellers
            "NPS_Propeller_4m3b",
            "NPS_Propeller_4m4b",
            "NPS_Propeller_4m5b"
        };

        private static readonly string[] AllowedPropellersOnly =
        {
            //Propellers
            "NPS_Propeller_4m3b",
            "NPS_Propeller_4m4b",
            "NPS_Propeller_4m5b"
        };
    }

}
