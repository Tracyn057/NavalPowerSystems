using Sandbox.Game.Entities;
using Sandbox.ModAPI;
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
        private bool _needsRefresh = true;
        private Vector3D _headPos;
        private float? _headDepth;
        private MyPlanet _planet;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            _drillHead = (IMyTerminalBlock)Entity;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (_drillHead != null)
            {
                GetLocation();
                GetYield();
                IsHeadAtSurface();
                GetWaterDepth();

                _drillHead.AppendingCustomInfo += AppendCustomInfo;
            }

            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateBeforeSimulation100()
        {
            if (_needsRefresh)
            {
                GetLocation();
                GetYield();
                IsHeadAtSurface();
                GetWaterDepth();
                _drillHead.RefreshCustomInfo();
            }
        }

        private void GetLocation()
        {
            _headPos = _drillHead.WorldMatrix.Translation;
            _planet = MyGamePruningStructure.GetClosestPlanet(_headPos);
        }

        private void GetWaterDepth()
        {
            _headDepth = Jakaria.API.WaterModAPI.GetDepth(_headPos);
            if (_headDepth == null)
            {
                _isUnderwater = false;
                return;
            }
            if (_headDepth > Config.minWaterDepth)
            {
                _isUnderwater = true;
                return;
            }
            _isUnderwater = false;
            return;
        }

        private void IsHeadAtSurface()
        {

            if (_planet == null) return;
            Vector3D surfacePoint = _planet.GetClosestSurfacePointGlobal(_headPos);
            double surfaceDist = Vector3D.Distance(_headPos, surfacePoint);

            if (surfaceDist <= 2.5)
            {
                _isAtGround = true;
                return;
            }
            _isAtGround = false;
            return;
        }

        private void GetYield()
        {
            if (_planet == null || _headPos == null) return;
            _oilYield = OilMap.GetOil(_headPos, _planet);
        }

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            if (_isAtGround)
            {
                sb.AppendLine("Location: Land");
            }
            else if (_isUnderwater)
            {
                sb.AppendLine("Status: Underwater");
                sb.AppendLine($"Depth: {_headDepth}:F2");
            }
        }

        public override void OnRemovedFromScene()
        {
            if (_drillHead != null) _drillHead.AppendingCustomInfo -= AppendCustomInfo;
        }

    }
}
