using NavalPowerSystems.Communication;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace NavalPowerSystems.Drivetrain_V2
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_CubeBlock), false,
            "NPS_Driveshaft",
            "NPS_Driveshaft_Marked",
            "NPS_Driveshaft_Long",
            "NPS_Driveshaft_Long_Marked",
            "NPS_Driveshaft_LinearGearbox1",
            "NPS_Driveshaft_LinearGearbox2",
            "NPS_Driveshaft_TubeSealEnclosed1",
            "NPS_Driveshaft_TubeSealSlope1",
            "NPS_Driveshaft_TubeSealSlope1Corner",
            "NPS_Driveshaft_TubeSealSlope2",
            "NPS_Driveshaft_TubeSealSlope2Corner",
            "NPS_Driveshaft_EndTubeV1"
    )]
    public class DriveshaftLogic_V2 : MyGameLogicComponent, IDrivetrainNode
    {
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        private MyCubeBlock ShaftBlock;
        private MyEntitySubpart ShaftSubpart;
        private Matrix ShaftSubpartInitialMatrix;
        private long ShaftId;
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

        public override void Init(MyComponentDefinitionBase definition)
        {
            base.Init(definition);
            ShaftBlock = (MyCubeBlock)Entity;
            ShaftId = Entity.EntityId;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (ShaftBlock == null) return;

            Entity.TryGetSubpart("Driveshaft", out ShaftSubpart);
            if (ShaftSubpart != null)
                ShaftSubpartInitialMatrix = ShaftSubpart.PositionComp.WorldMatrixRef;

            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
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

            
            if (ConnectedParts.Count > 0)
            {
                //Gather load information
                var loadPacket = new DrivetrainPacket
                {
                    SenderId = ShaftId,
                    TickSent = MyAPIGateway.Session.GameplayFrameCounter,
                    DownstreamLoad = InputLoad,
                    UpstreamTorque = double.NaN,
                    UpstreamRPM = double.NaN
                };

                //Then gather output information
                var outputPacket = new DrivetrainPacket
                {
                    SenderId = ShaftId,
                    TickSent = MyAPIGateway.Session.GameplayFrameCounter,
                    DownstreamLoad = double.NaN,
                    UpstreamTorque = OutputTorque,
                    UpstreamRPM = OutputRPM
                };

                foreach (var part in ConnectedParts)
                {
                    var logic = part.GameLogic?.GetAs<IDrivetrainNode>();
                    if (logic == null) continue;

                    //Send load packet first
                    if (part.EntityId == OutgoingId)
                        logic.ReceivePacket(loadPacket);

                    //Output packet after that
                    if (part.EntityId == IncomingId)
                        logic.ReceivePacket(outputPacket);
                }
            }

            //Animate
            if (ShaftSubpart != null && OutputRPM != 0 && ShouldAnimate)
            {
                float degreesPerTick = (float)OutputRPM * 360f / 3600f; // convert RPM → degrees/tick at 60 Hz
                CurrentAngle += degreesPerTick;
                CurrentAngle %= 360f;

                Matrix rotationMatrix = Matrix.CreateRotationZ(MathHelper.ToRadians(-CurrentAngle));
                Matrix finalMatrix = rotationMatrix * ShaftSubpartInitialMatrix;
                ShaftSubpart.PositionComp.SetLocalMatrix(ref finalMatrix);
            }
        }

        public void CleanAssembly()
        {
            ConnectedParts.Clear();
            foreach (IMyCubeBlock neighbor in ModularApi.GetConnectedBlocks(ShaftBlock, "Drivetrain_Definition_V2", false))
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
    }
}
