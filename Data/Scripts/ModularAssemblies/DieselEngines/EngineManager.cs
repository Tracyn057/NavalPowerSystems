using NavalPowerSystems.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;

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
                if (system.Controller == null || !system.Controller.IsWorking)
                {
                    system.Logic.Update10(0f);
                }
                else
                {
                    system.Logic.Update10(system.TargetThrottle);
                }
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
