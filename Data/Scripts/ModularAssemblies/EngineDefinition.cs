using NavalPowerSystems.DieselEngines;
using NavalPowerSystems.Utilities;
using Sandbox.ModAPI;
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
        private ModularPhysicalDefinition EngineDefinition => new ModularPhysicalDefinition
        {
            // Unique name of the definition.
            Name = "Engine_Definition",

            OnInit = () =>
            {
                EngineManager.Instance.EngineDefinition = this;
                //CommonUtilities.RemoveActions();
                //CommonUtilities.RemoveControls();
            },

            // Triggers whenever a new part is added to an assembly.
            OnPartAdd = (assemblyId, block, isBasePart) =>
            {
                EngineManager.Instance.OnPartAdd(assemblyId, block, isBasePart);
            },

            // Triggers whenever a part is removed from an assembly.
            OnPartRemove = (assemblyId, block, isBasePart) =>
            {
                EngineManager.Instance.OnPartRemove(assemblyId, block, isBasePart);
            },

            // Triggers whenever a part is destroyed, just after OnPartRemove.
            OnPartDestroy = (assemblyId, block, isBasePart) =>
            {

            },

            OnAssemblyClose = (assemblyId) =>
            {
                
            },

            // Optional - if this is set, an assembly will not be created until a baseblock exists.
            BaseBlockSubtype = "NPSEnginesController",

            // All SubtypeIds that can be part of this assembly.
            AllowedBlockSubtypes = new[]
            {
                "NPSEnginesController",
                "NPSDieselTurbine2MW",
                "NPSDieselTurbine5MW",
                "NPSDieselTurbine12MW",
                "NPSDieselTurbine25MW",
                "NPSDieselTurbine40MW",
                "NPSDieselEngine500KW",
                "NPSDieselEngine15MW",
                "NPSDieselEngine25MW"
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
