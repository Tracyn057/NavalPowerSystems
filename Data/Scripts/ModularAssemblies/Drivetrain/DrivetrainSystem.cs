using NavalPowerSystems.Communication;
using NavalPowerSystems.Drivetrain.Consumers;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Audio;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace NavalPowerSystems.Drivetrain
{
    public class DrivetrainSystem
    {
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        public readonly int AssemblyId;
        private bool AssemblyDirty = true;
        private double CurrentRPM;
        private double TotalLoad;

        private List<IMyCubeBlock> AllBlocks = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Engines = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Motors = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Generators = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Turbines = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Gearboxes = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Propellers = new List<IMyCubeBlock>();
        private List<IMyCubeBlock> Driveshafts = new List<IMyCubeBlock>();

        public List<IDrivetrainPart> Producers = new List<IDrivetrainPart>();
        private List<IDrivetrainPart> Transformers = new List<IDrivetrainPart>();
        private List<IDrivetrainPart> Consumers = new List<IDrivetrainPart>();

        public DrivetrainSystem(int assemblyId)
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
                {
                    var grid = block.CubeGrid;
                    if (grid != null)
                    {
                        var manager = DrivetrainManager.Instance.GetGridManager(grid);
                        if (manager != null)
                            manager.RegisterProp(logic);
                    }
                    Consumers.Add(logic);
                }

            }
            else if (Config.DriveshaftSubtypes.Contains(subtype))
            {
                Driveshafts.Add(block);
                AllBlocks.Add(block);
            }

            AssemblyDirty = true;
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

            AssemblyDirty = true;
        }

        public void UpdateTick()
        {
            if (Producers.Count == 0 || Consumers.Count == 0) return;

            //Grab load info
            TotalLoad = 0;
            foreach (var c in Consumers)
            {
                TotalLoad += c.GetLoad();
            }
            foreach (var t in Transformers)
            {
                TotalLoad += t.GetLoad();
            }

            //Send to producers
            foreach (var p in Producers)
            {
                p.Load_In = TotalLoad;
                CurrentRPM = Math.Max(CurrentRPM, p.GetRPM());
                p.RPM_In = CurrentRPM;
            }
        }

        public void UpdateTick100()
        {
            if (AssemblyDirty)
            {
                foreach (var engine in Engines)
                {
                    var logic = engine.GameLogic?.GetAs<IDrivetrainPart>();
                    var connected = ModularApi.GetConnectedBlocks(engine, "Drivetrain_Definition", false);

                    if (logic != null && connected.Count() > 0)
                    {
                        logic.IsGenSet = true;
                    }
                }
            }

            AssemblyDirty = false;
        }
    }
}
