using NavalPowerSystems.Communication;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
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
        private IMyCubeBlock ShaftIBlock;
        public IDrivetrainNode ShaftNode;
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

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            ShaftIBlock = (IMyCubeBlock)Entity;
            ShaftId = Entity.EntityId;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (ShaftIBlock == null) return;

            Entity.TryGetSubpart("Driveshaft", out ShaftSubpart);
            if (ShaftSubpart != null)
                ShaftSubpartInitialMatrix = ShaftSubpart.PositionComp.WorldMatrixRef;

            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateBeforeSimulation()
        {
            foreach (var packet in PacketInbox)
            {
                foreach (var neighbor in ConnectedParts)
                {
                    var logic = neighbor.GameLogic?.GetAs<IDrivetrainNode>();
                    if (logic == null || neighbor.EntityId == packet.SenderId) continue;

                    logic.ReceivePacket(packet);
                    OutputRPM = double.IsNaN(packet.UpstreamRPM) ? 0 : packet.UpstreamRPM;
                }
            }
            PacketInbox.Clear();

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
            foreach (IMyCubeBlock neighbor in ModularApi.GetConnectedBlocks(ShaftIBlock, "Drivetrain_Definition_V2", false))
            {
                ConnectedParts.Add(neighbor);
            }
        }
    }
}
