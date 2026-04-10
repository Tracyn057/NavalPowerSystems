using NavalPowerSystems.Communication;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.ModAPI;
using static NavalPowerSystems.Config;

namespace NavalPowerSystems.Drivetrain_V2
{
    public class NewDrivetrainSystem
    {
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        private readonly int AssemblyId;
        private bool TraceComplete = false;
        private int BlockCount = 0;

        private List<IMyCubeBlock> Engines = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Gearboxes = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Propellers = new List<IMyCubeBlock>();
        private List<IDrivetrainNode> Nodes = new List<IDrivetrainNode>();
        private HashSet<EngineNode> EngineNodes = new HashSet<EngineNode>();
        private HashSet<GearboxNode> GearboxNodes = new HashSet<GearboxNode>();
        private HashSet<PropellerNode> PropellerNodes = new HashSet<PropellerNode>();
        private List<IMySlimBlock> CWAnimList = new List<IMySlimBlock>();
        private List<IMySlimBlock> CCWAnimList = new List<IMySlimBlock>();

        public NewDrivetrainSystem(int assemblyId)
        {
            AssemblyId = assemblyId;
        }

        public void AddPart(IMyCubeBlock block)
        {
            if (block == null) return;

            string subtype = block.BlockDefinition.SubtypeId;

            if (Config.EngineSubtypes.Contains(subtype))
            {
                Engines.Add(block);
            }
            else if (Config.GearboxSubtypes.Contains(subtype))
            {
                Gearboxes.Add(block);
            }
            else if (Config.PropellerSubtypes.Contains(subtype))
            {
                Propellers.Add(block);
            }
            
            TraceComplete = false;
        }

        public void RemovePart(IMyCubeBlock block)
        {
            if (block == null) return;

            string subtype = block.BlockDefinition.SubtypeId;

            if (Config.EngineSubtypes.Contains(subtype))
            {
                Engines.Remove(block);
            }
            else if (Config.GearboxSubtypes.Contains(subtype))
            {
                Gearboxes.Remove(block);
            }
            else if (Config.PropellerSubtypes.Contains(subtype))
            {
                Propellers.Remove(block);
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

            if (Nodes.Count < 2) return;

            //Backwards trace to calculate loads (Propeller -> Engine)
            for (int i = Nodes.Count - 1; i > 0; i--)
            {
                //No output for propellers
                Nodes[i-1].CalculateLoad(Nodes[i].InputLoad); 
            }

            //Forwards trace to calculate outputs (Engine -> Propeller)
            double runningTorque = 0;
            double runningRPM = 0;

            for (int i = 0; i < Nodes.Count; i++)
            {
                //No input load for engines
                Nodes[i].CalculateOutput(runningTorque, runningRPM);
                
                runningTorque = Nodes[i].OutputTorque;
                runningRPM = Nodes[i].OutputRPM;
            }
        }

        private void RebuildDrivetrain()
        {
            //Clean the slate
            Nodes.Clear();
            EngineNodes.Clear();
            GearboxNodes.Clear(); //Do I need this?
            PropellerNodes.Clear(); //Do I need this?
            CWAnimList.Clear();
            CCWAnimList.Clear();
            TraceComplete = false; // Just in case, should already be false

            foreach (var engine in Engines)
            {
                var currentLogicPath = new List<IDrivetrainNode>();
                var currentShaftPath = new List<IMySLimBlock>();
                TraceDirectional(engine, engine, new HashSet<IMyCubeBlock>(), currentLogicPath, currentShaftPath, 0);
            }
        }

        private void TraceDirectional( 
            IMyCubeBlock current, 
            IMyCubeBlock startEngine, 
            HashSet<IMyCubeBlock> pathVisited, 
            List<IDrivetrainNode> currentLogicPath, 
            List<IMySLimBlock> currentShaftPath,
            double runningInertia)
        {
            if (pathVisited.Contains(current))
                return;
            pathVisited.Add(current);

            string subtype = current.BlockDefinition.SubtypeId;
            if (Config.DriveshaftSubtypes.Contains(subtype))
            {
                var stats = Config.ShaftSettings_V2[subtype];
                runningInertia += stats.BlockLength * Drivetrain_Config.DriveshaftInertiaPerBlock;
                currentShaftPath.Add(current.slimBlock);
            }
            
            var node = CreateNodeFromBlock(current);
            if (node != null)
            {
                currentLogicPath.Add(node);
            }

            if (Config.PropellerSubtypes.Contains(subtype))
            {
                bool isCCW = Config.PropellerSettings_V2[subtype].IsCCW;
                runningInertia += Config.PropellerSettings_V2[subtype].Inertia;

                var animTarget = isCCW ? CCWAnimList : CWAnimList;
                animTarget.AddRange(currentShaftPath);
                animTarget.Add(current.slimBlock);

                foreach (var node in currentLogicPath)
                {
                    if (!Nodes.Contains(node))
                    {
                        Nodes.Add(node);
                    }
                    if (node is EngineNode engineNode && !EngineNodes.Contains(engineNode)) EngineNodes.Add(engineNode);
                    else if (node is GearboxNode gearboxNode && !GearboxNodes.Contains(gearboxNode)) GearboxNodes.Add(gearboxNode);
                    else if (node is PropellerNode propellerNode && !PropellerNodes.Contains(propellerNode)) PropellerNodes.Add(propellerNode);
                }

                var startNode = currentLogicPath.FirstOrDefault() as EngineNode;
                if (startNode != null) startNode.SystemInertia = runningInertia;

                pathVisited.Remove(current);
                return;
            }

            var neighbors = ModularApi.GetConnectedBlocks(current, "Drivetrain_Definition", false);

            foreach (var neighbor in neighbors)
            {
                if (IsValidNext(current, neighbor))
                {
                    TraceDirectional(neighbor, startEngine, pathVisited, new List<IDrivetrainNode>(currentLogicPath), new List<IMySLimBlock>(currentShaftPath), runningInertia);
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

        private IDrivetrainNode CreateNodeFromBlock(IMyCubeBlock block)
        {
            string subtype = block.BlockDefinition.SubtypeId;

            if (Config.EngineSubtypes.Contains(subtype))
            {
                var stats = Config.EngineSettings_V2[subtype];
                var logic = block.GameLogic?.GetAs<EngineLogic_V2>();
                var node = new EngineNode{
                    EngineBlock = block,
                    EngineLogic = logic,
                    PeakRPM = stats.PeakRPM,
                    PeakTorque = stats.PeakTorque,
                    HeatRate = stats.HeatRate,
                    PowerCurveConstant = stats.PowerCurveConstant,
                    EngineInertia = stats.EngineInertia
                };
                logic.SetNode(node);
                return node;
            }
                
            if (Config.GearboxSubtypes.Contains(subtype))
            {
                    var stats = Config.GearboxSettings_V2[subtype];
                    return new GearboxNode{
                        GearboxBlock = block,
                        GearRatio = stats.GearRatio
                    };
            }
            if (Config.PropellerSubtypes.Contains(subtype))
            {
                    var stats = Config.PropellerSettings_V2[subtype];
                    return new PropellerNode{
                        PropellerBlock = block,
                        PropellerGrid = block.CubeGrid,
                        Diameter = stats.Diameter,
                        Inertia = stats.Inertia,
                        TorqueCoefficient = stats.TorqueCoefficient,
                        ThrustCoefficient = stats.ThrustCoefficient,
                        IsCRP = stats.IsCRP,
                        PitchRatio = Drivetrain_Config.PitchRatio
                    };
            }

            return null;
        }
    }
}
