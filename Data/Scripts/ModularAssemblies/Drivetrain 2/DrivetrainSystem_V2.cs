using NavalPowerSystems.Communication;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.ModAPI;
using static NavalPowerSystems.Config;

namespace NavalPowerSystems.Drivetrain
{
    public class NewDrivetrainSystem
    {
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        private readonly int AssemblyId;
        private bool TraceComplete = false;
        private int BlockCount = 0;
        public List<IMyTerminalBlock> Gearboxes = new List<IMyTerminalBlock>();
        public List<IMyGasTank> Engines = new List<IMyGasTank>();
        public List<IMyTerminalBlock> Motors = new List<IMyTerminalBlock>();
        public List<IMyTerminalBlock> Propellers = new List<IMyTerminalBlock>();
        public List<IMySlimBlock> Driveshafts = new List<IMySlimBlock>();
        private List<NewDrivetrainCircuit> DrivetrainMap = new List<NewDrivetrainCircuit>();

        private double InputRPM = 0;
        private double OutputRPM = 0;
        private double TorqueLoad = 0;
        private float GearRatio = 1;
        private double TotalInputTorque = 0;
        private double TotalOutputTorque = 0;

        public NewDrivetrainSystem(int assemblyId)
        {
            AssemblyId = assemblyId;
        }

        public void AddPart(IMyCubeBlock block)
        {
            if (block == null) return;

            string subtype = block.BlockDefinition.SubtypeId;
            ModularApi.Log($"Adding part {subtype} to {AssemblyId}");
            BlockCount++;

            if (Config.GearboxSubtypes.Contains(subtype))
            {
                Gearboxes.Add(block as IMyTerminalBlock);
                ModularApi.Log($"{AssemblyId} now contains {Gearboxes.Count} Gearboxes.");
                ModularApi.Log($"{AssemblyId} now contains {BlockCount} parts.");
            }
            else if (Config.PropellerSubtypes.Contains(subtype))
            {
                Propellers.Add(block as IMyTerminalBlock);
                ModularApi.Log($"{AssemblyId} now contains {Propellers.Count} Propellers.");
                ModularApi.Log($"{AssemblyId} now contains {BlockCount} parts.");
            }
            else if (Config.EngineSubtypes.Contains(subtype))
            {
                Engines.Add(block as IMyGasTank);
                ModularApi.Log($"{AssemblyId} now contains {Engines.Count} Engines.");
                ModularApi.Log($"{AssemblyId} now contains {BlockCount} parts.");
            }
            else if (Config.DriveshaftSubtypes.Contains(subtype))
            {
                Driveshafts.Add(block.SlimBlock);
                ModularApi.Log($"{AssemblyId} now contains {Driveshafts.Count} Driveshafts.");
                ModularApi.Log($"{AssemblyId} now contains {BlockCount} parts.");
            }
            
            TraceComplete = false;
        }

        public void RemovePart(IMyCubeBlock block)
        {
            if (block == null) return;

            string subtype = block.BlockDefinition.SubtypeId;
            ModularApi.Log($"Removing part {subtype} from {AssemblyId}");
            BlockCount--;

            if (Config.GearboxSubtypes.Contains(subtype))
            {
                Gearboxes.Remove(block as IMyTerminalBlock);
                ModularApi.Log($"{AssemblyId} now contains {Gearboxes.Count} Gearboxes.");
                ModularApi.Log($"{AssemblyId} now contains {BlockCount} parts.");
            }
            else if (Config.PropellerSubtypes.Contains(subtype))
            {
                Propellers.Remove(block as IMyTerminalBlock);
                ModularApi.Log($"{AssemblyId} now contains {Propellers.Count} Power Consumers.");
                ModularApi.Log($"{AssemblyId} now contains {BlockCount} parts.");
            }
            else if (Config.EngineSubtypes.Contains(subtype))
            {
                Engines.Remove(block as IMyGasTank);
                ModularApi.Log($"{AssemblyId} now contains {Engines.Count} Power Producers.");
                ModularApi.Log($"{AssemblyId} now contains {BlockCount} parts.");
            }
            else if (Config.DriveshaftSubtypes.Contains(subtype))
            {
                Driveshafts.Remove(block.SlimBlock);
                ModularApi.Log($"{AssemblyId} now contains {Driveshafts.Count} Driveshafts.");
                ModularApi.Log($"{AssemblyId} now contains {BlockCount} parts.");
            }
            
            TraceComplete = false;
        }

        public void UpdateTick()
        {
            if (!TraceComplete)
            {
                RebuildDrivetrain();
                TraceComplete = true;
            }


        }

        private void RebuildDrivetrain()
        {
            DrivetrainMap.Clear();
            foreach (var engine in Engines)
            {
                TraceDirectional(engine, engine, new HashSet<IMyCubeBlock>());
            }
        }

        private void TraceDirectional( IMyCubeBlock current, IMyCubeBlock startEngine, HashSet<IMyCubeBlock> pathVisited)
        {
            if (pathVisited.Contains(current))
                return;

            pathVisited.Add(current);

            string subtype = current.BlockDefinition.SubtypeId;
            
            if (Config.PropellerSubtypes.Contains(subtype))
            {
                DrivetrainMap.Add(
                    new NewDrivetrainCircuit(startEngine, current));

                pathVisited.Remove(current);
                return;
            }

            var neighbors = ModularApi.GetConnectedBlocks(current, "Drivetrain_Definition", false);

            foreach (var neighbor in neighbors)
            {
                if (IsValidNext(current, neighbor))
                {
                    TraceDirectional(neighbor, startEngine, pathVisited);
                }
            }

            pathVisited.Remove(current);
        }

        private bool IsValidNext(IMyCubeBlock from, IMyCubeBlock to)
        {
            string fromType = from.BlockDefinition.SubtypeId;
            string toType = to.BlockDefinition.SubtypeId;

            bool fromEngine = Config.EngineSubtypes.Contains(fromType);
            bool fromMotor = Config.MotorSubtypes.Contains(fromType);
            bool fromGearbox = Config.GearboxSubtypes.Contains(fromType);
            bool fromShaft = Config.DriveshaftSubtypes.Contains(fromType);

            bool toMotor = Config.MotorSubtypes.Contains(toType);
            bool toGearbox = Config.GearboxSubtypes.Contains(toType);
            bool toShaft = Config.DriveshaftSubtypes.Contains(toType);
            bool toProp = Config.PropellerSubtypes.Contains(toType);

            if (fromEngine)
                return toShaft || toGearbox;

            if (fromMotor)
                return toShaft || toGearbox || toProp;

            if (fromGearbox)
                return toShaft || toProp || toMotor;

            if (fromShaft)
                return toShaft || toGearbox || toProp || toMotor;

            return false;
        }
    }

    public class NewDrivetrainCircuit
    {
        public bool IsPathValid = false;
        public double GearRatio = 1;

        public NewDrivetrainCircuit(IMyCubeBlock engine, IMyCubeBlock propeller)
        {
            var engineLogic = engine.GameLogic?.GetAs<NewEngineLogic>();
            if (engineLogic != null)
            {
                engineLogic.IsValid = true;
            }
            //var motorLogic = engine.GameLogic?.GetAs<MotorLogic>();
            //if (motorLogic != null)
            //{
            //    motorLogic.IsValid = true;
            //}
            IsPathValid = true;
        }
    }
}
