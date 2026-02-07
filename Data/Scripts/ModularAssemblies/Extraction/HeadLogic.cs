using Jakaria.API;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace NavalPowerSystems.Extraction
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), false, "NPSExtractionDrillHead")]
    public class DrillHeadLogic : MyGameLogicComponent
    {
        private IMyTerminalBlock _drillHead;
        public float _oilYield { get; private set; }
        public bool _isAtGround { get; private set; }
        public bool _isUnderwater { get; private set; }
        private Vector3D _headPos;
        private float _headDepth;
        private MyPlanet _planet;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            _drillHead = (IMyTerminalBlock)Entity;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (_drillHead == null) return;

            UpdateData();
            _drillHead.AppendingCustomInfo += AppendCustomInfo;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateBeforeSimulation100()
        {
            UpdateData();
            _drillHead.RefreshCustomInfo();
        }

        private void UpdateData()
        {
            // 1. Get exact Head Position
            _headPos = _drillHead.WorldMatrix.Translation;
            _planet = MyGamePruningStructure.GetClosestPlanet(_headPos);
            if (_planet == null) return;

            // 2. Check Physical Requirements
            Vector3D seafloorPoint = _planet.GetClosestSurfacePointGlobal(_headPos);
            double distToSeafloor = Vector3D.Distance(_headPos, seafloorPoint);
            _isAtGround = (distToSeafloor <= 3.0);

            Vector3D? waterPoint = WaterModAPI.GetClosestSurfacePoint(_headPos, _planet);
            if (waterPoint.HasValue)
            {
                double centerDist = Vector3D.Distance(_headPos, _planet.PositionComp.GetPosition());
                double waterDist = Vector3D.Distance(waterPoint.Value, _planet.PositionComp.GetPosition());
                _headDepth = (float)(waterDist - centerDist);
                _isUnderwater = (_headDepth > Config.minWaterDepth);
            }

            // 3. Get Map Data & Apply Gate
            float rawYield = OilMap.GetOil(_headPos, _planet);

            // Logic: If we are touching the seabed OR drilling on land, allow yield.
            float yield = OilMap.GetOil(_headPos, _planet);
            //_oilYield = (_isAtGround || _isUnderwater) ? yield : 0f;
            _oilYield = yield;
        }

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            if (_isUnderwater)
            {
                sb.AppendLine("Status: Submerged");
                sb.AppendLine($"Depth: {_headDepth:F2}m");
            }
            else if (_isAtGround)
            {
                sb.AppendLine("Location: On Seabed/Ground");
            }
            else
            {
                sb.AppendLine("Status: In Air/Water Column");
            }

            sb.AppendLine($"Oil Yield: {_oilYield:P2}");
        }

        public override void OnRemovedFromScene()
        {
            if (_drillHead != null) _drillHead.AppendingCustomInfo -= AppendCustomInfo;
        }
    }
}