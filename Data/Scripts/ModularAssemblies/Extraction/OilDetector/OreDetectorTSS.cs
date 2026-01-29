using NavalPowerSystems.Extraction;
using Sandbox.Game.Entities;
using Sandbox.Game.GameSystems.TextSurfaceScripts;
using Sandbox.Game.GUI;
using Sandbox.ModAPI;
using System;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI;
using VRageMath;
using IMyTextSurface = Sandbox.ModAPI.Ingame.IMyTextSurface;

namespace OilExtraction.Detector
{
    [MyTextSurfaceScript("OilDetector", "Oil Heatmap Scanner")]
    public class OilDetectorTSS : MyTSSCommon
    {
        public override ScriptUpdate NeedsUpdate => ScriptUpdate.Update100;

        public class HeatmapData : MyEntityComponentBase
        {
            public float[,] Values = new float[5, 5];
            public override string ComponentTypeDebugString => "OilHeatmapData";
        }

        private HeatmapData _data;
        private IMyCubeBlock _block;
        private int _ticks = 0; // Custom tick counter

        public OilDetectorTSS(IMyTextSurface surface, IMyCubeBlock block, Vector2 size) : base(surface, block, size)
        {
            _block = block;
        }

        public override void Run()
        {
            _ticks++;

            // 1. LINKING LOGIC
            if (_data == null)
            {
                var core = OilDetectorCore.Instance;
                var termBlock = _block as IMyTerminalBlock;

                if (core?.DefExtensions != null && core.ModCtx != null && termBlock != null)
                {
                    // Register the type factory
                    core.DefExtensions.RegisterTSSDataComponent<OilDetectorTSS, HeatmapData>(core.ModCtx, () => new HeatmapData());

                    // Attempt to grab the instance for this block
                    _data = core.DefExtensions.GetTSSDataComponent<OilDetectorTSS>(termBlock) as HeatmapData;
                }

                // 2. FALLBACK: If API fails to link for ~5 seconds (Update100 * 3), use local data
                if (_data == null && _ticks > 3)
                {
                    _data = new HeatmapData();
                }

                if (_data == null)
                {
                    DrawMessage("Linking to Oil Network...");
                    return;
                }
            }

            // 3. SCANNING
            int gridSize = NavalPowerSystems.Config.scanSize;
            Vector3D myPos = _block.WorldMatrix.Translation;
            MyPlanet planet = MyGamePruningStructure.GetClosestPlanet(myPos);

            if (planet != null)
            {
                for (int x = 0; x < 5; x++)
                {
                    for (int z = 0; z < 5; z++)
                    {
                        Vector3D offset = new Vector3D((x - 2) * gridSize, 0, (z - 2) * gridSize);
                        _data.Values[x, z] = OilMap.GetOil(myPos + offset, planet);
                    }
                }
            }

            // 4. DRAWING
            using (MySpriteDrawFrame frame = Surface.DrawFrame())
            {
                // Solid Background
                frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", Surface.SurfaceSize / 2, Surface.SurfaceSize, Color.Black));

                if (planet == null)
                {
                    var msg = MySprite.CreateText("No Planet Detected", "White", Color.Red, 0.8f);
                    msg.Position = Surface.SurfaceSize / 2;
                    frame.Add(msg);
                }
                else
                {
                    DrawHeatmapUI(frame);
                }
            }
        }

        private void DrawHeatmapUI(MySpriteDrawFrame frame)
        {
            Vector2 size = Surface.SurfaceSize;
            float cellSize = size.X / 5.5f;
            Vector2 center = size / 2;

            for (int x = 0; x < 5; x++)
            {
                for (int z = 0; z < 5; z++)
                {
                    float yield = _data.Values[x, z];
                    Vector2 screenPos = center + new Vector2((x - 2) * cellSize, (z - 2) * cellSize);

                    // Dark Gray to Gold
                    Color color = Color.Lerp(new Color(20, 20, 20), Color.Gold, yield);
                    frame.Add(new MySprite(SpriteType.TEXTURE, "SquareSimple", screenPos, new Vector2(cellSize * 0.9f), color));
                }
            }
            // Player Crosshair
            frame.Add(new MySprite(SpriteType.TEXTURE, "Circle", center, new Vector2(10), Color.White));
        }

        private void DrawMessage(string text)
        {
            using (var frame = Surface.DrawFrame())
            {
                var msg = MySprite.CreateText(text, "White", Color.Yellow, 0.7f);
                msg.Position = Surface.SurfaceSize / 2;
                frame.Add(msg);
            }
        }
    }
}