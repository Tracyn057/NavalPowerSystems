using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Utils;
using VRageMath;
using Sandbox.Game.Entities;

namespace NavalPowerSystems.Extraction
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class OilDebugDrawer : MySessionComponentBase
    {
        private bool _drawDebug = false;
        private List<DebugPoint> _foundPoints = new List<DebugPoint>();
        private int _scanTimer = 0;
        private readonly MyStringId _material = MyStringId.GetOrCompute("Laser");

        private struct DebugPoint
        {
            public Vector3D Position;
            public Vector3D Up;
            public Vector4 Color;
        }

        public override void BeforeStart()
        {
            MyAPIGateway.Utilities.MessageEntered += OnMessageEntered;
        }

        private void OnMessageEntered(string messageText, ref bool sendToOthers)
        {
            if (messageText.Trim().Equals("/oildebug", StringComparison.OrdinalIgnoreCase))
            {
                _drawDebug = !_drawDebug;
                sendToOthers = false;
                MyAPIGateway.Utilities.ShowNotification($"Oil Debug: {(_drawDebug ? "ENABLED" : "DISABLED")}", 2000, "White");
            }
        }

        public override void UpdateAfterSimulation()
        {
            if (!_drawDebug || MyAPIGateway.Session?.Player?.Character == null) return;

            if (_scanTimer++ % 30 == 0) // Scan twice per second
            {
                PerformScan();
            }
        }

        private void PerformScan()
        {
            _foundPoints.Clear();

            // 1. Get the controlled entity (the ship you are in)
            var controlledEntity = MyAPIGateway.Session.Player.Controller?.ControlledEntity?.Entity;
            if (controlledEntity == null) return;

            // 2. Use the Ship/Grid orientation, NOT the character's head/body
            MatrixD gridMatrix = controlledEntity.WorldMatrix;
            Vector3D originPos = gridMatrix.Translation;

            MyPlanet planet = MyGamePruningStructure.GetClosestPlanet(originPos);
            if (planet == null) return;

            Vector3D planetCenter = planet.PositionComp.GetPosition();

            // 3. Match the TSS exactly: gridSize * 3 (Config.gridSize from your source)
            float scanRange = NavalPowerSystems.Config.gridSize * 3f;
            int steps = 20;

            for (int x = 0; x < steps; x++)
            {
                for (int z = 0; z < steps; z++)
                {
                    double offsetX = ((x / (double)(steps - 1)) - 0.5) * scanRange;
                    double offsetZ = ((z / (double)(steps - 1)) - 0.5) * scanRange;

                    // This now aligns perfectly with the TSS "block.WorldMatrix.Forward/Right" math
                    Vector3D checkPos = originPos + (gridMatrix.Right * offsetX) + (gridMatrix.Forward * offsetZ);
                    Vector3D surfacePos = planet.GetClosestSurfacePointGlobal(ref checkPos);

                    float yield = OilMap.GetOil(surfacePos, planet);
                    if (yield <= 0.01f) continue;

                    _foundPoints.Add(new DebugPoint
                    {
                        Position = surfacePos,
                        Up = Vector3D.Normalize(surfacePos - planetCenter),
                        Color = GetDebugColor(yield)
                    });
                }
            }
        }

        public override void Draw()
        {
            if (!_drawDebug || _foundPoints.Count == 0) return;

            foreach (var point in _foundPoints)
            {
                Vector3D start = point.Position;
                Vector3D end = point.Position + (point.Up * 100.0); // 100m tall for high visibility
                Vector4 color = point.Color;

                // Thick pillars (thickness 2.0f)
                MyTransparentGeometry.AddLineBillboard(_material, color, start, (Vector3)point.Up, 100f, 2.0f);
            }
        }

        private Vector4 GetDebugColor(float v)
        {
            if (v < 0.4f) return new Vector4(0, 0, 1, 1);
            if (v < 0.7f) return new Vector4(0, 1, 0, 1);
            if (v < 0.9f) return new Vector4(1, 1, 0, 1);
            return new Vector4(1, 0, 0, 1);
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.MessageEntered -= OnMessageEntered;
        }
    }
}