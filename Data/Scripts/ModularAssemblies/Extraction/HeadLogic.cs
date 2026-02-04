using Jakaria.API;
using NavalPowerSystems.Common;
using NavalPowerSystems.Communication;
using NavalPowerSystems.Production;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace NavalPowerSystems.Extraction
{
  [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), false, "NPSExtractionDrillHead")]
  public class HeadLogic : MyGameLogicComponent
  {
  
    private IMyTerminalBlock _drillHead;
    public float _oilYield { get; private set; }
    public bool _isAtGround { get; private set; }
    public bool _isUnderwater { get; private set; }
    private bool _needsRefresh = true;
    private Vector3D _headPos;
    private string _planet;
    
    public override void Init(MyObjectBuilder_EntityBase objectBuilder)
      {
          _engine = (IMyGasTank)Entity;
          _engineStats = Config.EngineSettings[_engine.BlockDefinition.SubtypeName];
          NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
      }

    public override void UpdateOnceBeforeNextFrame()
    {

    
      NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;  
    }

    public override void UpdateBeforeSimulation100()
    {

    }

    private void GetLocation()
    {
      _headPos = _drillHead.WorldMatrix.Translation;
      _planet = MyGamePruningStructure.GetClosestPlanet(_headPos)
    }

    private void GetWaterDepth()
        {
            var headDepth = WaterModAPI.GetDepth(_headPos)
            if (headDepth == null)
            {
              _isUnderwater = false;
                return;
              }
            if (headDepth > Config.minWaterDepth)
            {
              _inUnderwater = true;
                return;
                }
            _isUnderwater = false;
            return;
        }

    private void IsHeadAtSurface()
        {
          
            if (_planet == null) return false;
            float headDist = 10000;
            Vector3D surfacePoint = planet.GetClosestSurfacePointGlobal(_headPos);
            double surfaceDist = (surfacePoint - planet.WorldMatrix.Translation).Length();
            double headDist = (_headPos - planet.WorldMatrix.Translation).Length();

            if (headDist <= surfaceDist + 2.5)
            {
              _isAtGround = true;
              return;
            }
            _isAtGround = false;
            return;
        }

  }
}
