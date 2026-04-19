using NavalPowerSystems.Communication;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRageMath;

namespace NavalPowerSystems.Drivetrain_V2
{
    public class DrivetrainSystem_V2
    {
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        public readonly int AssemblyId;
        private readonly IMyCubeGrid SystemGrid;
        public bool ShouldAnimate = false;
        public bool DirtyAssembly = true;
        public const double ViewRange = 600;
        public const double ViewPadding = 200;

        private List<IMyCubeBlock> AllBlocks = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Engines = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Motors = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Generators = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Turbines = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Gearboxes = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Propellers = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Driveshafts = new List<IMyCubeBlock>();

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
            }
            else if (Config.GearboxSubtypes.Contains(subtype))
            {
                Gearboxes.Add(block);
                AllBlocks.Add(block);
            }
            else if (Config.PropellerSubtypes.Contains(subtype))
            {
                Propellers.Add(block);
                AllBlocks.Add(block);
            }
            else if (Config.DriveshaftSubtypes.Contains(subtype))
            {
                var logic = new DriveshaftLogic_V2();
                block.GameLogic = logic;
                logic.Init(block.GetObjectBuilder());
                Driveshafts.Add(block);
                AllBlocks.Add(block);
            }

            //ModularApi.Log($"Adding {subtype} to assembly {AssemblyId}. Assembly now contains {AllBlocks.Count} parts.");
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
            }
            else if (Config.GearboxSubtypes.Contains(subtype))
            {
                Gearboxes.Remove(block);
                AllBlocks.Remove(block);
            }
            else if (Config.PropellerSubtypes.Contains(subtype))
            {
                Propellers.Remove(block);
                AllBlocks.Remove(block);
            }
            else if (Config.DriveshaftSubtypes.Contains(subtype))
            {
                Driveshafts.Remove(block);
                AllBlocks.Remove(block);
            }

            //ModularApi.Log($"Removing {subtype} from assembly {AssemblyId}. Assembly now contains {AllBlocks.Count} parts.");
            DirtyAssembly = true;
        }

        public void UpdateTick10()
        {
            if (DirtyAssembly)
            {
                foreach (var block in AllBlocks)
                {
                    var node = block.GameLogic?.GetAs<IDrivetrainNode>();
                    if (node == null) continue;
                    node.CleanAssembly();
                }
                DirtyAssembly = false;
            }
        }

        public void UpdateTick100()
        {
            UpdateCameraDistance();

            foreach (var prop in Propellers)
            {
                var logic = prop.GameLogic?.GetAs<PropellerLogic_V2>();
                if (logic != null)
                    logic.ShouldAnimate = ShouldAnimate;
            }
            foreach (var shaft in Driveshafts)
            {
                var logic = shaft.GameLogic?.GetAs<DriveshaftLogic_V2>();
                if (logic != null)
                    logic.ShouldAnimate = ShouldAnimate;
            }
        }

        private void UpdateCameraDistance()
        {
            if (MyAPIGateway.Utilities.IsDedicated)
                return;

            var dist = Vector3D.Distance(SystemGrid.WorldMatrix.Translation, MyAPIGateway.Session.Camera.WorldMatrix.Translation);

            ShouldAnimate = dist < 750;
        }
    }
}
