using NavalPowerSystems.Communication;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.Components;
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
        private bool TraceComplete = false;
        private bool IsLeader = false;
        public List<IMyFunctionalBlock> Gearboxes = new List<IMyFunctionalBlock>();
        public List<IMyGasTank> Inputs = new List<IMyGasTank>();
        public List<IMyFunctionalBlock> Outputs = new List<IMyFunctionalBlock>();
        public List<IMySlimBlock> Driveshafts = new List<IMySlimBlock>();
        private List<DrivetrainCircuit> DrivetrainMap = new List<DrivetrainCircuit>();
        private static readonly Dictionary<long, int> GridDragLeaders = new Dictionary<long, int>();
        //Engine System Variables
        public float TotalInputMW = 0f;

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
            if (block == null || AssemblyId == -1) return;

            string subtype = block.BlockDefinition.SubtypeId;
            ModularApi.Log($"Adding part {subtype} to {AssemblyId}");
            var part = block as IMyFunctionalBlock;

            if (Config.GearboxSubtypes.Contains(subtype))
            {
                Gearboxes.Add(part);
                ModularApi.Log($"{AssemblyId} now contains {Gearboxes.Count} Gearboxes.");
            }
            else if (Config.PropellerSubtypes.Contains(subtype))
            {
                Outputs.Add(part);
                ModularApi.Log($"{AssemblyId} now contains {Outputs.Count} Power Consumers.");
            }
            else if (block is IMyGasTank)
            {
                Inputs.Add(block as IMyGasTank);
                ModularApi.Log($"{AssemblyId} now contains {Inputs.Count} Power Producers.");
            }
            else if (Config.DriveshaftSubtypes.Contains(subtype))
            {
                Driveshafts.Add(block.SlimBlock);
                ModularApi.Log($"{AssemblyId} now contains {Driveshafts.Count} Driveshafts.");
            }

            TraceComplete = false;
        }

        public void RemovePart(IMyCubeBlock block)
        {
            if (block == null || AssemblyId == -1) return;
            string subtype = block.BlockDefinition.SubtypeId;
            ModularApi.Log($"Removing part {subtype} from {AssemblyId}");
            var part = block as IMyFunctionalBlock;

            if (Config.GearboxSubtypes.Contains(subtype))
            {
                Gearboxes.Remove(part);
                ModularApi.Log($"{AssemblyId} now contains {Driveshafts.Count} Driveshafts.");
            }
            else if (Config.PropellerSubtypes.Contains(subtype))
            {
                Outputs.Remove(part);
                ModularApi.Log($"{AssemblyId} now contains {Driveshafts.Count} Driveshafts.");
            }
            else if (Config.EngineSubtypes.Contains(subtype))
            {
                Inputs.Remove(block as IMyGasTank);
                ModularApi.Log($"{AssemblyId} now contains {Driveshafts.Count} Driveshafts.");
            }
            else if (Config.DriveshaftSubtypes.Contains(subtype))
            {
                Driveshafts.Remove(block.SlimBlock);
                ModularApi.Log($"{AssemblyId} now contains {Driveshafts.Count} Driveshafts.");
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
                TraceDrivetrain(engine);
            }
        }

        private void TraceDrivetrain(IMyCubeBlock startEngine)
        {
            Queue<TraceStep> checkQueue = new Queue<TraceStep>();
            HashSet<IMyCubeBlock> visited = new HashSet<IMyCubeBlock>();

            checkQueue.Enqueue(new TraceStep(startEngine, 0, false));

            while (checkQueue.Count > 0)
            {
                TraceStep currentStep = checkQueue.Dequeue();
                IMyCubeBlock currentBlock = currentStep.Block;
                int activeReduction = currentStep.CurrentReductionLevel;
                bool hasClutch = currentStep.HasClutch;

                if (visited.Contains(currentBlock)) continue;
                visited.Add(currentBlock);

                string subtype = currentBlock.BlockDefinition.SubtypeId;

                if (Config.GearboxSubtypes.Contains(subtype))
                {
                    var stats = Config.GearboxSettings[subtype];
                    activeReduction += stats.ReductionLevel;
                    hasClutch = stats.IsClutched;
                }

                if (Config.PropellerSubtypes.Contains(subtype))
                {
                    DrivetrainMap.Add(new DrivetrainCircuit(startEngine, currentBlock, activeReduction, hasClutch));
                    continue;
                }

                var neighbors = ModularApi.GetConnectedBlocks(currentBlock, "Drivetrain_Definition", true);
                foreach (var neighbor in neighbors)
                {
                    if (!visited.Contains(neighbor))
                    {
                        checkQueue.Enqueue(new TraceStep(neighbor, activeReduction, hasClutch));
                    }
                }
            }
            ModularApi.Log($"{DrivetrainMap.Count} drivetrain systems found.");
        }

        public void UpdateClutches()
        {
            if (Inputs.Count == 0) return;

            float highThrottle = 0f;

            foreach (var engine in Inputs)
            {
                var logic = engine.GameLogic?.GetAs<NavalEngineLogicBase>();
                if (logic == null) continue;

                if (logic._currentThrottle > highThrottle)
                {
                    highThrottle = logic._currentThrottle;
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
                    if (logic._currentThrottle >= (highThrottle - 0.04f))
                    {
                        logic._isEngaged = true;
                    }
                }
                else
                {
                    if (logic._currentThrottle < (highThrottle - 0.06f))
                    {
                        logic._isEngaged = false;
                    }
                }
            }
        }

        public void UpdateInput()
        {
            if (Inputs.Count == 0) return;
            TotalInputMW = 0f;

            foreach (var engine in Inputs)
            {
                var logic = engine.GameLogic?.GetAs<NavalEngineLogicBase>();
                if (logic == null) continue;
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
            MyAPIGateway.Utilities.ShowNotification($"Assembly {AssemblyId} update output for {Outputs.Count} propellers.");
            if (Outputs.Count == 0) return;
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
    }

    public class DrivetrainCircuit
    {
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
            }
            else if (type == EngineType.GasTurbine && reduction == 2)
            {
                IsPathValid = true;
            }
            else
                IsPathValid = false;
        }
    }
}
