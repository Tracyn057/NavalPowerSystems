using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
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
using VRage.Utils;
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
        private MyCubeGrid _rudderGrid;
        private MyEntitySubpart _rudderSubpart;
        private Matrix _initialLocalMatrix;

        private bool _isAutoCenter = true;

        private float _gridMass = 1.0f;
        private float _maxAngle = 35f;
        private float _degreeSpeed = 0.15f;
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
            _rudderGrid = (MyCubeGrid)Entity.Parent;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
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
        }

        private void MarkForEngineSearch(IMySlimBlock block)
        {
            _enginesCached = false;
        }

        private void GetAngle()
        {
            var controller = MyAPIGateway.Players.GetPlayerControllingEntity(_myRudder.CubeGrid);
            IMyShipController shipController = controller?.Controller?.ControlledEntity as IMyShipController;

            float angle = 0f;
            if (shipController != null)
            {
                angle = shipController.MoveIndicator.X;
            }
            
            if (angle != 0f)
            {
                _targetAngle += angle * _degreeSpeed;
            }
            else if (_isAutoCenter)
            {
                _targetAngle = MathHelper.Lerp(_targetAngle, 0f, _degreeSpeed);
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

            Vector3D velocity = _rudderGrid.Physics.LinearVelocity;
            double speed = velocity.Length();
            double propWash = _currentThrottle * 15.0;
            double effectiveSpeed = speed + propWash;
            float finalThrust = (_gridMass * (float)effectiveSpeed * _degreeSpeed) * (float)Math.Sin(MathHelper.ToRadians(_currentAngle));

            float finalThrustN = finalThrust * 1000000f; // Convert MN to N for physics application

            if (Math.Abs(finalThrustN) > 100)
            {
                Vector3D thrustVector = _myRudder.WorldMatrix.Right * (float)finalThrustN;
                var BlockPos = _myRudder.PositionComp.GetPosition();
                _rudderGrid.Physics.AddForce(
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
