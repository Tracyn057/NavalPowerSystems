using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace NavalPowerSystems.Drivetrain
{
    public class NavalGridManager
    {
        public IMyCubeGrid IMyGrid;
        public MyCubeGrid MyGrid;
        public float MyGridMass => MyShipController.CalculateShipMass().TotalMass;
        public IMyShipController MyShipController;
        private IMyShipController MyShadowController;
        private bool ShadowControllerCaptured;
        private MatrixD ShadowControllerMatrix;
        public MatrixD GridMatrixRef => MyShipController?.WorldMatrix ?? ShadowControllerMatrix;
        public float Update100Coefficient = 1f;
        public float GridAverageThrust = 0f;
        private MyResourceSinkComponent MyResourceSink;
        private const float WaterDensity = 1024f;
        private const float Gravity = 9.81f;
        private const float PhysicsStep = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        public Dictionary<int, DrivetrainSystem> GridSystems = new Dictionary<int, DrivetrainSystem>();
        private List<RudderLogic> GridRudders = new List<RudderLogic>();
        private List<IDrivetrainPart> GridPropellers = new List<IDrivetrainPart>();

        public float DistanceToCamera { get; private set; }
        public float YawInput { get; private set; }
        public float RollInput { get; private set; }
        public float PitchInput { get; private set; }
        public float ForwardInput { get; private set; }

        public NavalGridManager(IMyCubeGrid grid)
        {
            IMyGrid = grid;
            MyGrid = grid as MyCubeGrid;
        }

        public void InitResourceSystem()
        {
            MyResourceSink = new MyResourceSinkComponent();
            var controller = MyShipController ?? MyShadowController;
        }

        public void UpdateTick()
        {
            if (IMyGrid.Physics == null) return;

            float gridVelocity = IMyGrid.Physics?.LinearVelocity.Length() ?? 0f;
            if (gridVelocity > 0.1f)
            {
                float dragForce = Update100Coefficient * (gridVelocity * gridVelocity) * -Math.Sign(gridVelocity);
                IMyGrid.Physics.AddForce(MyPhysicsForceType.ADD_BODY_FORCE_AND_BODY_TORQUE, -IMyGrid.Physics.LinearVelocity * dragForce, null, null);
            }

            GridAverageThrust = 0f;
            foreach (var p in GridPropellers)
            {
                GridAverageThrust += (float)p.Torque_Out;
            }
            var numP = GridPropellers.Count;
            GridAverageThrust *= numP;

            RecalculateController();
            GetControlInput();
        }

        public void UpdateTick100()
        {
            if (IMyGrid.Physics == null) return;

            Update100Coefficient = CalculateWaveCoefficient();
            RecalculateController100();

            CleanLists();

            if (!MyAPIGateway.Utilities.IsDedicated)
                UpdateCameraDistance();
        }

        private void UpdateCameraDistance()
        {
            if (MyAPIGateway.Utilities.IsDedicated)
                return;

            DistanceToCamera = (float)Vector3D.Distance(IMyGrid.WorldMatrix.Translation, MyAPIGateway.Session.Camera.WorldMatrix.Translation);
        }

        public void RegisterRudder(RudderLogic block)
        {
            if (!GridRudders.Contains(block))
                GridRudders.Add(block);
        }

        public void RegisterProp(IDrivetrainPart block)
        {
            if (!GridPropellers.Contains(block))
                GridPropellers.Add(block);
        }

        private void CleanLists()
        {
            GridRudders.RemoveAll(r => r == null || r.MarkedForClose);
            GridPropellers.RemoveAll(p => p == null);
        }

        public float CalculateWaveCoefficient()
        {
            Vector3 localVelocity = Vector3.TransformNormal(IMyGrid.Physics.LinearVelocity, IMyGrid.PositionComp.WorldMatrixInvScaled);
            float velocityZ = Math.Abs(localVelocity.Z);

            if (velocityZ < 0.1f) return 0f;

            float hullLength = (IMyGrid.Max.Z - IMyGrid.Min.Z + 1) * IMyGrid.GridSize;
            float frontArea = (IMyGrid.Max.X - IMyGrid.Min.X + 1) * (IMyGrid.Max.Y - IMyGrid.Min.Y + 1) * IMyGrid.GridSize * IMyGrid.GridSize;
            float effectiveArea = frontArea * 0.5f; // Assume only half the front area contributes to wave drag to avoid complex calculations

            float froude = velocityZ / (float)Math.Sqrt(Gravity * hullLength);

            float waveFactor = (froude > 0.1f) ? (float)Math.Pow(froude / 0.4f, 4) : 0f;

            return WaterDensity * PhysicsStep * effectiveArea * waveFactor;
        }

        public void RecalculateController()
        {
            if (MyShadowController != null && MyShipController.IsWorking)
            {
                ShadowControllerMatrix = MyShipController.WorldMatrix;
                ShadowControllerCaptured = true;
            }
            else
            {
                MyShipController = null;
                if (!ShadowControllerCaptured)
                {
                    ShadowControllerMatrix = IMyGrid.WorldMatrix;
                    ShadowControllerCaptured = true;
                }
            }
        }

        public void RecalculateController100()
        {
            if (MyShipController != null && MyShipController.IsWorking) return;

            var player = MyAPIGateway.Players.GetPlayerControllingEntity(IMyGrid);
            MyShipController = null;

            if (player?.Controller?.ControlledEntity != null)
            {
                MyShipController = player.Controller.ControlledEntity as IMyShipController;
            }
        }

        private void VerifyController()
        {

        }

        private void CreateShadowController()
        {

        }

        private void GetControlInput()
        {
            if (MyShipController != null)
            {
                PitchInput = MathHelper.Clamp(MyShipController?.RotationIndicator.Y ?? 0f, -1f, 1f);
                YawInput = MathHelper.Clamp(MyShipController?.RotationIndicator.X ?? 0f, -1f, 1f);
                RollInput = MathHelper.Clamp(MyShipController?.RollIndicator ?? 0f, -1f, 1f);
                ForwardInput = MathHelper.Clamp(-MyShipController?.MoveIndicator.Z ?? 0f, -1f, 1f);
            }
            else
            {
                PitchInput = 0f;
                YawInput = 0f;
                RollInput = 0f;
                ForwardInput = 0f;
            }
           
        }
    }
}