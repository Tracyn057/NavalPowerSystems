using EmptyKeys.UserInterface.Controls;
using NavalPowerSystems.Communication;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using Vector3 = VRageMath.Vector3;

namespace NavalPowerSystems.Drivetrain_V2
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_FunctionalBlock), false,
        "NPSDrivetrainRudderSmallCenteredV1",
        "NPSDrivetrainRudderSmallOffsetLeftV1",
        "NPSDrivetrainRudderSmallOffsetRightV1",
        "NPSDrivetrainRudderSmallCenteredV2",
        "NPSDrivetrainRudderSmallOffsetLeftV2",
        "NPSDrivetrainRudderSmallOffsetRightV2"
    )]
    internal class RudderLogic_V2 : MyGameLogicComponent
    {
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        private IMyCubeBlock RudderBlock;
        private IMyFunctionalBlock RudderFunctional;
        private MyEntitySubpart RudderSubpart;
        private RudderStats_V2 RudderStats;
        private Vector3D RudderPosition;
        private MatrixD RudderSubpartMatrix;
        private MyCubeGrid RudderMyGrid;
        private IMyCubeGrid RudderGrid;
        private IMyShipController RudderShipController;
        private PropellerLogic_V2 NearestPropellerLogic;

        private float DistanceToCamera = 0f;
        private float RudderMaxAngle = 35f;
        private float RudderTargetAngle = 0f;
        private float RudderCurrentAngle = 0f;
        private float GridMass = 0f;
        private float YawInput = 0f;

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

            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        

        public override void UpdateAfterSimulation()
        {
            if (!RudderFunctional.IsWorking || RudderMyGrid.Physics == null || RudderMyGrid.Physics.IsStatic) return;

            RecalculateController();
            RudderPosition = RudderBlock.PositionComp.WorldVolume.Center;
            if (RudderShipController != null)
                YawInput = MathHelper.Clamp(RudderShipController.RotationIndicator.X, -1, 1);
            RudderTargetAngle = RudderMaxAngle * YawInput;
            RudderAnimation();
            ApplyRotationalForce();
            SoftRollGravityAlign();
        }

        public override void UpdateAfterSimulation100()
        {
            UpdateDistanceToCamera();
            if (RudderShipController != null)
            {
                GridMass = RudderShipController.CalculateShipMass().TotalMass;
            }
        }

        public void RecalculateController()
        {
            if (RudderShipController == null || !RudderShipController.IsWorking || !RudderShipController.IsMainCockpit)
            {
                var player = MyAPIGateway.Players.GetPlayerControllingEntity(RudderMyGrid);
                IMyShipController RudderShipController = null;

                if (player?.Controller?.ControlledEntity != null)
                    RudderShipController = player.Controller.ControlledEntity as IMyShipController;
            }
        }

        public void UpdateDistanceToCamera()
        {
            if (MyAPIGateway.Utilities.IsDedicated)
                return;

            var dist = Vector3D.Distance(RudderBlock.WorldMatrix.Translation, MyAPIGateway.Session.Camera.WorldMatrix.Translation);
            DistanceToCamera = (float)dist;
        }

        private void ApplyRotationalForce()
        {
            if (YawInput > 0.05f || YawInput < -0.05f)
            {
                Vector3D steeringVector = RudderSubpart.PositionComp.WorldMatrixRef.Backward * YawInput;
                Vector3D dragCounterVector = RudderShipController.PositionComp.WorldMatrixRef.Forward * YawInput;

                MatrixD subpartWorldMatrix = RudderSubpart.PositionComp.WorldMatrixRef;
                var propWash = 0f; //Temp
                var velocity = RudderGrid.Physics.LinearVelocity.Length();
                var maxAuthorityVelocity = 25f;
                double velocityAuthority;

                if (velocity == 0)
                    velocityAuthority = 0f;
                else
                    velocityAuthority = (Math.Pow(velocity, 2) / Math.Pow(maxAuthorityVelocity, 2));

                var rudderLiftForce = 0.5 * 1024 * Math.Pow(MathHelper.Clamp((velocity + propWash), 0f, 25f), 2) * RudderStats.SufaceArea * Math.Sin(MathHelper.ToRadians(RudderCurrentAngle));
            }
        }

        private void SoftRollGravityAlign()
        {
            var gridAngularVelocity = RudderGrid.Physics.AngularVelocity;
            var dampenAggressiveness = 1.0f;
            var gravity = RudderGrid.NaturalGravity;
            if (gravity == Vector3.Zero || gravity == null || RudderGrid.Physics.IsStatic)
                return;

            var rollVector = Vector3.Zero;

            if (gridAngularVelocity.LengthSquared() > Math.Pow(dampenAggressiveness, 2) && rollVector == Vector3.Zero)
            {
                var rollError = Vector3.Dot(RudderShipController.WorldMatrix.Right, -gravity);
                var rollVelocity = Vector3.Dot(gridAngularVelocity, RudderShipController.WorldMatrix.Forward);
                if (Math.Abs(rollVelocity) < dampenAggressiveness) rollVelocity = 0f;
                rollVector = new Vector3(0f, 0f, rollVelocity);

                var forceStrength = GridMass * 0.15f;
                var forceDampen = GridMass * 0.05;

                var forceMagnitude = (rollError * forceStrength) - (rollVelocity * forceDampen);
                var forceToApply = RudderShipController.WorldMatrix.Right * forceMagnitude;

                var applicationPoint = RudderMyGrid.Physics.CenterOfMassWorld + (RudderShipController.WorldMatrix.Down * 10);

                RudderMyGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, forceToApply, applicationPoint, null);
            }
        }

        private void RudderAnimation()
        {
            if (MyAPIGateway.Utilities.IsDedicated || RudderSubpart == null || DistanceToCamera >= 500f)
                return;

            var angleStep = 0.1f;

            if (YawInput > 0.05f)
                RudderCurrentAngle += angleStep;
            else if (YawInput < 0.05f)
                RudderCurrentAngle -= angleStep;
            else
            {
                var tempAngle = RudderCurrentAngle;
                RudderCurrentAngle = MathHelper.Lerp(tempAngle, 0f, angleStep);
            }
                
            RudderCurrentAngle = MathHelper.Clamp(RudderTargetAngle, -RudderMaxAngle, RudderMaxAngle);
            Matrix rotationMatrix = Matrix.CreateRotationY(MathHelper.ToRadians(RudderCurrentAngle));
            Matrix finalMatrix = rotationMatrix * RudderSubpartMatrix;
            RudderSubpart.PositionComp.SetLocalMatrix(ref finalMatrix);
        }

        private void GetNearestProp()
        {
            BoundingSphereD propCheckSphere = new BoundingSphereD(RudderPosition, 10);
            List<IMySlimBlock> blocksInSphere = new List<IMySlimBlock>();
            List<PropellerLogic_V2> propsInSphere = new List<PropellerLogic_V2>();
            blocksInSphere = RudderGrid.GetBlocksInsideSphere(ref propCheckSphere);

            foreach (var block in blocksInSphere)
            {
                if (block.FatBlock != null)
                {
                    var subtype = block.FatBlock.BlockDefinition.SubtypeId;
                    if (Config.PropellerSubtypes.Contains(subtype))
                    {
                        var logic = block.FatBlock.GameLogic?.GetAs<PropellerLogic_V2>();
                        propsInSphere.Add(logic);
                    }
                }
            }

            if (propsInSphere.Count == 1)
            {
                NearestPropellerLogic = propsInSphere[0];
            }
        }
    }
}
