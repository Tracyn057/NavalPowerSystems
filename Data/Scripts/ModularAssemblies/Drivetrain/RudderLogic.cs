using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using SharpDX.XInput;
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
using VRage.Utils;
using VRageMath;

namespace NavalPowerSystems.Drivetrain
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), false,
            "NPSDrivetrainRudderSmallCenteredV1"
    )]
    internal class RudderLogic : MyGameLogicComponent
    {
        private IMyTerminalBlock _rudder;
        private IMyCubeBlock _myRudder;
        private MyCubeGrid _rudderGrid;
        private IMyShipController _gridController;
        private MyEntitySubpart _rudderSubpart;
        private Matrix _initialLocalMatrix;

        private bool _isAutoCenter = true;

        private float _gridMass = 1.0f;
        private float _maxAngle = 35f;
        private float _degreeSpeed = 0.05f;
        private float _currentAngle = 0f;
        private float _lastAngle = 0f;
        private float _distToCamera = 0f;
        private float _targetAngle = 0f;
        private float _currentThrottle = 0f;
        private bool _enginesCached = false;
        private List<NavalEngineLogicBase> _cachedEngines = new List<NavalEngineLogicBase>();

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _rudder = (IMyTerminalBlock)Entity;
            _myRudder = (MyCubeBlock)Entity;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            _rudderGrid = Entity.Parent as MyCubeGrid;
            if (_rudderGrid == null)
            {
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                return;
            }
            FindShipControllers(_rudderGrid);
            _rudder.AppendingCustomInfo += AppendCustomInfo;

            _enginesCached = false;
            _rudderGrid.OnBlockAdded += MarkForEngineSearch;
            _rudderGrid.OnBlockRemoved += MarkForEngineSearch;

            Entity.TryGetSubpart("Rudder", out _rudderSubpart);
            if (_rudderSubpart != null)
            {
                _initialLocalMatrix = _rudderSubpart.PositionComp.LocalMatrixRef;
            }

            NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateBeforeSimulation10()
        {
            if (!_enginesCached)
            {
                CacheEngines();
                _enginesCached = true;
            }
            GetThrottle();
            _rudder.RefreshCustomInfo();
        }

        public override void UpdateBeforeSimulation()
        {
            GetAngle();
            ApplyForce();
            if (_distToCamera <= 1000)
            {
                RudderAnimation();
            }
        }

        public override void UpdateAfterSimulation100()
        {
            UpdateDistanceToCamera();
            _gridMass = _rudderGrid.GetCurrentMass() / 1000000f;
            _degreeSpeed = MathHelper.Clamp(2.0f / _gridMass, 0.05f, 0.5f);
        }

        private void MarkForEngineSearch(IMySlimBlock block)
        {
            _enginesCached = false;
        }

        private void FindShipControllers(IMyCubeGrid grid)
        {
            var terminalSystem = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);
            if (terminalSystem == null) { return; }
            List<IMyShipController> controllers = new List<IMyShipController>();
            terminalSystem.GetBlocksOfType<IMyShipController>(controllers);

            foreach (var control in controllers)
            {
                if 
                    (   
                    control.IsUnderControl
                    || _gridController == null
                    || (!_gridController.IsUnderControl 
                    && control.IsMainCockpit)
                    )
                    _gridController = control;
            }
        }

        private void GetAngle()
        {
            var controller = MyAPIGateway.Players.GetPlayerControllingEntity(_myRudder.CubeGrid);
            IMyShipController shipController = controller?.Controller?.ControlledEntity as IMyShipController;

            float angle = shipController?.MoveIndicator.X ?? 0f;
            
            if (angle != 0f)
            {
                _targetAngle += angle * _degreeSpeed;
            }
            else if (_isAutoCenter)
            {
                if (Math.Abs(_targetAngle) > 0.01f)
                {
                    _targetAngle = MathHelper.Lerp(_targetAngle, 0f, 0.01f);
                }
            }

            _targetAngle = MathHelper.Clamp(_targetAngle, -_maxAngle, _maxAngle);

            _currentAngle = MathHelper.Lerp(_currentAngle, _targetAngle, _degreeSpeed);
        }

        private void CacheEngines()
        {
            _cachedEngines.Clear();
            var grid = _rudderGrid as IMyCubeGrid;
            if (_rudderGrid == null) return;

            List<IMySlimBlock> blocks = new List<IMySlimBlock>();
            grid.GetBlocks(blocks);

            foreach ( var block in blocks)
            {
                if (block.FatBlock == null) continue;

                var logic = block.FatBlock.GameLogic.GetAs<NavalEngineLogicBase>();
                if (logic != null)
                {
                    _cachedEngines.Add(logic);
                }
            }
        }

        private void GetThrottle()
        {
            if (_cachedEngines.Count == 0)
            {
                _currentThrottle = 0f;
                return;
            }

            float throttle = 0f;
            foreach (var engine in _cachedEngines)
            {
                if (engine._currentThrottle > throttle)
                    throttle = engine._currentThrottle;
            }

            _currentThrottle = throttle;
        }

        private void ApplyForce()
        {
            if (_rudderGrid.IsPreview || _rudderGrid.Physics == null || !_rudderGrid.Physics.Enabled || _rudderGrid.Physics.IsStatic)
                return;

            double velocity = _rudderGrid.Physics.LinearVelocity.Dot(_rudderGrid.WorldMatrix.Forward);
            double speed = Math.Abs(velocity);

            if (speed < 1) return;

            float rudderLiftCoef = 0.05f;
            float lifMagnitude = (float)(_gridMass * speed * rudderLiftCoef * Math.Sin(MathHelper.ToRadians(_currentAngle)));
            float forceN = lifMagnitude * 1000000f;
            Vector3D linearForce = _rudderGrid.WorldMatrix.Right * (forceN * 1f);
            double leverArm = Vector3D.Distance(_myRudder.WorldMatrix.Translation, _rudderGrid.Physics.CenterOfMassWorld);
            Vector3 gravity = _gridController.GetNaturalGravity();
            Vector3D skyAxis = Vector3D.Normalize(-gravity);

            Vector3D angularTorque = skyAxis * (forceN * leverArm);

            _rudderGrid.Physics.AddForce(
                MyPhysicsForceType.ADD_BODY_FORCE_AND_BODY_TORQUE,
                linearForce,
                null,
                angularTorque
            );
        }

        //private void ApplyForce()
        //{
        //    if (_rudderGrid.IsPreview || _rudderGrid.Physics == null || !_rudderGrid.Physics.Enabled || _rudderGrid.Physics.IsStatic)
        //        return;

        //    Vector3D velocity = _rudderGrid.Physics.LinearVelocity;
        //    Vector3D forward = _rudderGrid.WorldMatrix.Forward;

        //    double forwardSpeed = velocity.Dot(forward);
        //    double speed = Math.Max(0, forwardSpeed);

        //    if (speed < 1.5 && _currentThrottle < 0.1) return;
        //    double propWash = _currentThrottle * 1.25;
        //    double effectiveSpeed = MathHelper.Clamp(speed, 0, 10) + propWash;

        //    float liftCoef = 0.05f;
        //    float maxForce = _gridMass * (float)effectiveSpeed;
        //    float forceMagnitude = (float)(_gridMass * effectiveSpeed * liftCoef * Math.Sin(MathHelper.ToRadians(_currentAngle)));
        //    forceMagnitude = MathHelper.Clamp(forceMagnitude, -maxForce, maxForce);
        //    float finalThrustN = forceMagnitude * 1000000f; // Convert MN to N for physics application

        //    if (Math.Abs(finalThrustN) > 100)
        //    {
        //        // Adjust thrust application zone to above rudder in line with COM
        //        Vector3D comToRudder = _myRudder.WorldMatrix.Translation - _rudderGrid.Physics.CenterOfMassWorld;
        //        //Vector3D offset = Vector3D.ProjectOnVector(ref comToRudder, ref forward);
        //        //Vector3D forcePos = _rudderGrid.Physics.CenterOfMassWorld + offset;

        //        double leverDistance = Vector3D.Dot(comToRudder, forward);
        //        Vector3D forcePos = _rudderGrid.Physics.CenterOfMassWorld + (forward * leverDistance);

        //        Vector3D thrustVector = _myRudder.WorldMatrix.Right * (float)finalThrustN;
        //        _rudderGrid.Physics.AddForce(
        //            MyPhysicsForceType.APPLY_WORLD_FORCE,
        //            thrustVector,
        //            forcePos,
        //            null
        //        );
        //    }
        //}

        private void RudderAnimation()
        {
            if (MyAPIGateway.Utilities.IsDedicated || _rudderSubpart == null || _distToCamera >= 1000f)
                return;

            _currentAngle = MathHelper.Lerp(_currentAngle, _targetAngle, 0.05f);

            Matrix rotationMatrix = Matrix.CreateRotationY(MathHelper.ToRadians(_currentAngle));

            Matrix finalMatrix = rotationMatrix * _initialLocalMatrix;
            _rudderSubpart.PositionComp.SetLocalMatrix(ref finalMatrix);
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
            base.OnRemovedFromScene();
            if (_rudderGrid != null )
            {
                _rudderGrid.OnBlockAdded -= MarkForEngineSearch;
                _rudderGrid.OnBlockRemoved -= MarkForEngineSearch;
            }
            if (_rudder != null) _rudder.AppendingCustomInfo -= AppendCustomInfo;
        }

    }
}
