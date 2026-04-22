using System;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;

namespace NavalPowerSystems.Drivetrain
{
    public class NavalGridManager
    {
        public IMyCubeGrid IGrid;
        public MyCubeGrid Grid;
        public float Update100Coefficient = 1f;
        private const float WaterDensity = 1024f;
        private const float Gravity = 9.81f;
        private const float PhysicsStep = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;

        public NavalGridManager(IMyCubeGrid grid)
        {
            IGrid = grid;
            Grid = grid as MyCubeGrid;
        }

        public void UpdateTick()
        {
            if (Grid.Physics == null) return;

            float gridVelocity = Grid.Physics?.LinearVelocity.Length() ?? 0f;
            if (gridVelocity > 0.1f)
            {
                float dragForce = Update100Coefficient * (gridVelocity * gridVelocity) * -Math.Sign(gridVelocity);
                Grid.Physics.AddForce(MyPhysicsForceType.ADD_BODY_FORCE_AND_BODY_TORQUE, -Grid.Physics.LinearVelocity * dragForce, null, null);
            }
        }

        public void UpdateTick100()
        {
            if (Grid.Physics == null) return;
            Update100Coefficient = CalculateWaveCoefficient();
        }

        public float CalculateWaveCoefficient()
        {
            Vector3 localVelocity = Vector3.TransformNormal(Grid.Physics.LinearVelocity, Grid.PositionComp.WorldMatrixInvScaled);
            float velocityZ = Math.Abs(localVelocity.Z);

            if (velocityZ < 0.1f) return 0f;

            float hullLength = (Grid.Max.Z - Grid.Min.Z + 1) * Grid.GridSize;
            float frontArea = (Grid.Max.X - Grid.Min.X + 1) * (Grid.Max.Y - Grid.Min.Y + 1) * Grid.GridSize * Grid.GridSize;
            float effectiveArea = frontArea * 0.5f; // Assume only half the front area contributes to wave drag to avoid complex calculations

            float froude = velocityZ / (float)Math.Sqrt(Gravity * hullLength);

            float waveFactor = (froude > 0.1f) ? (float)Math.Pow(froude / 0.4f, 4) : 0f;

            return WaterDensity * PhysicsStep * effectiveArea * waveFactor;
        }
    }
}