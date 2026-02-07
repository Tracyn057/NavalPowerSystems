using Jakaria.API;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRageMath;

namespace NavalPowerSystems.Extraction
{
    public static class OilMap
    {
        public static bool oilGenDebug = false;

        public static float GetOil(Vector3D worldPos, MyPlanet planet)
        {
            if (planet == null || !WaterModAPI.HasWater(planet)) return 0f;


            Vector3D localPos = worldPos - planet.PositionComp.GetPosition();

            if (oilGenDebug)
            {
                long xGrid = (long)Math.Floor(localPos.X / Config.gridSize);
                long zGrid = (long)Math.Floor(localPos.Z / Config.gridSize);
                return ((xGrid + zGrid) % 2 == 0) ? 1.0f : 0.0f;
            }

            // 2. Snap to Grid
            long sX = (long)Math.Floor(localPos.X / Config.gridSize);
            long sZ = (long)Math.Floor(localPos.Z / Config.gridSize);

            // 3. Unique Seed for this Sector
            int seed = (int)(sX * 73856093 ^ sZ * 83492791 ^ planet.EntityId);
            Random rand = new Random(seed);

            if (rand.NextDouble() < Config.rarityThreshold) return 0f;

            // 4. Blob Center relative to Sector
            double offsetX = (rand.NextDouble() - 0.5) * Config.gridSize * 0.7;
            double offsetZ = (rand.NextDouble() - 0.5) * Config.gridSize * 0.7;

            Vector2D localCenter = new Vector2D(sX * Config.gridSize + (Config.gridSize / 2) + offsetX, sZ * Config.gridSize + (Config.gridSize / 2) + offsetZ);
            double dist = Vector2D.Distance(new Vector2D(localPos.X, localPos.Z), localCenter);

            // Shape Logic
            double distortion = 60.0 * Math.Sin(seed);
            double totalRadius = Config.baseRadius + distortion;
            double plateauRadius = 25.0;

            if (dist > totalRadius) return 0f;

            // --- PLATEAU MATH ---
            float richness = (float)(Config.rarityThreshold + (rand.NextDouble() * (1 - Config.rarityThreshold)));

            if (dist <= plateauRadius) return richness;

            double divisor = totalRadius - plateauRadius;
            if (divisor <= 0) return richness;

            // Falloff only happens AFTER the plateau
            float distFactor = (float)(1.0 - ((dist - plateauRadius) / divisor));
            distFactor = (float)Math.Pow(MathHelper.Clamp(distFactor, 0, 1), 0.5);

            return MathHelper.Clamp(distFactor * richness, 0f, 1f);
        }
    }
}