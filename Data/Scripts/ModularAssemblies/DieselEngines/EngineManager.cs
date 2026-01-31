using NavalPowerSystems.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;
using VRageMath;

namespace NavalPowerSystems.DieselEngines
{
    internal class EngineManager
    {
        public static EngineManager Instance = new EngineManager();
        public static Dictionary<int, EngineSystem> EngineSystems = new Dictionary<int, EngineSystem>();
        public ModularDefinition EngineDefinition;
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

                float finalThrottle;

                if (system.TargetSpeedMS > 0f)
                {
                    float currentSpeed = (float)system.Controller.CubeGrid.LinearVelocity.Length();

                    if (currentSpeed < system.TargetSpeedMS)
                        system.CruiseThrottle += 0.01f;
                    else if (currentSpeed > system.TargetSpeedMS + 0.2f)
                        system.CruiseThrottle -= 0.01f;

                    system.CruiseThrottle = MathHelper.Clamp(system.CruiseThrottle, 0f, 1.25f);
                    finalThrottle = system.CruiseThrottle;
                }
                else
                {
                    finalThrottle = system.TargetThrottle;
                }
                system.Logic.Update10(system.TargetThrottle);
            }
        }

        public void Update100()
        {
            var currentAssemblies = ModularApi.GetAllAssemblies();
            var keysToRemove = EngineSystems.Keys.Where(id => !currentAssemblies.Contains(id)).ToList();

            foreach (var id in keysToRemove)
                EngineSystems.Remove(id);
        }

        public void RunSystem(EngineSystem system)
        {
            float finalThrottle = system.TargetThrottle;

            if (system.TargetSpeedMS > 0)
            {
                float currentSpeed = (float)system.Controller.CubeGrid.LinearVelocity.Length();

                if (currentSpeed < system.TargetSpeedMS)
                    system.CruiseThrottle += 0.02f;
                else
                    system.CruiseThrottle -= 0.02f;

                finalThrottle = MathHelper.Clamp(system.CruiseThrottle, 0f, 1.25f);
            }

            system.Logic.Update10(finalThrottle);
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
