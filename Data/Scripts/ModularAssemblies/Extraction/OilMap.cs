using System;
using Sandbox.ModAPI;
using VRageMath;
using VRage.Game.ModAPI;
using VRage.Game.Entity;
using Sandbox.Game.Entities;
using Jakaria.API;

namespace NavalPowerSystems.Extraction
{
    public static class OilMap
    {
        public static bool oilGenDebug = false;
        public static float GetOil(Vector3D worldPos, MyPlanet planet)
        {
            if (oilGenDebug)
            {
                long xGrid = (long)Math.Floor(worldPos.X / Config.gridSize);
                long zGrid = (long)Math.Floor(worldPos.Z / Config.gridSize);

                return ((xGrid + zGrid) % 2 == 0) ? 1.0f : 0.0f;
            }

            if (planet == null || WaterModAPI.HasWater(planet) == false) return 0f;

            long sX = (long)Math.Floor(worldPos.X / Config.gridSize);
            long sZ = (long)Math.Floor(worldPos.Z / Config.gridSize);

            int seed = (int)(sX * 73856093 ^ sZ * 83492791 ^ planet.EntityId);
            Random rand = new Random(seed);

            double roll = rand.NextDouble();
            if (roll < Config.rarityThreshold) return 0f;

            double offsetX = (rand.NextDouble() - 0.5) * Config.gridSize * 0.6;
            double offsetZ = (rand.NextDouble() - 0.5) * Config.gridSize * 0.6;

            Vector3D sectorCenter = new Vector3D(sX * Config.gridSize + (Config.gridSize / 2), 0, sZ * Config.gridSize + (Config.gridSize / 2));
            Vector3D blobCenter = sectorCenter + new Vector3D(offsetX, 0, offsetZ);

            double distToBlob = Vector2D.Distance(new Vector2D(worldPos.X, worldPos.Z), new Vector2D(blobCenter.X, blobCenter.Z));

            double angle = Math.Atan2(worldPos.Z - blobCenter.Z, worldPos.X - blobCenter.X);
            double distortion = (50.0 * Math.Sin(angle * 3.0 + seed)) + (30.0 * Math.Cos(angle * 5.0));
            double currentWavyRadius = Config.baseRadius + distortion;

            if (distToBlob > currentWavyRadius) return 0f;

            Vector3D surfacePoint = planet.GetClosestSurfacePointGlobal(worldPos);
            double surfaceRadius = (surfacePoint - planet.WorldMatrix.Translation).Length();

            if (surfaceRadius > planet.AverageRadius + 50) return 0f;

            float distanceFactor = (float)(1.0 - (distToBlob / currentWavyRadius));

            float richnessFactor = (float)((roll - Config.rarityThreshold) / (1.0 - Config.rarityThreshold));

            return MathHelper.Clamp(distanceFactor * richnessFactor, 0f, 1f);
        }

        public static bool HasOil(Vector3D headPos)
        {
            var planet = MyGamePruningStructure.GetClosestPlanet(headPos);
            return planet != null && GetOil(headPos, planet) > 0f;
        }
    }
}