using NavalPowerSystems.Communication;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace NavalPowerSystems.Drivetrain_V2
{
    public class DrivetrainSystem_V2
    {
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        public readonly int AssemblyId;
        private readonly IMyCubeGrid SystemGrid;
        public bool DirtyAssembly = true;
        private double DistanceToCamera;
        public const double ViewRange = 750;

        private List<IMyCubeBlock> AllBlocks = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Engines = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Motors = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Generators = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Turbines = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Gearboxes = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Propellers = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Driveshafts = new List<IMyCubeBlock>();
        private List<IDrivetrainPart> Producers = new List<IDrivetrainPart>();
        private List<IDrivetrainPart> Transformers = new List<IDrivetrainPart>();
        private List<IDrivetrainPart> Consumers = new List<IDrivetrainPart>();
        private List<LinkedPath> LinkedPaths = new List<LinkedPath>();
        private Dictionary<long, DriveshaftSection> DriveshaftSections = new Dictionary<long, DriveshaftSection>();

        public DrivetrainSystem_V2(int assemblyId)
        {
            AssemblyId = assemblyId;
            SystemGrid = ModularApi.GetAssemblyGrid(assemblyId);
        }

        public void AddPart(IMyCubeBlock block)
        {
            //Standard Housekeeping
            if (block == null) return;

            string subtype = block.BlockDefinition.SubtypeId;

            if (Config.EngineSubtypes.Contains(subtype))
            {
                Engines.Add(block);
                AllBlocks.Add(block);
                var logic = block.GameLogic?.GetAs<IDrivetrainPart>();
                if (logic != null)
                    Producers.Add(logic);
            }
            else if (Config.GearboxSubtypes.Contains(subtype))
            {
                Gearboxes.Add(block);
                AllBlocks.Add(block);
                var logic = block.GameLogic?.GetAs<IDrivetrainPart>();
                if (logic != null)
                    Transformers.Add(logic);
            }
            else if (Config.PropellerSubtypes.Contains(subtype))
            {
                Propellers.Add(block);
                AllBlocks.Add(block);
                var logic = block.GameLogic?.GetAs<IDrivetrainPart>();
                if (logic != null)
                    Consumers.Add(logic);
            }
            else if (Config.DriveshaftSubtypes.Contains(subtype))
            {
                Driveshafts.Add(block);
                AllBlocks.Add(block);
            }
            DirtyAssembly = true;
        }

        public void RemovePart(IMyCubeBlock block)
        {
            //Standard Housekeeping
            if (block == null) return;

            string subtype = block.BlockDefinition.SubtypeId;

            if (Config.EngineSubtypes.Contains(subtype))
            {
                Engines.Remove(block);
                AllBlocks.Remove(block);
                var logic = block.GameLogic?.GetAs<IDrivetrainPart>();
                if (logic != null)
                    Producers.Remove(logic);
            }
            else if (Config.GearboxSubtypes.Contains(subtype))
            {
                Gearboxes.Remove(block);
                AllBlocks.Remove(block);
                var logic = block.GameLogic?.GetAs<IDrivetrainPart>();
                if (logic != null)
                    Transformers.Remove(logic);
            }
            else if (Config.PropellerSubtypes.Contains(subtype))
            {
                Propellers.Remove(block);
                AllBlocks.Remove(block);
                var logic = block.GameLogic?.GetAs<IDrivetrainPart>();
                if (logic != null)
                    Consumers.Remove(logic);
            }
            else if (Config.DriveshaftSubtypes.Contains(subtype))
            {
                Driveshafts.Remove(block);
                AllBlocks.Remove(block);
            }
            DirtyAssembly = true;
        }

        public void UpdateTick()
        {
            //Go away if there's nothing to do
            if (LinkedPaths.Count <= 0)
                return;

            foreach (var prod in Producers) prod.Load_In = 0;
            var shafts = LinkedPaths.GroupBy(p => p.Consumer);

            //Get/set load
            foreach (var shaft in shafts)
            {
                var con = shaft.Key;
                int prodsForCon = shaft.Count();

                //Skip if no shaft input from this path
                foreach (var path in shaft)
                {
                    if (path.Producer.GetRatio() == 0)
                    {
                        prodsForCon--;
                        continue;
                    }
                }
                //Skip whole shaft if there's no one left to contribute
                if (prodsForCon <= 0) continue;

                double conRPM = con.RPM_Out;
                double conLoad = con.GetLoad();

                //Gather and distribute load, any producer past this point is contributing
                foreach (var path in shaft)
                {
                    //Get any shaft brake
                    double brakeLoad = 0;
                    foreach (var member in path.PathMembers)
                    {
                        if (member.GetRole() == DrivetrainRole.Transformer)
                            brakeLoad += member.GetLoad();
                    }

                    double gearedLoad = (conLoad + brakeLoad) / path.PathGearRatio;
                    double prodLoad = gearedLoad / prodsForCon;

                    path.Producer.RPM_In = (float)conRPM * path.PathGearRatio;
                    path.Producer.Load_In += prodLoad;
                }
            }

            //Get/set torque
            foreach (var shaft in shafts)
            {
                //Replicate 
                var con = shaft.Key;
                double prodTotalTorque = 0;
                float prodRPM = 0;

                foreach (var path in shaft)
                {
                    if (path.Producer.GetRatio() > 0)
                    {
                        prodTotalTorque += path.Producer.GetTorque() * path.PathGearRatio;
                        prodRPM = Math.Max(prodRPM, path.Producer.RPM_Out / path.PathGearRatio);
                    }
                }
                con.Torque_In = prodTotalTorque;
                con.RPM_In = prodRPM;
            }

            //Animate last
            if (DistanceToCamera < ViewRange)
            {
                var shaftSections = DriveshaftSections.GroupBy(p => p.Value.ControllerLogic);

                foreach (var shaft in DriveshaftSections)
                {
                    var controller = shaft.Value.ControllerLogic;
                    var shaftRPM = controller.RPM_In;
                    float deltaAngle = (float)shaftRPM * 360f / 3600f;

                    shaft.Value.UpdateRotation(deltaAngle);
                }
            }
        }

        public void UpdateTick10()
        {
            if (DirtyAssembly)
            {
                RebuildDrivetrain();
            }
        }

        public void UpdateTick100()
        {
            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                UpdateCameraDistance();
            }
            
        }

        private void UpdateCameraDistance()
        {
            if (MyAPIGateway.Utilities.IsDedicated)
                return;

            DistanceToCamera = Vector3D.Distance(SystemGrid.WorldMatrix.Translation, MyAPIGateway.Session.Camera.WorldMatrix.Translation);
        }

        private void RebuildDrivetrain()
        {
            LinkedPaths.Clear();
            DriveshaftSections.Clear();            

            foreach (var engineBlock in Engines)
            {
                var engineLogic = engineBlock.GameLogic?.GetAs<IDrivetrainPart>();
                if (engineLogic == null) continue;

                foreach (var propBlock in Propellers)
                {
                    var propLogic = propBlock.GameLogic?.GetAs<IDrivetrainPart>();
                    if (propLogic == null) continue;

                    var newPath = new LinkedPath(engineLogic, propLogic);
                    var visited = new HashSet<IMyCubeBlock>();
                    if (RunTrace(engineBlock, engineBlock, propBlock, 1.0f, newPath, ref visited, ref DriveshaftSections))
                        LinkedPaths.Add(newPath);
                }
            }

            foreach (var section in DriveshaftSections)
            {
                var logic = section.Value.ControllerLogic;
                if (logic == null) continue;
                if (logic.GetRole() == DrivetrainRole.Consumer)
                {
                    var subtype = section.Value.SectionController.BlockDefinition.SubtypeId;
                    if (subtype != null && Drivetrain_Config.PropellerSettings_V2[subtype].IsCCW)
                    {
                        section.Value.IsCCW = true;
                    }
                }
            }
        }

        private bool RunTrace(
            IMyCubeBlock startBlock, //Producer
            IMyCubeBlock currentBlock,
            IMyCubeBlock targetBlock, //Consumer
            float ratio,
            LinkedPath path,
            ref HashSet<IMyCubeBlock> visited,
            ref Dictionary<long, DriveshaftSection> sections)
        {
            if (!visited.Add(currentBlock)) return false;

            var logic = currentBlock.GameLogic?.GetAs<IDrivetrainPart>();
            var subtype = currentBlock.BlockDefinition.SubtypeName;
            if (logic != null)
            {
                path.PathMembers.Add(logic);
                if (Config.GearboxSubtypes.Contains(subtype))
                    ratio *= logic.GetRatio();
            }

            if (currentBlock == targetBlock)
            {   
                path.PathGearRatio = ratio;
                return true;
            }

            var connectedBlocks = ModularApi.GetConnectedBlocks(currentBlock, "Drivetrain_Definition_V2", false);
            foreach (var connected in connectedBlocks)
            {
                long sectionId = GetSectionId(currentBlock, connected);

                if (Config.DriveshaftSubtypes.Contains(subtype))
                {
                    if (!sections.ContainsKey(sectionId))
                    {
                        sections.Add(sectionId, new DriveshaftSection{ 
                            SectionController = currentBlock,
                            ControllerLogic = logic
                        });
                    }
                }

                if (RunTrace(startBlock, connected, targetBlock, ratio, path, ref visited, ref sections))
                    return true;
            }

            if (logic != null) path.PathMembers.Remove(logic);
            return false;
        }

        private long GetSectionId(IMyCubeBlock a, IMyCubeBlock b)
        {
            long idA = a.EntityId;
            long idB = b.EntityId;

            long id1 = Math.Min(idA, idB);
            long id2 = Math.Max(idA, idB);

            return (id1 << 32) | (uint)id2;
        }

        public class DriveshaftSection
        {
            public IMyCubeBlock SectionController;
            public IDrivetrainPart ControllerLogic;
            public float CurrentAngle;
            public bool IsCCW;

            public Dictionary<MyEntitySubpart, Matrix> ShaftSubparts = new Dictionary<MyEntitySubpart, Matrix>();
            public void UpdateRotation(float deltaAngle)
            {
                float direction = IsCCW ? -1f : 1f;
                CurrentAngle += deltaAngle * direction;

                if (CurrentAngle >= 360f) CurrentAngle -= 360f;
                if (CurrentAngle < 0f) CurrentAngle += 360f;

                foreach (var sub in ShaftSubparts)
                {
                    Matrix rotation = Matrix.CreateRotationZ(MathHelper.ToRadians(-deltaAngle));
                    Matrix final = rotation * sub.Value;
                    sub.Key.PositionComp.SetLocalMatrix(ref final);
                }
            }
        }

        public class LinkedPath
        {
            public readonly IDrivetrainPart Producer;
            public readonly IDrivetrainPart Consumer;

            public readonly List<IDrivetrainPart> PathMembers = new List<IDrivetrainPart>();
            public float PathGearRatio { get; internal set; } = 1.0f;
            public bool IsValid => Producer != null && Consumer != null;

            public LinkedPath(IDrivetrainPart producer, IDrivetrainPart consumer)
            {
                Producer = producer;
                Consumer = consumer;
            }

        }
    }
}
