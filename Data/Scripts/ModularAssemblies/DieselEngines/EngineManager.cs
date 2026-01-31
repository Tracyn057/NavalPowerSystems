using EmptyKeys.UserInterface;
using NavalPowerSystems.Communication;
using NavalPowerSystems.Extraction;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageRender.Utils;
using static NavalPowerSystems.Config;

namespace NavalPowerSystems.DieselEngines
{
    internal class EngineManager
    {
        public static EngineManager Instance;
        public static Dictionary<int, EngineSystem> EngineSystems = new Dictionary<int, EngineSystem>();
        private static ModularDefinitionApi ModularApi => NavalPowerSystems.ModularDefinition.ModularApi;

        public void Load()
        {
            Instance = this;
        }

        public void Unload()
        {
            Instance = null;
            EngineSystems.Clear();
        }

        public void Update10()
        {
            foreach (var system in EngineSystems.Values)
            {
                system.Update();
            }
        }

        public void Update100()
        {
            var currentAssemblies = ModularApi.GetAllAssemblies();
            var keysToRemove = EngineSystems.Keys.Where(id => !currentAssemblies.Contains(id)).ToList();

            foreach (var id in keysToRemove)
                EngineSystems.Remove(id);
        }

        public void OnPartAdd(int assemblyId, IMyCubeBlock block, bool isBasePart)
        {
            if (!EngineSystems.ContainsKey(assemblyId))
                EngineSystems.Add(assemblyId, new EngineSystem(assemblyId));

            EngineSystems[assemblyId].AddPart(block);
        }

        public void OnPartRemove(int assemblyId, IMyCubeBlock block, bool isBasePart)
        {
            if (EngineSystems.ContainsKey(assemblyId))
                EngineSystems[assemblyId].RemovePart(block);
        }
    }
}
