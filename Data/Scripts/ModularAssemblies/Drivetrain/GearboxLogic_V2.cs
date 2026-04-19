using NavalPowerSystems.Communication;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Network;
using VRage.ModAPI;
using VRage.Network;
using VRage.ObjectBuilders;
using VRage.Sync;
using VRage.Utils;

namespace NavalPowerSystems.Drivetrain_V2
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_FunctionalBlock), false,
            "NPS_Gearbox_MRG",
            "NPS_Gearbox_DoublePlanetary"
    )]
    public class GearboxLogic_V2 : MyGameLogicComponent, IMyEventProxy, IDrivetrainNode
    {
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        private IMyCubeBlock GearboxBlock;
        private IMyFunctionalBlock GearboxFunctional;
        private IMyTerminalBlock GearboxTerminal;
        private GearboxStats_V2 GearboxStats;
        private float BrakeEngagement = 0f;

        private HashSet<IMyCubeBlock> ConnectedParts = new HashSet<IMyCubeBlock>();
        private Dictionary<long, IDrivetrainNode> NodeLookup = new Dictionary<long, IDrivetrainNode>();
        private HashSet<long> IncomingIds = new HashSet<long>(); //Load coming from downstream -- Send output information back
        private HashSet<long> OutgoingIds = new HashSet<long>(); //Output coming from upstream -- Send load information back
        private List<DrivetrainPacket> PacketInbox = new List<DrivetrainPacket>();
        public void ReceivePacket(DrivetrainPacket packet) => PacketInbox.Add(packet);
        public double GetLoadWeight() => 1;
        private double InputLoad = 0;
        private double InputRPM = 0;
        private double OutputRPM = 0;
        private double OutputTorque = 0;
        static GearboxLogic_V2 GetLogic(IMyTerminalBlock gearbox) => gearbox?.GameLogic?.GetAs<GearboxLogic_V2>();

        private bool ControlsInitialized = false;
        private bool ActionsInitialized = false;
        MySync<float, SyncDirection.BothWays> Terminal_ShaftBrake;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            GearboxFunctional = (IMyFunctionalBlock)Entity;
            GearboxBlock = (MyCubeBlock)Entity;
            GearboxTerminal = (IMyTerminalBlock)Entity;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            GearboxStats = Drivetrain_Config.GearboxSettings_V2[GearboxBlock.BlockDefinition.SubtypeId];

            if (!ControlsInitialized)
                CreateControls();
            if (!ActionsInitialized)
                CreateActions();

            Terminal_ShaftBrake.SetLocalValue(BrakeEngagement);
            Terminal_ShaftBrake.ValueChanged += Terminal_ShaftBrake_ValueChanged;

            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateBeforeSimulation()
        {
            //Clean slate before information gathering
            InputLoad = 0;
            OutputRPM = 0;
            OutputTorque = 0;
            IncomingIds.Clear();
            OutgoingIds.Clear();

            //Gather load information
            ModularApi.Log($"{PacketInbox.Count} packets recieved by {GearboxBlock.BlockDefinition.SubtypeId}");
            //int tickNow = MyAPIGateway.Session.GameplayFrameCounter;
            double totalLoad = 0;
            double wightedRPM = 0;
            double totalTorque = 0;

            for (int i = 0; i < PacketInbox.Count; i++)
            {
                var packet = PacketInbox[i];
                //if (packet.TickSent != tickNow) continue;

                if (IsLoadPacket(packet))
                {
                    totalLoad += packet.DownstreamLoad;
                    IncomingIds.Add(packet.SenderId);
                    continue;
                }
                else if (IsOutputPacket(packet))
                {
                    totalTorque += packet.UpstreamTorque;
                    wightedRPM += packet.UpstreamRPM * packet.UpstreamTorque;
                    OutgoingIds.Add(packet.SenderId);
                    continue;
                }
            }

            PacketInbox.Clear();
            InputLoad = totalLoad;
            OutputRPM = totalTorque > 0 ? wightedRPM / totalTorque : 0;
            OutputTorque = totalTorque;

            //Calculate total load
            var gearedLoad = InputLoad / GearboxStats.GearRatio;
            var gearedRPMIn = OutputRPM * GearboxStats.GearRatio; //To send to inputs for any clutch logic
            var gearedRPMOut = OutputRPM / GearboxStats.GearRatio; //To send downstream

            if (BrakeEngagement > 0f)
            {
                var brakeTorque = GearboxStats.MaxBrakeTorque * BrakeEngagement;
                gearedLoad += brakeTorque / GearboxStats.GearRatio;
            }

            InputLoad = gearedLoad;

            //Calculate and send per-input load
            double totalWeight = 0;
            foreach (var id in OutgoingIds)
            {
                IDrivetrainNode node;
                if (!NodeLookup.TryGetValue(id, out node))
                {
                    var clutch = node.GetLoadWeight();
                    totalWeight += double.IsNaN(clutch) ? 1.0 : clutch;
                }
            }

            foreach (var id in OutgoingIds)
            {
                IDrivetrainNode node;
                if (!NodeLookup.TryGetValue(id, out node))
                    continue;

                double clutch = node.GetLoadWeight();
                double weight = double.IsNaN(clutch) ? 1.0 : clutch;
                double individualLoad = totalWeight > 0 ? (weight / totalWeight) * InputLoad : 0;

                node.ReceivePacket(new DrivetrainPacket
                {
                    SenderId = GearboxBlock.EntityId,
                    TickSent = MyAPIGateway.Session.GameplayFrameCounter,
                    DownstreamLoad = individualLoad,
                    UpstreamRPM = double.NaN,
                    UpstreamTorque = double.NaN
                });
            }

            var accumulatedTorque = OutputTorque * GearboxStats.GearRatio;
            var distributedTorque = accumulatedTorque / Math.Max(1, OutgoingIds.Count);
            foreach (var id in IncomingIds)
            {
                IDrivetrainNode node;
                if (!NodeLookup.TryGetValue(id, out node))
                    continue;

                node.ReceivePacket(new DrivetrainPacket
                {
                    SenderId = GearboxBlock.EntityId,
                    TickSent = MyAPIGateway.Session.GameplayFrameCounter,
                    DownstreamLoad = double.NaN,
                    UpstreamTorque = distributedTorque,
                    UpstreamRPM = gearedRPMOut
                });
            }
        }

        private void Terminal_ShaftBrake_ValueChanged(MySync<float, SyncDirection.BothWays> obj)
        {
            BrakeEngagement = obj.Value;
            UpdateControls();
        }

        public void CleanAssembly()
        {
            ConnectedParts.Clear();
            NodeLookup.Clear();
            foreach (IMyCubeBlock neighbor in ModularApi.GetConnectedBlocks(GearboxBlock, "Drivetrain_Definition_V2", false))
            {
                ConnectedParts.Add(neighbor);

                var logic = neighbor.GameLogic?.GetAs<IDrivetrainNode>();
                if (logic != null)
                    NodeLookup[neighbor.EntityId] = logic;
            }
        }

        private static bool IsLoadPacket(DrivetrainPacket p)
        {
            return double.IsNaN(p.UpstreamRPM) && double.IsNaN(p.UpstreamTorque);
        }

        private static bool IsOutputPacket(DrivetrainPacket p)
        {
            return double.IsNaN(p.DownstreamLoad);
        }

        private void CreateControls()
        {
            if (ControlsInitialized) return;

            ControlsInitialized = true;

            {
                var Control_ShaftBrake = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyFunctionalBlock>("NPS_Gearbox_TerminalControl_ShaftBrake");
                Control_ShaftBrake.Title = MyStringId.GetOrCompute("Shaft Brake Override");
                Control_ShaftBrake.Visible = Control_ShaftBrake_Visible;
                Control_ShaftBrake.SetLimits(0f, 1f);
                Control_ShaftBrake.SupportsMultipleBlocks = true;
                Control_ShaftBrake.Getter = Control_Terminal_ShaftBrake_Getter;
                Control_ShaftBrake.Setter = Control_Terminal_ShaftBrake_Setter;
                Control_ShaftBrake.Writer = Control_Terminal_ShaftBrake_Writer;
                MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(Control_ShaftBrake);
            }

            //Shaft brake
            //Reverse select
        }

        private void CreateActions()
        {
            if (ActionsInitialized) return;

            ActionsInitialized = true;

            {

            }

            //Shaft brake
            //Reverse select
        }

        public static void UpdateControls()
        {
            List<IMyTerminalControl> controls;

            MyAPIGateway.TerminalControls.GetControls<IMyFunctionalBlock>(out controls);

            foreach (IMyTerminalControl control in controls)
            {
                switch (control.Id)
                {
                    case "NPS_Gearbox_TerminalControl_ShaftBrake":
                        {
                            control.UpdateVisual();
                            break;
                        }
                }
            }
        }

        static bool Control_ShaftBrake_Visible(IMyTerminalBlock gearbox)
        {
            var logic = GetLogic(gearbox);
            return (logic == null ? false : logic.GearboxStats.MaxBrakeTorque > 0);
        }

        static float Control_Terminal_ShaftBrake_Getter(IMyTerminalBlock engine)
        {
            var logic = GetLogic(engine);
            return logic == null ? 0.01f : logic.Terminal_ShaftBrake;
        }

        static void Control_Terminal_ShaftBrake_Setter(IMyTerminalBlock engine, float value)
        {
            var logic = GetLogic(engine);
            if (logic != null)
                logic.Terminal_ShaftBrake.ValidateAndSet(value);
        }

        static void Control_Terminal_ShaftBrake_Writer(IMyTerminalBlock engine, StringBuilder writer)
        {
            var logic = GetLogic(engine);
            if (logic != null)
                writer.Append((int)(logic.Terminal_ShaftBrake * 100f)).Append('%');
        }
    }
}
