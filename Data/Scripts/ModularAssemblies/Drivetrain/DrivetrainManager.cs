using NavalPowerSystems.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace NavalPowerSystems.Drivetrain
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    internal class DrivetrainManager : MySessionComponentBase
    {
        private int Ticks;
        public static DrivetrainManager Instance { get; private set; } = null;
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        public IEnumerable<DrivetrainSystem> GetAssemblies => DrivetrainSystems.Values;
        public Dictionary<int, DrivetrainSystem> DrivetrainSystems = new Dictionary<int, DrivetrainSystem>();
        public Dictionary<IMyCubeGrid, NavalGridManager> GridManagers = new Dictionary<IMyCubeGrid, NavalGridManager>();

        public override void LoadData()
        {
            Instance = this;
            ModularApi.Log("DrivetrainManager Loaded.");
        }

        protected override void UnloadData()
        {
            Instance = null;
            ModularApi.Log("DrivetrainManager closed.");
        }

        public override void UpdateAfterSimulation()
        {
            foreach (var system in DrivetrainSystems.Values)
            {
                //system.UpdateTick();
            }
            foreach (var grid in GridManagers.Values)
            {
                grid.UpdateTick();
            }

            if (Ticks % 10 == 0)
            {
                foreach (var system in DrivetrainSystems.Values)
                {
                    system.UpdateTick10();
                }
            }

            if (Ticks % 100 == 0)
            {
                Update100();
                foreach (var grid in GridManagers.Values)
                {
                    grid.UpdateTick100();
                }
            }
            Ticks++;
        }

        private void Update100()
        {
            var assemblies = ModularApi.GetAllAssemblies();

            foreach (var driveSystem in DrivetrainSystems.Values.ToList())
                if (!assemblies.Contains(driveSystem.AssemblyId))
                    DrivetrainSystems.Remove(driveSystem.AssemblyId);

            foreach (var gridManager in GridManagers.Values.ToList())
            {
                var gridAssemblies = ModularApi.GetGridAssemblies(gridManager.IMyGrid);
                if (gridManager.IMyGrid == null || gridAssemblies.Count() == 0)
                    GridManagers.Remove(gridManager.IMyGrid);
            }
        }

        public static void OnPartAdd(int assemblyId, IMyCubeBlock block, bool isBasePart)
        {
            if (Instance == null) return;

            DrivetrainSystem drivetrain;
            NavalGridManager navalGridManager;
            IMyCubeGrid grid = null;

            if (!Instance.DrivetrainSystems.TryGetValue(assemblyId, out drivetrain))
            {
                drivetrain = new DrivetrainSystem(assemblyId);
                Instance.DrivetrainSystems.Add(assemblyId, drivetrain);

                grid = ModularApi.GetAssemblyGrid(assemblyId);
            }

            if (grid != null && !Instance.GridManagers.TryGetValue(grid, out navalGridManager))
            {
                navalGridManager = new NavalGridManager(grid);
                Instance.GridManagers.Add(grid, navalGridManager);
            }

            drivetrain.AddPart(block);
        }

        public static void OnPartRemove(int assemblyId, IMyCubeBlock block, bool isBasePart)
        {
            DrivetrainSystem drivetrain;
            if (Instance == null || !Instance.DrivetrainSystems.TryGetValue(assemblyId, out drivetrain))
                return;

            drivetrain.RemovePart(block);
        }

        public static void OnPartDestroy(int assemblyId, IMyCubeBlock block, bool isBasePart)
        {
            DrivetrainSystem drivetrain;
            if (Instance == null || !Instance.DrivetrainSystems.TryGetValue(assemblyId, out drivetrain))
                return;

            //drivetrain.OnPartDestroy(block);
        }

        public static void OnAssemblyClose(int assemblyId)
        {
            DrivetrainSystem drivetrain;
            if (Instance == null || !Instance.DrivetrainSystems.TryGetValue(assemblyId, out drivetrain))
                return;

            Instance.DrivetrainSystems.Remove(assemblyId);
            //ModularApi.Log($"DrivetrainManager removed assembly {assemblyId}");
        }

        public DrivetrainSystem GetDrivetrainSystem(int assemblyId)
        {
            DrivetrainSystem drivetrain;
            if (Instance == null || !Instance.DrivetrainSystems.TryGetValue(assemblyId, out drivetrain))
                return null;
            return drivetrain;
        }

        public NavalGridManager GetGridManager(IMyCubeGrid grid)
        {
            NavalGridManager manager;
            if (GridManagers.TryGetValue(grid, out manager))
            {
                return manager;
            }
            return null;
        }
    }
}
