using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using Vector3 = VRageMath.Vector3;

namespace NavalPowerSystems.Drivetrain_2
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_FunctionalBlock), false,
        "NPSDrivetrainRudderSmallCenteredV1",
        "NPSDrivetrainRudderSmallOffsetLeftV1",
        "NPSDrivetrainRudderSmallOffsetRightV1",
        "NPSDrivetrainRudderSmallCenteredV2",
        "NPSDrivetrainRudderSmallOffsetLeftV2",
        "NPSDrivetrainRudderSmallOffsetRightV2"
    )]
    internal class RudderLogic_V2 : MyGameLogicComponent, MySync
    {
        private IMyCubeBlock RudderBlock;
        private IMyFunctionalBlock RudderFunctional;
        private MyEntitySubpart RudderSubpart;
        private RudderStats_V2 RudderStats;
        private Vector3D RudderPosition;
        private MatrixD RudderSubpartMatrix;
        private MyCubeGrid RudderMyGrid;
        private IMyCubeGrid RudderGrid;
        private IMyShipController RudderShipController;

        private float DistanceToCamera = 0f;
        private float RudderMaxAngle = 35f;
        private float RudderTargetAngle = 0f;
        private float RudderCurrentAngle = 0f;
        private float RudderThrust = 0f;
        private float GridMass = 0f;
        private Vector3 RotationInputVector = Vector3.Zero;
        private const double InnerZone = 0.2094395;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            RudderBlock = Entity as IMyCubeBlock;
            RudderFunctional = Entity as IMyFunctionalBlock;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (RudderBlock != null && RudderFunctional != null)
            {
                Entity.TryGetSubpart("Rudder", out RudderSubpart);
                if (RudderSubpart != null)
                {
                    RudderSubpartMatrix = RudderSubpart.PositionComp.LocalMatrixRef;
                }
                RudderStats = Drivetrain_Config.RudderSettings_V2[RudderBlock.BlockDefinition.SubtypeId];
                RudderGrid = RudderBlock.CubeGrid;
                RudderMyGrid = RudderBlock.CubeGrid as MyCubeGrid;
                if (RudderMyGrid == null)
                {
                    NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                    return;
                }

                var player = MyAPIGateway.Players.GetPlayerControllingEntity(RudderMyGrid);

                if (player?.Controller?.ControlledEntity != null)
                {
                    RudderShipController = player.Controller.ControlledEntity as IMyShipController;
                }
            }

            Terminal_ControlRotation.SetLocalValue(ControlRotation);
            Terminal_ControlRotation.ValueChanged += Terminal_ControlRotation_ValueChanged;

            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateAfterSimulation()
        {
            if (!RudderFunctional.IsWorking || RudderMyGrid.Physics == null || RudderMyGrid.Physics.IsStatic) return;

            RudderPosition = RudderBlock.PositionComp.WorldVolume.Center;
            RecalculateController();
            UpdateControllerInput();
            SoftRollGravityAlign();
            ApplyDragForce();
            ApplyRotationalForce();
        }

        public void RecalculateController()
        {
            if (RudderShipController == null || !RudderShipController.IsWorking || !RudderShipController.IsMainCockpit)
            {
                var player = MyAPIGateway.Players.GetPlayerControllingEntity(RudderMyGrid);
                IMyShipController RudderShipController = null;

                if (player?.Controller?.ControlledEntity != null)
                    RudderShipController = player.Controller.ControlledEntity as IMyShipController;

                if (RudderShipController != null)
                    var MatrixTransposeToCockpit = Matrix.Transpose(RudderShipController.LocalMatrix.GetOrientation());
            }
        }

        private void UpdateControllerInput()
        {
                var rotateInput = Vector2.ClampToSphere(RudderShipController.RotationIndicator.X, 1f);
                RotationInputVector = Vector3(rotateInput, 0f, 0f); // X yaw Y Pitch Z Roll
        }

        public override void UpdateAfterSimulation100()
        {
            UpdateDistanceToCamera();
            if (RudderShipController != null)
            {
                GridMass = RudderShipController.CalculateShipMass().TotalMass;
            }
        }

        public void UpdateDistanceToCamera()
        {
            if (MyAPIGateway.Utilities.IsDedicated)
                return;

            var dist = Vector3D.Distance(PropellerBlock.WorldMatrix.Translation, MyAPIGateway.Session.Camera.WorldMatrix.Translation);
            DistanceToCamera = (float)dist;
        }

        private void ApplyRotationalForce()
        {
            Vector3D shipRight = RudderShipController.WorldMatrix.Right;

            Vector3D forceToApply =
            RudderMyGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, forceToApply, RudderPosition, null);
        }

        private void ApplyDragForce()
        {
            Vector3D shipForward = RudderShipController.WorldMatrix.Forward;

            Vector3D forceToApply =
            RudderMyGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, forceToApply, RudderPosition, null);
        }

        private void SoftRollGravityAlign()
        {
                
        }

        private void RudderAnimation(float yawInput)
        {
            if (MyAPIGateway.Utilities.IsDedicated || RudderSubpart == null || DistanceToCamera >= 1000f)
                return;


            if (Math.Abs(RudderTargetAngle) > 0.01f)
            {
                RudderTargetAngle = MathHelper.Lerp(RudderTargetAngle, 0f, 0.01f);
            }

            RudderTargetAngle = MathHelper.Clamp(RudderTargetAngle, -RudderMaxAngle, RudderMaxAngle);

            RudderCurrentAngle = MathHelper.Lerp(RudderCurrentAngle, yawInput * RudderMaxAngle, 0.025f);

            Matrix rotationMatrix = Matrix.CreateRotationY(MathHelper.ToRadians(RudderCurrentAngle));

            Matrix finalMatrix = rotationMatrix * RudderSubpartMatrix;
            RudderSubpart.PositionComp.SetLocalMatrix(ref finalMatrix);
        }
    }
}
