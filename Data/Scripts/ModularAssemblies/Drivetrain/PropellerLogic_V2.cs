
using NavalPowerSystems.Communication;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Network;
using VRage.ModAPI;
using VRage.Network;
using VRage.ObjectBuilders;
using VRage.Sync;
using VRageMath;

namespace NavalPowerSystems.Drivetrain_V2
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_FunctionalBlock), false,
    "NPS_Propeller_4m3b",
    "NPS_Propeller_4m4b",
    "NPS_Propeller_4m5b"
    )]
    public class PropellerLogic_V2 : MyGameLogicComponent, IMyEventProxy, IDrivetrainNode
    {
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        public IMyCubeBlock PropellerBlock;
        private IMyFunctionalBlock PropellerFunctional;
        private IMyTerminalBlock PropellerTerminal;
        private MyEntitySubpart PropellerSubpart;
        private IMyCubeGrid PropellerGrid;
        private Matrix PropellerSubpartInitialMatrix;
        public PropellerStats_V2 PropellerStats;
        private bool ControlsInitialized = false;
        private bool ActionsInitialized = false;
        MySync<float, SyncDirection.BothWays> Terminal_PitchRatio;
        public float PitchRatio = 0f;

        private float CurrentAngle;
        public bool ShouldAnimate = false;
        private HashSet<IMyCubeBlock> ConnectedParts = new HashSet<IMyCubeBlock>();
        private long IncomingId = 0; //Load coming from downstream -- Send output information back
        private long OutgoingId = 0; //Output coming from upstream -- Send load information back
        private List<DrivetrainPacket> PacketInbox = new List<DrivetrainPacket>();
        public void ReceivePacket(DrivetrainPacket packet) => PacketInbox.Add(packet);
        public double GetLoadWeight() => 1;
        private double InputLoad = 0;
        private double OutputRPM = 0;
        private double OutputTorque = 0;
        public double OutputThrust = 0;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            PropellerFunctional = (IMyFunctionalBlock)Entity;
            PropellerBlock = (MyCubeBlock)Entity;
            PropellerTerminal = (IMyTerminalBlock)Entity;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            PropellerGrid = PropellerBlock.CubeGrid;
            PropellerStats = Drivetrain_Config.PropellerSettings_V2[PropellerBlock.BlockDefinition.SubtypeId];
            Entity.TryGetSubpart("Propeller", out PropellerSubpart);
            if (PropellerSubpart != null)
            {
                PropellerSubpartInitialMatrix = PropellerSubpart.PositionComp.LocalMatrixRef;
            }

            if (PropellerStats.IsCRP)
            {
                Terminal_PitchRatio.SetLocalValue(PitchRatio);
                Terminal_PitchRatio.ValueChanged += Terminal_PitchRatio_ValueChanged;
            }
            else
                PitchRatio = 1.1f;
            

            NeedsUpdate = 
                MyEntityUpdateEnum.EACH_FRAME
                | MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateAfterSimulation()
        {
            //Clean slate before information gathering
            InputLoad = 0;
            OutputRPM = 0;
            OutputTorque = 0;
            IncomingId = 0;
            OutgoingId = 0;

            //Grab information from inbox
            if (PacketInbox.Count > 0)
            {
                int tickNow = MyAPIGateway.Session.GameplayFrameCounter;

                for (int i = 0; i < PacketInbox.Count; i++)
                {
                    var packet = PacketInbox[i];
                    if (packet.TickSent != tickNow) continue;
                    {
                        if (IsOutputPacket(packet))
                        {
                            OutputRPM = packet.UpstreamRPM;
                            OutputTorque = packet.UpstreamTorque;
                            OutgoingId = packet.SenderId;
                        }
                        else if (IsLoadPacket(packet))
                        {
                            InputLoad = packet.DownstreamLoad;
                            IncomingId = packet.SenderId;
                        }
                    }
                }
                PacketInbox.Clear();
            }

            //Gather load information
            double velocity = PropellerBlock.CubeGrid.Physics?.LinearVelocity.Length() ?? 0;
            double RPS = OutputRPM / 60;
            double advanceRatio = (RPS > 0.1) ? velocity / (RPS * PropellerStats.Diameter) : 0;
            double currentTorque = PropellerStats.TorqueCoefficient * PitchRatio * MathHelper.Clamp(1 - (advanceRatio / PitchRatio), 0.1, 1);
            double torqueDemand = currentTorque * PitchRatio * Math.Pow(RPS, 2) * Math.Pow(PropellerStats.Diameter, 5);
            InputLoad = torqueDemand;

            //Create load packet and send
            if (ConnectedParts.Count > 0)
            {
                var loadPacket = new DrivetrainPacket
                {
                    SenderId = PropellerBlock.EntityId,
                    TickSent = MyAPIGateway.Session.GameplayFrameCounter,
                    DownstreamLoad = InputLoad,
                    UpstreamTorque = double.NaN,
                    UpstreamRPM = double.NaN
                };

                foreach (var part in ConnectedParts)
                {
                    var logic = part.GameLogic?.GetAs<IDrivetrainNode>();
                    if (logic == null) continue;

                    //Send load packet -- Propellers don't have output packets
                    if (part.EntityId == OutgoingId)
                        logic.ReceivePacket(loadPacket);
                }
            }

            //Calculate output force
            OutputThrust = currentTorque * 1024 * Math.Pow(RPS, 2) * Math.Pow(PropellerStats.Diameter, 4);
            if (Math.Abs(OutputThrust) > 100)
            {
                Vector3D thrustVector = PropellerBlock.WorldMatrix.Backward * OutputThrust;
                var thrustPos = PropellerBlock.PositionComp.WorldVolume.Center;
                PropellerGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, thrustVector, thrustPos, null);
            }

            //Animate
            if (PropellerSubpart != null && OutputRPM != 0 && ShouldAnimate)
            {
                float degreesPerTick = (float)OutputRPM * 360f / 3600f; // convert RPM → degrees/tick at 60 Hz
                CurrentAngle += degreesPerTick;
                CurrentAngle %= 360f;

                Matrix rotationMatrix = Matrix.CreateRotationZ(MathHelper.ToRadians(-CurrentAngle));
                Matrix finalMatrix = rotationMatrix * PropellerSubpartInitialMatrix;
                PropellerSubpart.PositionComp.SetLocalMatrix(ref finalMatrix);
            }
        }
        public void CleanAssembly()
        {
            ConnectedParts.Clear();
            foreach (IMyCubeBlock neighbor in ModularApi.GetConnectedBlocks(PropellerBlock, "Drivetrain_Definition_V2", false))
            {
                ConnectedParts.Add(neighbor);
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

        private void Terminal_PitchRatio_ValueChanged(MySync<float, SyncDirection.BothWays> obj)
        {
            PitchRatio = obj.Value;
            //UpdateControls();
        }

        private static void UpdateControls()
        {
            List<IMyTerminalControl> controls;

            MyAPIGateway.TerminalControls.GetControls<IMyFunctionalBlock>(out controls);

            foreach (IMyTerminalControl control in controls)
            {
                switch (control.Id)
                {
                    case "NPS_Propeller_TerminalControl_PitchRatio":
                        {
                            control.UpdateVisual();
                            break;
                        }
                }
            }
        }
    }
}
