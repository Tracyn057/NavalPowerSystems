using NavalPowerSystems.Common;
using NavalPowerSystems.Communication;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace NavalPowerSystems.DieselEngines
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation | MyUpdateOrder.Simulation)]
    internal class EngineManager : MySessionComponentBase
    {
        public static EngineManager Instance = new EngineManager();
        public static Dictionary<int, EngineSystem> EngineSystems = new Dictionary<int, EngineSystem>();
        internal readonly HashSet<IMyTerminalControl> ThrottleControls = new HashSet<IMyTerminalControl>();
        public ModularDefinition EngineDefinition;
        private int _ticks;
        public bool _controlsCreated = false;
        private static ModularDefinitionApi ModularApi => NavalPowerSystems.ModularDefinition.ModularApi;

        private bool _initialized = false;

        public void Load()
        {
            Instance = this;
        }

        public void Unload()
        {
            Instance = null;
            EngineSystems.Clear();
        }

        public void UpdateTick()
        {
            if (_ticks % 10 == 0)
                Update10();

            _ticks++;
        }

        public void Update10()
        {
            MyAPIGateway.Utilities.ShowMessage("Naval Power Systems", $"Update10");
            if (!_initialized)
            {
                MyAPIGateway.Utilities.ShowMessage("Naval Power Systems", $"Init Engine Controls");
                _initialized = true;
                InitTerminal();
            }

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
                if (system.Controller != null && system.Controller.HasLocalPlayerAccess())
                {
                    system.Controller.RefreshCustomInfo();
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

            if (block.BlockDefinition.SubtypeName == "NPSEnginesController")
            {
                var terminal = block as IMyTerminalBlock;

                terminal.RefreshCustomInfo();
            }

            EngineSystems[assemblyId].AddPart(block);
        }

        public void OnPartRemove(int assemblyId, IMyCubeBlock block, bool isBasePart)
        {
            if (block.BlockDefinition.SubtypeName == "NPSEnginesController")
            {
                var terminal = block as IMyTerminalBlock;
            }

            if (EngineSystems.ContainsKey(assemblyId))
                EngineSystems[assemblyId].RemovePart(block);
        }

        private void InitTerminal()
        {
            EngineTerminalHelpers.AddEngineControls<IMyTerminalBlock>(this);
        }
    }
}
