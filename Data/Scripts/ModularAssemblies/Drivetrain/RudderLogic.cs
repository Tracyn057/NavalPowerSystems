using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace NavalPowerSystems.Drivetrain
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), false,
            ""
    )]
    internal class RudderLogic : MyGameLogicComponent
    {
        private IMyTerminalBlock _rudder;
        private IMyCubeBlock _myRudder;
        private MyEntitySubpart _rudderSubpart;
        private GearboxLogic _gearboxLogic;
        private Matrix _initialLocalMatrix;

        private bool _isAutoCenter = true;

        private float _maxAngle = 35f;
        private float _degreeSpeed = 0.15f;
        private float _currentAngle = 0f;
        private float _lastAngle = 0f;
        private float _distToCamera = 0f;
        private float _targetAngle = 0f;
        private float _currentThrottle = 0f;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _rudder = (IMyTerminalBlock)Entity;
            _myRudder = (MyCubeBlock)Entity;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            _rudder.AppendingCustomInfo += AppendCustomInfo;

            Entity.TryGetSubpart("Rudder", out _rudderSubpart);
            if (_rudderSubpart != null)
            {
                _initialLocalMatrix = _rudderSubpart.PositionComp.LocalMatrix;
            }

            NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateBeforeSimulation10()
        {
            _rudder.RefreshCustomInfo();
        }

        public override void UpdateBeforeSimulation()
        {
            if (_targetAngle > 0f)
            {
                GetAngle();
                ApplyForce();
            }
            
        }

        private void GetAngle()
        {
            var controller = MyAPIGateway.CubeGrid.ControlSystem.RelativeControl as IMyShipController;
            if (controller == null) return 0f;

            return controller.MoveIndicator.X;
        }

        private void GetThrottle()
        {
            if (_gearboxLogic != null)
            {
                _currentThrottle = _gearboxLogic.urrentThrottle;
            }
            else
            {
                var controller = MyAPIGateway.CubeGrid.ControlSystem.RelativeControl as IMyShipController;
                if (controller != null)
                {
                    _currentThrottle = controller.MoveIndicator.Z;
                }
            }
        }

        private void ApplyForce()
        {
            var grid = _myRudder.CubeGrid as MyCubeGrid;

            if (grid.IsPreview || grid.Physics == null || !grid.Physics.Enabled || grid.Physics.IsStatic)
                return;

            Vector3D velocity = grid.Physics.LinearVelocity;
            double speed = velocity.Length();
            float finalThrust = 0f;

            finalThrust *= 1000000f; // Convert MN to N for physics application

            if (finalThrust > 100)
            {
                Vector3D thrustVector = _myRudder.WorldMatrix.Right * (float)finalThrust;
                var BlockPos = _myRudder.PositionComp.GetPosition();
                grid.Physics.AddForce(
                    MyPhysicsForceType.APPLY_WORLD_FORCE,
                    thrustVector,
                    BlockPos,
                    null
                );
            }
        }

        private void RudderAnimation()
        {
            //if (_rudderSubpart == null || MyAPIGateway.Utilities.IsDedicated)
            //    return;

            //Vector2 gimbal = new Vector2(ThrustLR);

            //if (_distToCamera >= 1000f)
            //    gimbal = Vector2.Zero;

            //float YAngle = gimbal.Y - _lastAngle;
            //if (YAngle != 0)
            //{
            //    Matrix rudderMatrix = _rudderSubpart.PositionComp.LocalMatrixRef;
            //    rudderMatrix = Matrix.CreateRotationY(-YAngle * (float)JETGimbalAngle) * rudderMatrix;
            //    _rudderSubpart.PositionComp.SetLocalMatrix(ref rudderMatrix);
            //    _lastAngle = gimbal.Y;
            //}
        }

        public void UpdateDistanceToCamera()
        {
            if (MyAPIGateway.Utilities.IsDedicated)
                return;

            var dist = Vector3D.Distance(_myRudder.WorldMatrix.Translation, MyAPIGateway.Session.Camera.WorldMatrix.Translation);
            _distToCamera = (float)dist;
        }

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            sb.AppendLine($"Angle: {_currentAngle:F4} degrees");
        }

        public override void OnRemovedFromScene()
        {
            if (_rudder != null) _rudder.AppendingCustomInfo -= AppendCustomInfo;
        }

    }
}
