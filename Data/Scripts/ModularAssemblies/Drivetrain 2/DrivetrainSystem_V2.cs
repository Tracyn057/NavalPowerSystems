using NavalPowerSystems.Communication;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;

namespace NavalPowerSystems.Drivetrain_V2
{
    public class NewDrivetrainSystem
    {
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        private readonly int AssemblyId;
        private bool TraceComplete = false;
        private int BlockCount = 0;

        private List<IMyCubeBlock> Engines = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Motors = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Generators = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Turbines = new List<IMyCubeBlock>();

        private List<IMyCubeBlock> Gearboxes = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Propellers = new List<IMyCubeBlock>();
        private List<IDrivetrainNode> Nodes = new List<IDrivetrainNode>();
        private List<EngineNode> EngineNodes = new List<EngineNode>();
        private List<TurbineNode> TurbineNodes = new List<TurbineNode>();
        private List<MotorNode> MotorNodes = new List<MotorNode>();
        private List<GeneratorNode> GeneratorNodes = new List<GeneratorNode>();
        private List<GearboxNode> GearboxNodes = new List<GearboxNode>();
        private List<PropellerNode> PropellerNodes = new List<PropellerNode>();

        public NewDrivetrainSystem(int assemblyId)
        {
            AssemblyId = assemblyId;
        }

        public void AddPart(IMyCubeBlock block)
        {
            //Standard Housekeeping
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
            //Standard Housekeeping
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
            if (!TraceComplete) RebuildDrivetrain();
            if (Nodes.Count < 2) return;

            //Calculate propeller load first. Internal CalculateLoad calls will propagate through the system to calculate load and output for all nodes.
            foreach (var prop in PropellerNodes)
            {
                prop.CalculateLoad(0, 0);
            }

            //Load should have reached engines at this point, so send torque and RPM back down the system to calculate output for all nodes.
            foreach (var engine in EngineNodes)
            {
                engine.CalculateOutput(0, 0);
            }
        }

        private void RebuildDrivetrain()
        {
            //Clear gearboxes from propellers
            foreach (var node in PropellerNodes)
                node.ConnectedGearboxNode = null;

            //Clear connections on gearboxes and reset passthrough
            foreach (var node in GearboxNodes)
            {
                node.ConnectedEngineNodes.Clear();
                node.GearboxNodesTowardsEngines.Clear();
                node.GearboxNodesTowardsPropellers.Clear();
                node.ConnectedPropellerNodes.Clear();
            }

            //Clear connected gearboxes from engines
            foreach (var node in EngineNodes)
                node.ConnectedGearboxNode = null;

            foreach (var engine in Engines)
            {
                var logic = engine.GameLogic?.GetAs<EngineLogic_V2>();
                if (logic != null) logic.ConnectedGearbox = null;
            }
            //Clear each logic component of its animation lists
            foreach (var gear in Gearboxes)
            {
                var logic = gear.GameLogic?.GetAs<GearboxLogic_V2>();
                if (logic != null)
                {
                    logic.Driveshafts.Clear();
                    logic.DriveshaftMatrices.Clear();
                }
            }
            foreach (var prop in Propellers)
            {
                var logic = prop.GameLogic?.GetAs<PropellerLogic_V2>();
                if (logic != null)
                {
                    logic.Driveshafts.Clear();
                    logic.DriveshaftMatrices.Clear();
                }
            }

            //Clear node lists
            Nodes.Clear();
            EngineNodes.Clear();
            GearboxNodes.Clear(); //Do I need this list in the first place?
            PropellerNodes.Clear();

            TraceComplete = false; // Just in case, should already be false

            foreach (var engine in Engines)
            {
                TraceDirectional(engine, engine, new HashSet<IMyCubeBlock>(), new List<IDrivetrainNode>(), new List<IMySlimBlock>(), 0, null);
            }
        }

        private void TraceDirectional( 
            IMyCubeBlock current, 
            IMyCubeBlock startEngine, 
            HashSet<IMyCubeBlock> pathVisited, 
            List<IDrivetrainNode> currentLogicPath, 
            List<IMySlimBlock> currentShaftPath,
            double runningInertia,
            IDrivetrainNode previousNode)
        {
            if (pathVisited.Contains(current))
                return;
            pathVisited.Add(current);

            string subtype = current.BlockDefinition.SubtypeId;
            if (Config.DriveshaftSubtypes.Contains(subtype))
            {
                //Add inertia for each shaft in the line
                var stats = Drivetrain_Config.ShaftSettings_V2[subtype];
                runningInertia += stats.BlockLength * Drivetrain_Config.DriveshaftInertiaPerBlock;
                currentShaftPath.Add(current.SlimBlock);
            }
            
            var node = CreateNodeFromBlock(current);
            if (node != null)
            {
                currentLogicPath.Add(node);

                if (previousNode != null)
                {
                    var previousNodeType = previousNode.GetType();
                    var currentNodeType = node.GetType();
                    if (previousNodeType == typeof(EngineNode) && currentNodeType == typeof(GearboxNode))
                    {
                        var previousN = previousNode as EngineNode;
                        var currentNode = node as GearboxNode;
                        previousN.ConnectedGearboxNode = currentNode;
                        currentNode.ConnectedEngineNodes.Add(previousN);

                        //Add to block logic for animations
                        if (currentShaftPath.Count > 0)
                        {
                            var logic = current.GameLogic?.GetAs<GearboxLogic_V2>();
                            logic.Driveshafts.AddRange(currentShaftPath);
                            currentShaftPath.Clear();
                        }
                    }
                    else if (previousNodeType == typeof(GearboxNode) && currentNodeType == typeof(GearboxNode))
                    {
                        var previousN = previousNode as GearboxNode;
                        var currentNode = node as GearboxNode;
                        var startLogic = startEngine.GameLogic?.GetAs<EngineLogic_V2>();
                        var startNode = startLogic?.EngineNode;

                        if (startNode.ConnectedGearboxNode != null)
                             previousN.GearboxNodesTowardsPropellers.Add(currentNode);
                        else
                        {
                            previousN.GearboxNodesTowardsPropellers.Add(currentNode);
                            currentNode.GearboxNodesTowardsEngines.Add(previousN);
                        }

                        //Add to block logic for animations
                        if (currentShaftPath.Count > 0)
                        {
                            var logic = current.GameLogic?.GetAs<GearboxLogic_V2>();
                            logic.Driveshafts.AddRange(currentShaftPath);
                            currentShaftPath.Clear();
                        }
                    }
                    else if (previousNodeType == typeof(GearboxNode) && currentNodeType == typeof(PropellerNode))
                    {
                        var previousN = previousNode as GearboxNode;
                        var currentNode = node as PropellerNode;
                        var logic = current.GameLogic?.GetAs<PropellerLogic_V2>();
                        currentNode.PropellerLogic = logic;
                        currentNode.ConnectedGearboxNode = previousN;
                        previousN.ConnectedPropellerNodes.Add(currentNode);

                        //Add to block logic for animations
                        if (currentShaftPath.Count > 0)
                        {
                            logic.Driveshafts.AddRange(currentShaftPath);
                            logic.ShaftListDirty = true;
                            currentShaftPath.Clear();
                        }
                    }
                }
            }

            //If a propeller is found, that is the end of this trace
            if (Config.PropellerSubtypes.Contains(subtype))
            {
                //Running tally of nodes in the network
                foreach (var pNode in currentLogicPath)
                {
                    if (!Nodes.Contains(pNode))
                    {
                        Nodes.Add(pNode);
                    }
                    if (pNode.GetType() == typeof(EngineNode) && !EngineNodes.Contains((EngineNode)pNode)) EngineNodes.Add((EngineNode)pNode);
                    else if (pNode.GetType() == typeof(GearboxNode) && !GearboxNodes.Contains((GearboxNode)pNode)) GearboxNodes.Add((GearboxNode)pNode);
                    else if (pNode.GetType() == typeof(PropellerNode) && !PropellerNodes.Contains((PropellerNode)pNode)) PropellerNodes.Add((PropellerNode)pNode);
                }

                //Determine if this propeller is CCW and if the gearbox preceeding it should be CCW as well.
                var stats = Drivetrain_Config.PropellerSettings_V2[subtype];
                if (stats != null && stats.IsCCW)
                {
                    var logic = current.GameLogic?.GetAs<PropellerLogic_V2>();
                    var propNode = node as PropellerNode;
                    if (logic != null)
                    {
                        logic.AnimCCW = true;
                    }
                    if (propNode != null && propNode.ConnectedGearboxNode != null)
                    {
                        var gearNode = propNode.ConnectedGearboxNode;
                        var gearlogic = propNode.ConnectedGearboxNode.GearboxLogic;

                        //If the previous gearbox is only connected to this propeller, make it CCW like the propeller.
                        //Gearbox Logic initializes AnimCCW as false.
                        if (gearNode != null && gearlogic != null && gearNode.ConnectedPropellerNodes.Count == 1)
                        {
                            gearlogic.AnimCCW = true;
                        }
                    }
                }

                //Add system inertia to the engine node
                var startNode = currentLogicPath.FirstOrDefault() as EngineNode;
                if (startNode != null) startNode.SystemInertia = runningInertia;

                pathVisited.Remove(current);
                return;
            }

            var neighbors = ModularApi.GetConnectedBlocks(current, "Drivetrain_Definition", false);

            //Recursion
            foreach (var neighbor in neighbors)
            {
                if (IsValidNext(current, neighbor))
                {
                    TraceDirectional(neighbor, startEngine, pathVisited, new List<IDrivetrainNode>(currentLogicPath), new List<IMySlimBlock>(currentShaftPath), runningInertia, node ?? previousNode);
                }
            }

            pathVisited.Remove(current);
            TraceComplete = true;
        }

        private bool IsValidNext(IMyCubeBlock from, IMyCubeBlock to)
        {
            //Define valid connections from and to each block type
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
                var stats = Drivetrain_Config.EngineSettings_V2[subtype];
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
                var stats = Drivetrain_Config.GearboxSettings_V2[subtype];
                var logic = block.GameLogic?.GetAs<GearboxLogic_V2>();
                var node = new GearboxNode
                {
                    GearboxBlock = block,
                    GearboxLogic = logic,
                    GearRatio = stats.GearRatio
                };
                logic.SetNode(node);
                return node;
            }
            if (Config.PropellerSubtypes.Contains(subtype))
            {
                var stats = Drivetrain_Config.PropellerSettings_V2[subtype];
                var logic = block.GameLogic?.GetAs<PropellerLogic_V2>();
                var node = new PropellerNode{
                    PropellerBlock = block,
                    PropellerGrid = block.CubeGrid,
                    Diameter = stats.Diameter,
                    Inertia = stats.Inertia,
                    TorqueCoefficient = stats.TorqueCoefficient,
                    ThrustCoefficient = stats.ThrustCoefficient,
                    IsCRP = stats.IsCRP,
                    PitchRatio = Drivetrain_Config.PitchRatio
                };
                logic.SetNode(node);
                return node;
            }

            return null;
        }
    }
}
