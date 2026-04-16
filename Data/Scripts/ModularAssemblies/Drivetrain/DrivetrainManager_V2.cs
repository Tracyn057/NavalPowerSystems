using NavalPowerSystems.Communication;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace NavalPowerSystems.Drivetrain_V2
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    internal class DrivetrainManager_V2 : MySessionComponentBase
    {
        private int _ticks;
        public static DrivetrainManager_V2 Instance { get; private set; } = null;
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        public IEnumerable<DrivetrainSystem_V2> GetAssemblies => DrivetrainSystems.Values;
        private Dictionary<int, DrivetrainSystem_V2> DrivetrainSystems = new Dictionary<int, DrivetrainSystem_V2>();
        private Dictionary<IMyCubeGrid, NavalGridManager> GridManagers = new Dictionary<IMyCubeGrid, NavalGridManager>();



        public override void LoadData()
        {
            Instance = this;
            ModularApi.Log("DrivetrainManager Loaded.");
        }

        protected override void UnloadData()
        {
            foreach (var drivtrain in DrivetrainSystems.Values)
            {
                drivtrain.Unload();
            }
            Instance = null;
            ModularApi.Log("DrivetrainManager closed.");
        }

        public override void UpdateAfterSimulation()
        {
            foreach (var drivetrain in DrivetrainSystems.Values)
            {
                drivetrain.UpdateTick();
            }

            if (_ticks % 10 == 0)
            {
                foreach (var drivetrain in DrivetrainSystems.Values)
                {
                    drivetrain.UpdateTick10();
                }
            }

            if (_ticks % 100 == 0)
            {
                Update100();
            }
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

        public static void OnPartAdd(int assemblyId, IMyCubeBlock block, bool isBasePart)
        {
            if (Instance == null) return;

            DrivetrainSystem_V2 drivetrain;
            if (!Instance.DrivetrainSystems.TryGetValue(assemblyId, out drivetrain))
            {
                drivetrain = new DrivetrainSystem(assemblyId);
                Instance.DrivetrainSystems.Add(assemblyId, drivetrain);
                //ModularApi.Log($"DrivetrainManager created new assembly {assemblyId}");
            }

            drivetrain.AddPart(block);
        }

        public static void OnPartRemove(int assemblyId, IMyCubeBlock block, bool isBasePart)
        {
            DrivetrainSystem_V2 drivetrain;
            if (Instance == null || !Instance.DrivetrainSystems.TryGetValue(assemblyId, out drivetrain))
                return;

            drivetrain.RemovePart(block);
        }

        public static void OnPartDestroy(int assemblyId, IMyCubeBlock block, bool isBasePart)
        {
            DrivetrainSystem_V2 drivetrain;
            if (Instance == null || !Instance.DrivetrainSystems.TryGetValue(assemblyId, out drivetrain))
                return;

            //drivetrain.OnPartDestroy(block);
        }

        public static void OnAssemblyClose(int assemblyId)
        {
            DrivetrainSystem_V2 drivetrain;
            if (Instance == null || !Instance.DrivetrainSystems.TryGetValue(assemblyId, out drivetrain))
                return;

            drivetrain.Unload();
            Instance.DrivetrainSystems.Remove(assemblyId);
            ModularApi.Log($"DrivetrainManager removed assembly {assemblyId}");
        }

        public DrivetrainSystem_V2 GetDrivetrainSystem(int assemblyId)
        {
            DrivetrainSystem_V2 drivetrain;
            if (Instance == null || !Instance.DrivetrainSystems.TryGetValue(assemblyId, out drivetrain))
                return null;
            return drivetrain;
        }
    }
}
