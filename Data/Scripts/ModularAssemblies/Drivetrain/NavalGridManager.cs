using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using VRageMath;

namespace NavalPowerSystems.Drivetrain_V2
{
    internal class NavalGridManager
    {
        public IMyCubeGrid IGrid { get; private set; }
        public MyCubeGrid Grid { get; private set; }
        public float Update100Coefficient { get; private set; } = 1f;
        private const float WaterDensity = 1024f; // kg/m^3
        private const float Gravity = 9.81f; // m/s^2
        private const float PhysicsStep = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS; // 1/60th of a second

        public NavalGridManager(IMyCubeGrid grid)
        {
            IGrid = grid;
            Grid = grid as MyCubeGrid;
        }

        public void Update()
        {
            if (Grid.Physics == null) return;

            float gridVelocity = Grid.Physics?.LinearVelocity.Length() ?? 0f;
            if (gridVelocity > 0.1f)
            {
                float dragForce = Update100Coefficient * (gridVelocity * gridVelocity);
                Grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -Grid.Physics.LinearVelocity * dragForce, null, null);
            }
        }

        public void Update100()
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
            float effectiveArea = frontArea * 0.5f; // Assume only half the front area contributes to wave drag, as a simplification

            float froude = velocityZ / (float)Math.Sqrt(Gravity * hullLength);

            float waveFactor = (froude > 0.1f) ? (float)Math.Pow(froude / 0.4f, 4) : 0f;

            return WaterDensity * PhysicsStep * effectiveArea * waveFactor;
        }
    }
}