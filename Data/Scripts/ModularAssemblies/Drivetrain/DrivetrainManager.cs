using NavalPowerSystems.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace NavalPowerSystems.Drivetrain
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    internal class DrivetrainManager : MySessionComponentBase
    {
        private int _ticks;
        public static DrivetrainManager Instance = new DrivetrainManager();
        public ModularDefinition DrivetrainDefinition;
        public static Dictionary<int, DrivetrainSystem> DrivetrainSystems = new Dictionary<int, DrivetrainSystem>();
        private static ModularDefinitionApi ModularApi => NavalPowerSystems.ModularDefinition.ModularApi;

        public void Load()
        {
            Instance = this;
        }

        public void Unload()
        {
            Instance = null;
        }

        public void UpdateTick()
        {
            foreach (var driveSystem in DrivetrainSystems.Values)
                driveSystem.UpdateTick();

            if (_ticks % 10 == 0)
            {
                foreach (var driveSystem in DrivetrainSystems.Values)
                {
                    driveSystem.UpdateTick10();
                }
            }

            if (_ticks % 100 == 0)
                Update100();

            _ticks++;
        }

        private void Update100()
        {
            var systems = ModularApi.GetAllAssemblies();
            foreach (var driveSystem in DrivetrainSystems.Values.ToList())
                // Remove invalid systems
                if (!systems.Contains(driveSystem.AssemblyId))
                    DrivetrainSystems.Remove(driveSystem.AssemblyId);
        }

        public void OnPartAdd(int assemblyId, IMyCubeBlock block, bool isBasePart)
        {
            if (!DrivetrainSystems.ContainsKey(assemblyId))
                DrivetrainSystems.Add(assemblyId, new DrivetrainSystem(assemblyId));

            DrivetrainSystems[assemblyId].AddPart(block);
        }

        public void OnPartRemove(int assemblyId, IMyCubeBlock block, bool isBasePart)
        {
            if (!DrivetrainSystems.ContainsKey(assemblyId))
                return;

            if (!isBasePart)
                DrivetrainSystems[assemblyId].RemovePart(block);

        }
    }
}
