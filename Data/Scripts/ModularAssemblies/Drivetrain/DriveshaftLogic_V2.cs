using NavalPowerSystems.Communication;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using static Sandbox.Game.Components.MyRenderComponentThrust;

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
        public IDrivetrainNode ShaftNode => this as IDrivetrainNode;
        private MyEntitySubpart ShaftSubpart;
        private Matrix ShaftSubpartInitialMatrix;
        private long ShaftId;
        private float CurrentAngle;
        public bool ShouldAnimate = false;
        private Dictionary<IMyCubeBlock, IDrivetrainNode> ConnectedParts = new Dictionary<IMyCubeBlock, IDrivetrainNode>();
        private IDrivetrainNode LoadRequestNode; //Load coming from downstream -- Send output information back
        private List<DrivetrainPacket> PacketInbox = new List<DrivetrainPacket>();
        public void ReceivePacket(DrivetrainPacket packet) => PacketInbox.Add(packet);
        public long GetNodeID() => ShaftId;
        public string GetNodeSubtype() => ShaftIBlock.BlockDefinition.SubtypeId;
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
            else
                ModularApi.Log($"{ShaftIBlock.BlockDefinition.SubtypeId} subpart is null.");

            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateBeforeSimulation()
        {

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
                var logic = neighbor.GameLogic?.GetAs<MyGameLogicComponent>() as IDrivetrainNode;
                if (logic != null)
                {
                    ConnectedParts[neighbor] = logic;
                }
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
