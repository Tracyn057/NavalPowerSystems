using NavalPowerSystems.Drivetrain;
using System.Collections.Generic;
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

            OnInit = () =>
            {
                //MyAPIGateway.Utilities.ShowMessage("Naval Power Systems", "Extraction Initialized.");
                DrivetrainManager.Instance.DrivetrainDefinition = this;
            },

            // Triggers whenever a new part is added to an assembly.
            OnPartAdd = (assemblyId, block, isBasePart) =>
            {
                //MyAPIGateway.Utilities.ShowMessage("Naval Power Systems", $"Extraction_Definition.OnPartAdd called.\nAssembly: {assemblyId}\nBlock: {block.DisplayNameText}\nIsBasePart: {isBasePart}");
                //MyAPIGateway.Utilities.ShowNotification("Assembly has " + ModularApi.GetMemberParts(assemblyId).Length + " blocks.");

                DrivetrainManager.Instance.OnPartAdd(assemblyId, block, isBasePart);
            },

            // Triggers whenever a part is removed from an assembly.
            OnPartRemove = (assemblyId, block, isBasePart) =>
            {
                //MyAPIGateway.Utilities.ShowMessage("Naval Power Systems", $"Extraction_Definition.OnPartRemove called.\nAssembly: {assemblyId}\nBlock: {block.DisplayNameText}\nIsBasePart: {isBasePart}");
                //MyAPIGateway.Utilities.ShowNotification("Assembly has " + ModularApi.GetMemberParts(assemblyId).Length + " blocks.");

                DrivetrainManager.Instance.OnPartRemove(assemblyId, block, isBasePart);
            },

            // Triggers whenever a part is destroyed, just after OnPartRemove.
            OnPartDestroy = (assemblyId, block, isBasePart) =>
            {
                // You can remove this function, and any others if need be.
                //MyAPIGateway.Utilities.ShowMessage("Naval Power Systems", $"Production_Definition.OnPartDestroy called.\nI hope the explosion was pretty.");
                //MyAPIGateway.Utilities.ShowNotification("Assembly has " + ModularApi.GetMemberParts(assemblyId).Length + " blocks.");
            },

            OnAssemblyClose = (assemblyId) =>
            {
                //MyAPIGateway.Utilities.ShowMessage("Naval Power Systems", $"Production_Definition.OnAssemblyClose called.\nAssembly: {assemblyId}");
            },

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
                "NPSDriveshaftTube",

                //Propellers
                "NPSDrivetrainProp33",

            },

            // Allowed connection directions & whitelists, measured in blocks.
            // If an allowed SubtypeId is not included here, connections are allowed on all sides.
            // If the connection type whitelist is empty, all allowed subtypes may connect on that side.
            AllowedConnections = new Dictionary<string, Dictionary<Vector3I, string[]>>
            {
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
            },
        };

        private static readonly string[] AllowedDriveshaftConnections =
        {
            "NPSDrivetrainDriveshaft",

            "NPSDrivetrainShaftTubeEndVertical",
            "NPSDriveshaftTube",
        };

        private static readonly string[] AllowedPropellerConnections =
        {
            "NPSDrivetrainProp33",
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
    }
}
