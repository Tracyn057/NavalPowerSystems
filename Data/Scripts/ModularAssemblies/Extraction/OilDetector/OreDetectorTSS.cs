using NavalPowerSystems;
using NavalPowerSystems.Extraction;
using Sandbox.Game.Entities;
using Sandbox.Game.GameSystems.TextSurfaceScripts;
using Sandbox.ModAPI;
using System;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI;
using VRageMath;
using IMyTextSurface = Sandbox.ModAPI.Ingame.IMyTextSurface;

namespace OilExtraction.Detector
{
    [MyTextSurfaceScript("OilDetector", "Oil Deposit Scanner")]
    public class OilDetectorTSS : MyTSSCommon
    {
        public override ScriptUpdate NeedsUpdate => ScriptUpdate.Update100;
        private IMyCubeBlock _block;
        private IMyCubeGrid _grid;
        private float[,] _viewData = new float[32, 32];

        public OilDetectorTSS(IMyTextSurface surface, IMyCubeBlock block, Vector2 size) : base(surface, block, size)
        {
            _block = block;
            _grid = block.CubeGrid;
        }

        public override void Run()
        {
            // 1. Center everything on the LCD block's physical position
            MatrixD myMatrix = _block.WorldMatrix;
            Vector3D myPos = _block.WorldMatrix.Translation;
            MyPlanet planet = MyGamePruningStructure.GetClosestPlanet(myPos);

            if (planet != null)
            {
                Vector3D planetCenter = planet.PositionComp.GetPosition();
                // 2. Use the Block's own orientation
                // This ensures 'Up' on the LCD is always 'Forward' for the block
                Vector3D forward = _block.WorldMatrix.Forward;
                Vector3D right = _block.WorldMatrix.Right;

                // 3. Scan a 500m area (2 grid sizes) to ensure we see the edges
                float scanRange = NavalPowerSystems.Config.gridSize * 3f;
                int steps = 20;

                for (int x = 0; x < steps; x++)
                {
                    for (int z = 0; z < steps; z++)
                    {
                        double offsetX = ((x / (double)(steps - 1)) - 0.5) * scanRange;
                        double offsetZ = ((z / (double)(steps - 1)) - 0.5) * scanRange;

                        // This now aligns perfectly with the TSS "block.WorldMatrix.Forward/Right" math
                        Vector3D checkPos = myPos + (myMatrix.Right * offsetX) + (myMatrix.Forward * offsetZ);
                        Vector3D surfacePos = planet.GetClosestSurfacePointGlobal(ref checkPos);

                        float yield = OilMap.GetOil(surfacePos, planet);
                        if (yield > 0.01f)
                        {
                            _viewData[x, z] = yield;
                        }
                        else
                        {
                            _viewData[x, z] = 0f;
                        }

                        _viewData[x, z] = yield;
                    }
                }
            }

            using (MySpriteDrawFrame frame = Surface.DrawFrame())
            {
                frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Surface.SurfaceSize / 2, Surface.SurfaceSize, Color.Black));
                if (planet != null) DrawHeatmapUI(frame);
            }
        }

        private void DrawHeatmapUI(MySpriteDrawFrame frame)
        {
            Vector2 size = Surface.SurfaceSize;
            int res = 20;
            float pixelSize = (size.X * 0.9f) / res; // Slightly larger for better visibility
            Vector2 offset = (size / 2) - new Vector2((pixelSize * res) / 2);

            for (int x = 0; x < res; x++)
            {
                for (int z = 0; z < res; z++)
                {
                    float yield = _viewData[x, z];
                    if (yield <= 0.01f) continue; // Match debug pillar threshold

                    Color c = GetHeatColor(yield);
                    Vector2 pos = offset + new Vector2(x * pixelSize, z * pixelSize) + (pixelSize / 2);

                    // Draw as sharp squares so you see the exact grid boundaries
                    frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", pos, new Vector2(pixelSize), c));
                }
            }
            // Self Marker (The white dot is the LCD block itself)
            frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", size / 2, new Vector2(8), Color.White));
        }

        private Color GetHeatColor(float v)
        {
            if (v < 0.3f) return Color.Lerp(Color.Blue * 0.3f, Color.Green, v * 3.3f);
            if (v < 0.7f) return Color.Lerp(Color.Green, Color.Yellow, (v - 0.3f) * 2.5f);
            return Color.Lerp(Color.Yellow, Color.Red, (v - 0.7f) * 3.3f);
        }
    }
}