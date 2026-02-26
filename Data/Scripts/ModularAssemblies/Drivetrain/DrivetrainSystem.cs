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

namespace NavalPowerSystems.Drivetrain
{
    internal class DrivetrainSystem
    {
        //Overhead Variables
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        public readonly int AssemblyId;
        public readonly IMyCubeGrid Grid;
        public int BlockCount;
        private bool TraceComplete = false;
        private bool IsLeader = false;
        public List<IMyTerminalBlock> Gearboxes = new List<IMyTerminalBlock>();
        public List<IMyGasTank> Inputs = new List<IMyGasTank>();
        public List<IMyTerminalBlock> Outputs = new List<IMyTerminalBlock>();
        public List<IMySlimBlock> Driveshafts = new List<IMySlimBlock>();
        private List<DrivetrainCircuit> DrivetrainMap = new List<DrivetrainCircuit>();
        private static readonly Dictionary<long, int> GridDragLeaders = new Dictionary<long, int>();
        //Engine System Variables
        public float TotalInputMW = 0f;
        public float _highThrottle = 0f;

        //Gearbox Variables

        //Propeller Variables

        public DrivetrainSystem(int id)
        {
            AssemblyId = id;
            Grid = ModularApi.GetAssemblyGrid(id);
            ModularApi.Log($"DrivetrainSystem, assembly {AssemblyId} registered.");
        }

        public void AddPart(IMyCubeBlock block)
        {
            if (block == null) return;

            string subtype = block.BlockDefinition.SubtypeId;
            ModularApi.Log($"Adding part {subtype} to {AssemblyId}");
            BlockCount++;

            if (Config.GearboxSubtypes.Contains(subtype))
            {
                if (block == null)
                {
                    ModularApi.Log($"{AssemblyId} gearbox part attempted to add null.");
                    return;
                }
                Gearboxes.Add(block as IMyTerminalBlock);
                ModularApi.Log($"{AssemblyId} now contains {Gearboxes.Count} Gearboxes.");
                ModularApi.Log($"{AssemblyId} now contains {BlockCount} parts.");
            }
            else if (Config.PropellerSubtypes.Contains(subtype))
            {
                if (block == null)
                {
                    ModularApi.Log($"{AssemblyId} propeller part attempted to add null.");
                    return;
                }
                Outputs.Add(block as IMyTerminalBlock);
                if (Outputs.Count == 1)
                {
                    var logic = block.GameLogic?.GetAs<PropellerLogic>();
                    if (logic != null)
                    {
                        logic._isPrime = true;
                    }
                }
                ModularApi.Log($"{AssemblyId} now contains {Outputs.Count} Power Consumers.");
                ModularApi.Log($"{AssemblyId} now contains {BlockCount} parts.");
            }
            else if (block is IMyGasTank)
            {
                if (block == null)
                {
                    ModularApi.Log($"{AssemblyId} engine part attempted to add null.");
                    return;
                }
                Inputs.Add(block as IMyGasTank);
                ModularApi.Log($"{AssemblyId} now contains {Inputs.Count} Power Producers.");
                ModularApi.Log($"{AssemblyId} now contains {BlockCount} parts.");
            }
            else if (Config.DriveshaftSubtypes.Contains(subtype))
            {
                if (block == null)
                {
                    ModularApi.Log($"{AssemblyId} driveshaft part attempted to add null.");
                    return;
                }
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
                Outputs.Remove(block as IMyTerminalBlock);
                ModularApi.Log($"{AssemblyId} now contains {Outputs.Count} Power Consumers.");
                ModularApi.Log($"{AssemblyId} now contains {BlockCount} parts.");
            }
            else if (Config.EngineSubtypes.Contains(subtype))
            {
                Inputs.Remove(block as IMyGasTank);
                ModularApi.Log($"{AssemblyId} now contains {Inputs.Count} Power Producers.");
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
                UpdateGridLeader();
                TraceComplete = true;
            }

            if (IsLeader)
            {
                ApplyDrag();
            }
            else
            {
                UpdateGridLeader();
            }
        }

        public void UpdateTick10()
        {
            UpdateClutches();
            UpdateInput();
            UpdateOutput();
        }

        public void Unload()
        {
            ModularApi.Log($"DrivetrainSystem, assembly {AssemblyId} unload called.");
        }

        private void RebuildDrivetrain()
        {
            DrivetrainMap.Clear();
            foreach (var engine in Inputs)
            {
                TraceDirectional(
                    engine,
                    engine,
                    new HashSet<IMyCubeBlock>(),
                    0,
                    false);
            }
        }

        private void TraceDirectional(
            IMyCubeBlock current,
            IMyCubeBlock startEngine,
            HashSet<IMyCubeBlock> pathVisited,
            int reduction,
            bool hasClutch)
        {
            if (pathVisited.Contains(current))
                return;

            pathVisited.Add(current);

            string subtype = current.BlockDefinition.SubtypeId;

            if (Config.GearboxSubtypes.Contains(subtype))
            {
                var stats = Config.GearboxSettings[subtype];
                reduction += stats.ReductionLevel;
                hasClutch |= stats.IsClutched;
            }

            if (Config.PropellerSubtypes.Contains(subtype))
            {
                DrivetrainMap.Add(
                    new DrivetrainCircuit(
                        startEngine,
                        current,
                        reduction,
                        hasClutch));

                pathVisited.Remove(current);
                return;
            }

            var neighbors = ModularApi.GetConnectedBlocks(
                current,
                "Drivetrain_Definition",
                false);

            foreach (var neighbor in neighbors)
            {
                if (IsValidNext(current, neighbor))
                {
                    TraceDirectional(
                        neighbor,
                        startEngine,
                        pathVisited,
                        reduction,
                        hasClutch);
                }
            }

            pathVisited.Remove(current);
        }

        private bool IsValidNext(IMyCubeBlock from, IMyCubeBlock to)
        {
            string fromType = from.BlockDefinition.SubtypeId;
            string toType = to.BlockDefinition.SubtypeId;

            bool fromEngine = Config.EngineSubtypes.Contains(fromType);
            bool fromGearbox = Config.GearboxSubtypes.Contains(fromType);
            bool fromShaft = Config.DriveshaftSubtypes.Contains(fromType);

            bool toGearbox = Config.GearboxSubtypes.Contains(toType);
            bool toShaft = Config.DriveshaftSubtypes.Contains(toType);
            bool toProp = Config.PropellerSubtypes.Contains(toType);

            if (fromEngine)
                return toShaft || toGearbox;

            if (fromGearbox)
                return toShaft || toGearbox || toProp;

            if (fromShaft)
                return toShaft || toGearbox || toProp;

            return false;
        }

        public void UpdateClutches()
        {
            if (Inputs.Count == 0) return;

            foreach (var engine in Inputs)
            {
                var logic = engine.GameLogic?.GetAs<NavalEngineLogicBase>();
                if (logic == null) continue;

                if (logic._currentThrottle > _highThrottle)
                {
                    _highThrottle = logic._currentThrottle;
                }
            }

            foreach (var engine in Inputs)
            {
                var logic = engine.GameLogic?.GetAs<NavalEngineLogicBase>();
                if (logic == null) continue;

                if (logic._currentThrottle < 0.01)
                {
                    logic._isEngaged = false;
                    continue;
                }

                if (!logic._isEngaged)
                {
                    if (logic._currentThrottle >= (_highThrottle - 0.04f))
                    {
                        logic._isEngaged = true;
                    }
                }
                else
                {
                    if (logic._currentThrottle < (_highThrottle - 0.06f))
                    {
                        logic._isEngaged = false;
                    }
                }
            }
        }

        public void UpdateInput()
        {
            if (Inputs.Count == 0)
                return; 
            TotalInputMW = 0f;

            foreach (var engine in Inputs)
            {
                var logic = engine.GameLogic?.GetAs<NavalEngineLogicBase>();
                if (logic == null)
                {
                    ModularApi.Log($"{AssemblyId} engine logic is null.");
                    continue;
                }
                bool contributes = false;
                foreach (var circuit in DrivetrainMap)
                {
                    if (circuit.EngineLogic == logic && circuit.IsPathValid)
                    {
                        contributes = true;
                        break;
                    }
                }
                if (contributes)
                    TotalInputMW += logic._currentOutputMW;
            }
        }

        public void UpdateOutput()
        {
            Outputs.RemoveAll(x => x == null || x.Closed || x.MarkedForClose);
            if (Outputs.Count == 0)
                return;
            float perPropMW = TotalInputMW / Outputs.Count;

            foreach (var prop in Outputs)
            {
                var logic = prop.GameLogic?.GetAs<PropellerLogic>();
                if (logic == null) continue;
                logic._inputMW = perPropMW;
            }
        }

        private void UpdateGridLeader()
        {
            if (Grid == null || Grid.Physics == null) return;

            long gridId = Grid.EntityId;

            //Claim leadership if the slot is empty
            if (!GridDragLeaders.ContainsKey(gridId))
            {
                GridDragLeaders[gridId] = this.AssemblyId;
            }

            //Check if we are the designated leader
            if (GridDragLeaders[gridId] == this.AssemblyId)
            {
                IsLeader = true;
            }
            else
            {
                // Check if the current leader is still valid. 
                var leaderGrid = ModularApi.GetAssemblyGrid(GridDragLeaders[gridId]);

                if (leaderGrid == null || leaderGrid.Closed)
                {
                    // The old leader is gone
                    GridDragLeaders[gridId] = this.AssemblyId;
                    IsLeader = true;
                }
                else
                {
                    IsLeader = false;
                }
            }
        }

        public void DrivetrainDebug10()
        {
            ModularApi.Log($"{AssemblyId} Output count is {Outputs.Count}.");
            ModularApi.Log($"{AssemblyId} total input is {TotalInputMW}MW.");
        }

        private void ApplyDrag()
        {   
            var grid = Grid as MyCubeGrid;
            if (grid.IsPreview || grid.Physics == null || !grid.Physics.Enabled || grid.Physics.IsStatic)
                return;

            Vector3D velocity = grid.Physics.LinearVelocity;
            double speed = velocity.Length();
            if (speed < 0.1) return;

            Vector3D dragDirection = -Vector3D.Normalize(velocity);

            float dragQuadCoeff = 0.00045f;
            float dragLinearCoeff = 0.0003f;
            float gridMass = (float)grid.Physics.Mass * 1.15f;

            //Quadratic drag formula
            double quadDrag = 0.5 * dragQuadCoeff * speed * speed * gridMass;
            //Add a linear drag component
            double linearDrag = speed * dragLinearCoeff * gridMass;
            //Total drag force
            double forceMagnitude = quadDrag + linearDrag;
            //Cap the drag force to prevent excessive deceleration
            double maxDrag = speed * grid.Physics.Mass / (1f / 60f);
            //If the calculated drag exceeds the max, limit it
            Vector3D finalForce = dragDirection * forceMagnitude;
            if (forceMagnitude > maxDrag) finalForce = dragDirection * maxDrag;

            double totalForceNewtons = finalForce.Length();

            grid.Physics.AddForce(
                MyPhysicsForceType.APPLY_WORLD_FORCE,
                finalForce,
                grid.Physics.CenterOfMassWorld,
                null
                );
        }

        struct TraceStep
        {
            public IMyCubeBlock Block;
            public int CurrentReductionLevel;
            public bool HasClutch;

            public TraceStep(IMyCubeBlock block, int level, bool hasClutch)
            {
                Block = block;
                CurrentReductionLevel = level;
                HasClutch = hasClutch;
            }
        }

        public struct CachedShaft
        {
            public IMySlimBlock Shaft;
            public long ShaftId;
            public MyEntitySubpart Subpart;
            public Matrix InitialMatrix;
        }
    }

    public class DrivetrainCircuit
    {
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        public NavalEngineLogicBase EngineLogic;
        public PropellerLogic PropLogic;
        public int PathReduction;
        public bool IsPathValid;
        public bool HasClutch;

        public DrivetrainCircuit(IMyCubeBlock engine, IMyCubeBlock prop, int reduction, bool hasClutch)
        {
            EngineLogic = engine.GameLogic.GetAs<NavalEngineLogicBase>();
            PropLogic = prop.GameLogic.GetAs<PropellerLogic>();
            PathReduction = reduction;
            HasClutch = hasClutch;

            string subtype = engine.BlockDefinition.SubtypeId;
            var stats = Config.EngineSettings[subtype];
            var type = stats.Type;

            if (type == EngineType.Diesel && reduction == 1)
            {
                IsPathValid = true;
                ModularApi.Log($"{subtype} found valid path.");
            }
            else if (type == EngineType.GasTurbine && reduction == 2)
            {
                IsPathValid = true;
                ModularApi.Log($"{subtype} found valid path.");
            }
            else
            {
                ModularApi.Log($"{subtype} no valid path.");
                IsPathValid = false;
            }
                
        }
    }
}
