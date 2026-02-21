using NavalPowerSystems.Communication;
using Sandbox.ModAPI;
using SixLabors.ImageSharp.Formats;
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
        public static DrivetrainManager Instance { get; private set; } = null;
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        public IEnumerable<DrivetrainSystem> GetAssemblies => DrivetrainSystems.Values;
        private Dictionary<int, DrivetrainSystem> DrivetrainSystems = new Dictionary<int, DrivetrainSystem>();
        
        

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

            DrivetrainSystem drivetrain;
            if (!Instance.DrivetrainSystems.TryGetValue(assemblyId, out drivetrain))
            {
                drivetrain = new DrivetrainSystem(assemblyId);
                Instance.DrivetrainSystems.Add(assemblyId, drivetrain);
                ModularApi.Log($"DrivetrainManager created new assembly {assemblyId}");
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

            drivetrain.Unload();
            Instance.DrivetrainSystems.Remove(assemblyId);
            ModularApi.Log($"DrivetrainManager removed assembly {assemblyId}");
        }
    }
}
