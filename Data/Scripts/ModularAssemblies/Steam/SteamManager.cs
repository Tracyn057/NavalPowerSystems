using NavalPowerSystems.Communication;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace NavalPowerSystems.Steam
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    internal class SteamManager : MySessionComponentBase
    {
        private int _ticks;
        public static SteamManager Instance { get; private set; } = null;
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        public IEnumerable<SteamSystem> GetAssemblies => SteamSystems.Values;
        private Dictionary<int, SteamSystem> SteamSystems = new Dictionary<int, SteamSystem>();



        public override void LoadData()
        {
            Instance = this;
            ModularApi.Log("SteamManager Loaded.");
        }

        protected override void UnloadData()
        {
            foreach (var steamSystem in SteamSystems.Values)
            {
                steamSystem.Unload();
            }
            Instance = null;
            ModularApi.Log("SteamManager closed.");
        }

        public override void UpdateAfterSimulation()
        {
            foreach (var steamSystem in SteamSystems.Values)
            {
                steamSystem.UpdateTick();
            }

            if (_ticks % 10 == 0)
            {
                foreach (var steamSystem in SteamSystems.Values)
                {
                    steamSystem.UpdateTick10();
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
            foreach (var steamSystem in SteamSystems.Values.ToList())
                if (!systems.Contains(steamSystem.AssemblyId))
                    SteamSystems.Remove(steamSystem.AssemblyId);
        }

        public static void OnPartAdd(int assemblyId, IMyCubeBlock block, bool isBasePart)
        {
            if (Instance == null) return;

            steamSystem drivetrain;
            if (!Instance.SteamSystems.TryGetValue(assemblyId, out steamSystem))
            {
                steamSystem = new SteamSystem(assemblyId);
                Instance.SteamSystems.Add(assemblyId, steamSystem);
            }

            drivetrain.AddPart(block);
        }

        public static void OnPartRemove(int assemblyId, IMyCubeBlock block, bool isBasePart)
        {
            SteamSystem steamSystem;
            if (Instance == null || !Instance.SteamSystems.TryGetValue(assemblyId, out steamSystem))
                return;

            steamSystem.RemovePart(block);
        }

        public static void OnPartDestroy(int assemblyId, IMyCubeBlock block, bool isBasePart)
        {
            SteamSystem steamSystem;
            if (Instance == null || !Instance.SteamSystems.TryGetValue(assemblyId, out steamSystem))
                return;
        }

        public static void OnAssemblyClose(int assemblyId)
        {
            SteamSystem steamSystem;
            if (Instance == null || !Instance.SteamSystems.TryGetValue(assemblyId, out steamSystem))
                return;

            steamSystem.Unload();
            Instance.SteamSystems.Remove(assemblyId);
            ModularApi.Log($"SteamManager removed assembly {assemblyId}");
        }

        public SteamSystem GetSteamSystem(int assemblyId)
        {
            SteamSystem steamSystem;
            if (Instance == null || !Instance.SteamSystems.TryGetValue(assemblyId, out steamSystem))
                return null;
            return steamSystem;
        }
    }
}
