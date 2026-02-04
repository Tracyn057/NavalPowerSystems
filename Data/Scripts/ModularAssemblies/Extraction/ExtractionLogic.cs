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
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OxygenGenerator), false, "NPSExtractorOilDerrick")]
    public class ExtractionLogic : MyGameLogicComponent
    {
        internal static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;

        private IMyFunctionalBlock _block;
        private IMyGasTank _outputTank;
        private IMyCubeBlock _rigBlock;
        private int _assemblyId = -1;
        private string _status = "Idle";
        private int _pipeCount = 0;
        private float _extractionRate = 0f;
        private bool _isDebug = false;
        private bool _isDebugOcean = false;

        private float _YieldMult;
        private bool _IsOcean;
        private bool _IsRig;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _block = Entity as IMyFunctionalBlock;
            _rigBlock = Entity as IMyCubeBlock;

            if (_block == null) return;

            _block.AppendingCustomInfo += AppendCustomInfo;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateAfterSimulation100()
        {
            if (!MyAPIGateway.Session.IsServer || _block == null || !_block.IsWorking) 
                return;

            _assemblyId = ModularApi.GetContainingAssembly(_rigBlock, "Extraction_Definition");
            if (_assemblyId == -1)
                _assemblyId = ModularApi.GetContainingAssembly(Entity as IMyCubeBlock, "Extraction_Definition");

            if (_assemblyId == -1)
            {
                SetIdle("No Assembly Found");
                Utilities.UpdatePowerConsumption(_block, false);
                return;
            }

            _YieldMult = ModularApi.GetAssemblyProperty<float>(_assemblyId, "OilYield");
            _IsOcean = ModularApi.GetAssemblyProperty<bool>(_assemblyId, "IsOcean");
            _IsRig = ModularApi.GetAssemblyProperty<bool>(_assemblyId, "IsRig");
            _isDebug = _block.CustomData.Trim().ToUpper().Contains("DEBUG");

            if (_isDebug)
            {
                _status = "DEBUG";
            }
            else
            {
                if (_YieldMult <= 0)
                {
                    SetIdle("No Oil Detected");
                    return;
                }
                if (!_IsRig)
                {
                    SetIdle("Incomplete Assembly");
                    return;
                }
            }

            ExtractionSystem system;
            if (ExtractionManager.ExtractionSystems.TryGetValue(_assemblyId, out system))
            {
                if (system.IsAssemblyComplete)
                    _outputTank = system.OutputTank;
                else
                {
                    _outputTank = null;
                    return;
                }
            }

            if (!_block.ResourceSink.IsPoweredByType(MyResourceDistributorComponent.ElectricityId))
            {
                SetIdle("Insufficient Power");
                return;
            }
            if (_outputTank == null) 
            { 
                SetIdle("No Storage Found"); 
                return; 
            }
            else if (_outputTank.FilledRatio >= 1.0f) 
            { 
                SetIdle("Storage Full"); 
                return; 
            }
            else
            {
                UpdateExtract();
                Utilities.ChangeTankLevel(_outputTank, (double)_extractionRate);
                Utilities.UpdatePowerConsumption(_block, true);
            }

            _block.RefreshCustomInfo();
        }

        private void UpdateExtract()
        {
            float baseRate = Config.derrickExtractRate > 0 ? Config.derrickExtractRate : 1.0f;
            float oceanMult = Config.derrickOceanMult > 0 ? Config.derrickOceanMult : 2.0f;
            _isDebug = _block.CustomData.Trim().ToUpper().Contains("DEBUG");
            _isDebugOcean = _block.CustomData.Trim().ToUpper().Contains("OCEAN");
            if (_isDebug)
            {
                _extractionRate = 100;
                _status = "Extracting OVERRIDE";
                return;
            }
            else if (_isDebug && _isDebugOcean)
            {
                _extractionRate = 100 * oceanMult;
                _status = "Extracting (Oceanic) OVERRIDE";
                return;
            }
            else if (_IsOcean)
            {
                _extractionRate = ((baseRate * _YieldMult) * 1.6f) * oceanMult;
                _status = "Extracting (Oceanic)";
            }
            else
            {
                _extractionRate = (baseRate * _YieldMult) * 1.6f;
                _status = "Extracting";
            }
        }

        private void SetIdle(string reason)
        {
            _status = reason;
            _extractionRate = 0;
            Utilities.UpdatePowerConsumption(_block, false);
            _block.RefreshCustomInfo();
        }

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            sb.AppendLine($"Status: {_status}");
            sb.AppendLine($"Extension Pipes: {_pipeCount}");
            sb.AppendLine($"Extraction Rate: {(_extractionRate  + " l/s")}");
        }

        public override void OnRemovedFromScene()
        {
            if (_block != null) _block.AppendingCustomInfo -= AppendCustomInfo;
        }

        public static bool IsHeadAtSurface(Vector3D headPos, MyPlanet planet)
        {
            if (planet == null) return false;

            Vector3D surfacePoint = planet.GetClosestSurfacePointGlobal(headPos);
            double surfaceDist = (surfacePoint - planet.WorldMatrix.Translation).Length();
            double headDist = (headPos - planet.WorldMatrix.Translation).Length();

            return headDist <= surfaceDist + 2.5;
        }
    }
}
