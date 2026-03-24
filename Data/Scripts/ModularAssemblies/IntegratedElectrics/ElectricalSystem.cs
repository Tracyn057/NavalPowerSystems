using NavalPowerSystems.Communication;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using static NavalPowerSystems.Config;

namespace NavalPowerSystems.IntegratedElectrics
{
    public class ElectricalSystem
    {
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        private long GridId;
        public readonly IMyCubeGrid Grid;
        public float DistributedPowerMW { get; private set; } = 0f;
        public List<IMyTerminalBlock> Switchboards = new List<IMyTerminalBlock>();
        public List<IMyTerminalBlock> Transformers = new List<IMyTerminalBlock>();
        public List<IMyTerminalBlock> DriveSystems = new List<IMyTerminalBlock>();
        public List<IMyTerminalBlock> Motors = new List<IMyTerminalBlock>();

        public ElectricalSystem(long id)
        {
            GridId = id;
        }

        public void AddPart(IMyCubeBlock block)
        {
            if (block == null) return;

            string subtype = block.BlockDefinition.SubtypeId;
            var terminal = block as IMyTerminalBlock;

            if (Config.TransformerSubtypes.Contains(subtype))
            {
                Transformers.Add(terminal);
            }
            else if (Config.SwitchboardSubtypes.Contains(subtype))
            {
                Switchboards.Add(terminal);
            }
            else if (Config.VFDSubtypes.Contains(subtype))
            {
                DriveSystems.Add(terminal);
            }
            else if (Config.MotorSubtypes.Contains(subtype))
            {
                Motors.Add(terminal);
            }
        }

        public void RemovePart(IMyCubeBlock block)
        {
            if (block == null) return;

            string subtype = block.BlockDefinition.SubtypeId;
            var terminal = block as IMyTerminalBlock;

            if (Config.TransformerSubtypes.Contains(subtype))
            {
                Transformers.Remove(terminal);
            }
            else if (Config.SwitchboardSubtypes.Contains(subtype))
            {
                Switchboards.Remove(terminal);
            }
            else if (Config.VFDSubtypes.Contains(subtype))
            {
                DriveSystems.Remove(terminal);
            }
            else if (Config.MotorSubtypes.Contains(subtype))
            {
                Motors.Remove(terminal);
            }
        }

        public void UpdateTick()
        {
            DistributePower();
        }

        public void UpdateTick100()
        {
            AggregatePower();
        }

        public void Unload()
        {
            Transformers.Clear();
            Switchboards.Clear();
            DriveSystems.Clear();
        }

        private void AggregatePower()
        {
            int functionalSets = Math.Min(Switchboards.Count, Math.Min(Transformers.Count, DriveSystems.Count));
            float totalGridCeiling = functionalSets * 8.0f;

            if (Motors.Count > 0 && totalGridCeiling > 0f)
            {
                DistributedPowerMW = totalGridCeiling / Motors.Count;
            }
            else
            {
                DistributedPowerMW = 0f;
            }
        }

        private void DistributePower()
        {
            //Update switchboards with current demand
        }

        public string GetSystemStatus()
        {
            if (Transformers.Count == 0) return "Missing Transformers";
            if (DriveSystems.Count == 0) return "Missing VFDs";
            if (Switchboards.Count == 0) return "Missing Switchboards";
            return "System Nominal";
        }
    }
}
