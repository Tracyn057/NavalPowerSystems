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

namespace NavalPowerSystems.Drivetrain
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Gyro), false,
        "NPSDrivetrainRudderSmallCenteredV1",
        "NPSDrivetrainRudderSmallOffsetLeftV1",
        "NPSDrivetrainRudderSmallOffsetRightV1",
        "NPSDrivetrainRudderSmallCenteredV2",
        "NPSDrivetrainRudderSmallOffsetLeftV2",
        "NPSDrivetrainRudderSmallOffsetRightV2"
    )]
    internal class RudderLogic : MyGameLogicComponent
    {
        private IMyCubeBlock Rudder;
        private IMyGyro RudderGyro;
        private MyEntitySubpart RudderSubpart;
        private MatrixD RudderSubpartMatrix;
        private IMyCubeGrid RudderGrid;
        private IMyShipController RudderShipController;

        private float DistanceToCamera = 0f;
        private float RudderMaxAngle = 35f;
        private float RudderTargetAngle = 0f;
        private float RudderCurrentAngle = 0f;
        private const double InnerZone = 0.2094395;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            Rudder = Entity as IMyCubeBlock;
            RudderGyro = Entity as IMyGyro;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (Rudder != null && RudderGyro != null)
            {
                Entity.TryGetSubpart("Rudder", out RudderSubpart);
                if (RudderSubpart != null)
                {
                    RudderSubpartMatrix = RudderSubpart.PositionComp.LocalMatrixRef;
                }
                RudderGrid = Rudder.Parent as IMyCubeGrid;
                if (RudderGrid == null)
                {
                    NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                    return;
                }

                var player = MyAPIGateway.Players.GetPlayerControllingEntity(RudderGrid);

                if (player?.Controller?.ControlledEntity != null)
                {
                    RudderShipController = player.Controller.ControlledEntity as IMyShipController;
                }
            }

            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateBeforeSimulation()
        {
            if (!Rudder.IsWorking) return;

            if (RudderShipController == null || !RudderShipController.IsWorking || !RudderShipController.IsMainCockpit)
            {
                var player = MyAPIGateway.Players.GetPlayerControllingEntity(RudderGrid);

                if (player?.Controller?.ControlledEntity != null)
                {
                    RudderShipController = player.Controller.ControlledEntity as IMyShipController;
                }
            }

            RudderGyro.GyroOverride = true;

            float yawInput = 0f;
            if (RudderShipController != null)
                yawInput = RudderShipController.MoveIndicator.X;
            AlignToGravity(yawInput);
            RudderAnimation(yawInput);
        }

        public override void UpdateBeforeSimulation100()
        {
            if (MyAPIGateway.Utilities.IsDedicated)
                return;

            var dist = Vector3D.Distance(Rudder.WorldMatrix.Translation, MyAPIGateway.Session.Camera.WorldMatrix.Translation);
            DistanceToCamera = (float)dist;
        }

        private void AlignToGravity(float yawInput)
        {
            if (RudderShipController == null || RudderGrid == null || RudderGrid.Physics.IsStatic || !RudderShipController.IsWorking) 
                return;

            Vector3D gravity = RudderShipController.GetNaturalGravity();
            if (gravity.LengthSquared() < 0.001) 
                return;

            Vector3D gravityDir = Vector3D.Normalize(gravity);
            Vector3D shipDown = RudderShipController.WorldMatrix.Down;

            Vector3D cross = shipDown.Cross(-gravityDir);

            MatrixD worldToLocal = MatrixD.Transpose(RudderShipController.WorldMatrix);
            Vector3 localError = Vector3.TransformNormal((Vector3)cross, worldToLocal);

            double rollAngle = Math.Asin(localError.X);
            double pitchAngle = Math.Asin(localError.Z);

            Vector3 localAngleVel = Vector3.TransformNormal(RudderGrid.Physics.AngularVelocity, worldToLocal);

            double rollTorque = ComputeRestore(rollAngle) - localAngleVel.X * 6;
            double pitchTorque = ComputeRestore(pitchAngle) - localAngleVel.Z * 6;

            double speed = RudderGrid.Physics.LinearVelocity.Length();
            double speedFactor = MathHelper.Clamp(speed / 15.0, 0.0, 1.0);

            rollTorque *= speedFactor;
            pitchTorque *= speedFactor;

            float radToRPM = MathHelper.RadiansPerSecondToRPM;

            float rollRPM = (float)(rollTorque * radToRPM);
            float pitchRPM = (float)(pitchTorque * radToRPM);

            rollRPM = MathHelper.Clamp(rollRPM, -5f, 5f);
            pitchRPM = MathHelper.Clamp(pitchRPM, -5f, 5f);

            RudderGyro.Roll = -pitchRPM * 0.15f;
            RudderGyro.Pitch = -rollRPM * 0.15f;

            float manualYaw = yawInput * 0.15f * (float)speedFactor;
            float autoYaw = AutoYawReturn() * 0.15f * (float)speedFactor;

            if (Math.Abs(speed) > 2)
            {
                RudderGyro.Yaw = manualYaw + autoYaw;
            }
            else
            {
                RudderGyro.Yaw = manualYaw;
            }
            
        }

        private double ComputeRestore(double angle)
        {
            double sign = Math.Sign(angle);
            double abs = Math.Abs(angle);

            if (abs < InnerZone)
            {
                return -angle * 1.5;
            }
            else
            {
                double excess = abs - InnerZone;

                double soft = InnerZone * 1.5 + excess * 0.75;

                return -sign * soft;
            }
        }

        private float AutoYawReturn()
        {
            Vector3D gravityDir = Vector3D.Normalize(RudderShipController.GetNaturalGravity());
            Vector3D planeNormal = -gravityDir;
            Vector3D shipForward = RudderShipController.WorldMatrix.Forward;
            Vector3D velocityDir = Vector3D.Normalize(RudderGrid.Physics.LinearVelocity);
            double forwardVelocity = shipForward.Dot(velocityDir);
            bool movingBackwards = forwardVelocity < -0.1;

            Vector3D fwdProj =
                shipForward - planeNormal * shipForward.Dot(planeNormal);

            Vector3D velProj =
                velocityDir - planeNormal * velocityDir.Dot(planeNormal);

            double fwdLenSq = fwdProj.LengthSquared();
            double velLenSq = velProj.LengthSquared();

            if (fwdLenSq < 1e-6 || velLenSq < 1e-6) return 0f;

            fwdProj.Normalize();
            velProj.Normalize();

            if (movingBackwards)
            {
                fwdProj = -fwdProj;
            }

            double sinYaw = planeNormal.Dot(fwdProj.Cross(velProj));
            double cosYaw = fwdProj.Dot(velProj);

            double yawAngle = Math.Atan2(sinYaw, cosYaw);

            return (float)-yawAngle;
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
