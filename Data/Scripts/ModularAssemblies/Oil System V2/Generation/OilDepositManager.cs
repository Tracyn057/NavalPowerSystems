using ProtoBuff;

namespace BlackGold.Generation
{
    [ProtoContract]
    public class OilWell
    {
        [ProtoMember(1)]
        public long OilWellId;
        [ProtoMember(2)]
        public Vector3D OilWellLocation;
        [ProtoMember(3)]
        public float OilWellDepth;
        [ProtoMember(4)]
        public double OilWellCapacity;
        [ProtoMember(5)]
        public float OilWellTapProgress;
        [ProtoMember(6)]
        public WellState OilWellState;
    }

    public enum WellState
    {
        Scanned,
        Inactive,
        Active,
        Depleted
    }

    public static class OilWellGenerator
    {
        public Dictionary<long, OilWell> ActiveDeposits = new Dictionary<long, OilWell>();

        public static long? GetOilDepositId(IMyTerminalBlock scannerBlock, MyPlanet planet)
        {
            if (planet == null || !WaterModAPI.HasWater(planet)) return null;

            Vector3D localPos = scannerBlock.Position - planet.PositionComp.GetPosition();

            // Snap position to grid
            long sX = (long)Math.Floor(localPos.X / OilSystem_Config.DetectorGridSizeInMeters);
            long sZ = (long)Math.Floor(localPos.Z / OilSystem_Config.DetectorGridSizeInMeters);

            // Create a unique seed for this grid location on this planet
            long depositId = sX * 73856093 ^ sZ * 83492791 ^ planet.EntityId;
            Random rand = new Random((int)depositId);

            if (rand.NextDouble() < OilSystem_Config.DepositRarityThreshold) return null;

            return depositId;
        }

        public OilWell GenerateOilWell(long depositId, Vector3D worldPos)
        {
            if (ActiveDeposits.ContainsKey(depositId))
            {
                return ActiveDeposits[depositId];
            }

            Random rand = new Random((int)depositId);

            OilWell newWell = new OilWell
            {
                OilWellId = depositId,
                OilWellLocation = worldPos,
                OilWellDepth = rand.Next(OilSystem_Config.OilDepositDepthMin, OilSystem_Config.OilDepositDepthMax),
                OilWellCapacity = rand.Next(OilSystem_Config.FiniteDepositYieldLitersMinimum, OilSystem_Config.FiniteDepositYieldLitersMaximum),
                OilWellTapProgress = 0f,
                OilWellState = WellState.Scanned
            };

            ActiveDeposits.Add(depositId, newWell);
            return newWell;
        }

        public void PerformAreaScan(IMyTerminalBlock scannerBlock, double scanRadius)
        {
            MyPlanet planet = MyGamePruningStructure.GetClosestPlanet(scannerBlock.WorldMatrix.Translation);
            if (planet == null) return;

            Vector3D origin = scannerBlock.WorldMatrix.Translation;

            // Determine how many grid squares to check based on radius
            int gridRange = (int)Math.Ceiling(scanRadius / OilSystem_Config.DetectorGridSizeInMeters);

            for (int x = -gridRange; x <= gridRange; x++)
            {
                for (int z = -gridRange; z <= gridRange; z++)
                {
                    // Calculate the center of the neighboring square
                    Vector3D scanPos = origin + new Vector3D(x * OilSystem_Config.DetectorGridSizeInMeters, 0, z * OilSystem_Config.DetectorGridSizeInMeters);

                    // Get the ID
                    long? depositId = OilMap.GetOilID(scanPos, planet);

                    if (depositId.HasValue)
                    {
                        // Define/Retrieve the Persistent Object
                        OilWell well = GenerateOilWell(depositId.Value, scanPos);
                    }
                }
            }
        }
    }
}