using NavalPowerSystems.Communication;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace NavalPowerSystems.IntegratedElectrics
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    internal class ElectricalManager : MySessionComponentBase
    {
        private int Ticks;
        public static ElectricalManager Instance { get; private set; } = null;
        private Dictionary<int, ElectricalSystem> ElectricalSystems = new Dictionary<int, ElectricalSystem>();

        public override void LoadData()
        {
            Instance = this;
        }

        public override void UnloadData()
        {
            Instance = null;
        }

        public override void UpdateAfterSimulation()
        {
            foreach (var system in ElectricalSystems.Values)
            {
                system.UpdateTick();
            }

            if (Ticks % 10 == 0)
            {
                foreach (var system in ElectricalSystems.Values)
                {
                    system.UpdateTick10();
                }
            }

            if (Ticks % 100 == 0)
            {
                Update100();
            }
            Ticks++;
        }

        public static void OnBlockAdded(IMyCubeBlock block)
        {
            if (instance == null || block?.CubeGrid == null) return;

            long gridId = block.CubeGrid.EntityId;

            if (!Instance.ElectricalSystems.ContainsKey(gridId))
            {
                Instance.ElectricalSystems.Add(gridId, new ElectricalSystem(gridId));
            }

            Instance.ElectricalSystems[gridId].AddBlock(block);
        }

        public static void OnBlockRemoved(IMyCubeBlock block)
        {
            Instance.ElectricalSystems[gridId].RemoveBlock(block);
        }

        public static void OnBlockDestroyed(IMyCubeBlock block)
        {
            Instance.ElectricalSystems[gridId].RemoveBlock(block);
        }
    }
}
