using NavalPowerSystems.Common;
using NavalPowerSystems.Communication;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace NavalPowerSystems.Extraction
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OxygenGenerator), false, "NPSExtractionOilDerrick")]
    public class DerrickLogic : MyGameLogicComponent
    {
        internal static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;

        private IMyFunctionalBlock _derrick;
        private IMyTerminalBlock _drillHead;
        public List<IMySlimBlock> _drillrods = new List<IMySlimBlock>();
        private int _assemblyId = -1;
        private string _status = "Idle";
        private string _location = "Void";
        private float _extractionRate = 0f;
        private bool _hasDrillHead = false;
        private bool _hasDrillRod = false;
        private bool _isComplete = false;
        private bool _timer = false;

        public bool _needsRefresh { get; set; }
        public bool _isDebug { get; set; }
        public bool _isDebugOcean { get; set; }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _derrick = Entity as IMyFunctionalBlock;

            if (_derrick == null) return;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            _derrick.AppendingCustomInfo += AppendCustomInfo;
            _assemblyId = ModularApi.GetContainingAssembly(_derrick, "Extraction_Definition");

            _needsRefresh = true;

            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateBeforeSimulation100()
        {
            if (_needsRefresh)
            {
                ValidateRig();
                if (_isComplete) _needsRefresh = false;
            }
            else if (_derrick.IsWorking)
            {
                UpdateExtract();
            }
            else if (!_isComplete)
            {
                MyAPIGateway.Utilities.SendMessage($"Derrick Incomplete. Drill:{_hasDrillHead}, Rods:{_hasDrillRod}");
                if (!_hasDrillRod)
                    _status = "Missing Drill Rod";
                else if (!_hasDrillHead)
                    _status = "Missing Drill Head";
                _location = "-";
                _extractionRate = 0;
            }
            _timer = !_timer;
            _derrick.RefreshCustomInfo();
        }

        private void ValidateRig()
        {
            _isComplete = false;
            _hasDrillHead = false;
            _hasDrillRod = false;
            _drillrods.Clear();
            if (_assemblyId != -1)
            {
                ExtractionSystem system;
                if (ExtractionManager.ExtractionSystems.TryGetValue(_assemblyId, out system))
                {
                    if (system.DrillHead != null)
                    {
                        _hasDrillHead = true;
                        _drillHead = system.DrillHead.FatBlock as IMyTerminalBlock;
                    }

                    if (system.Pipes.Count > 0)
                    {
                        _hasDrillRod = true;
                        _drillrods.AddRange(system.Pipes);
                    }
                }

                _isComplete = _hasDrillHead && _hasDrillRod;
            }
        }

        private void UpdateExtract()
        {
            IMyInventory inventory = _derrick.GetInventory(0);
            var logic = _drillHead.GameLogic?.GetAs<DrillHeadLogic>();

            if (inventory == null || logic == null) return;

            if (inventory.CurrentVolume >= inventory.MaxVolume * 0.95f)
            {
                _status = "Inventory Full";
                _extractionRate = 0;
                return;
            }

            MyObjectBuilder_PhysicalObject oilItem = new MyObjectBuilder_PhysicalObject
            {
                TypeId = "MyObjectBuilder_Ore",
                SubtypeName = "DummyItemCrude"
            };
            float baseRate = Config.derrickExtractRate * 1.6f * logic._oilYield;
            float oceanRate = baseRate * Config.derrickOceanMult;

            VRage.MyFixedPoint count = (VRage.MyFixedPoint)baseRate;
            VRage.MyFixedPoint countOcean = (VRage.MyFixedPoint)oceanRate;

            if (_isDebug)
            {
                _status = "Extracting Oil (Debug)";
                _location = "On Land";
                _extractionRate = (float)count;

                Utilities.AddNewItem(inventory, oilItem, count);
            }
            else if (_isDebugOcean)
            {
                _status = "Extracting Oil (Debug)";
                _location = "At Sea";
                _extractionRate = (float)countOcean;

                Utilities.AddNewItem(inventory, oilItem, countOcean);
            }
            else if (logic._isAtGround)
            {
                if (logic._oilYield > 0.25)
                {
                    _status = "Extracting Oil";
                    if (!logic._isUnderwater)
                    {
                        _extractionRate = (float)count;
                        _location = "On Land";
                        
                        Utilities.AddNewItem(inventory, oilItem, count);
                    }
                    else
                    {
                        _extractionRate = (float)countOcean;
                        _location = "At Sea";
                        
                        Utilities.AddNewItem(inventory, oilItem, countOcean);
                    }
                }
                else if (logic._oilYield <= 0.25)
                {
                    _status = "No Oil Found";
                    _extractionRate = 0;
                    if (!logic._isUnderwater)
                    {
                        _location = "On Land";
                    }
                    else
                    {
                        _location = "At Sea";
                    }
                }
            }
            else if (!logic._isAtGround)
            {
                _status = "Searching for Ground";
                _location = "In Air";
                _extractionRate = 0;
            }
            else
            {
                _status = "Unknown";
                _location = "-";
                _extractionRate = 0;
            }
            
        }

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            var logic = _drillHead.GameLogic?.GetAs<DrillHeadLogic>();

            sb.AppendLine($"Assembly ID: {_assemblyId}");
            sb.AppendLine($"Status: {_status}");
            sb.AppendLine($"Location: {_location}");
            sb.AppendLine($"Drill Rods: {_drillrods.Count}");
            if (logic != null)
                sb.AppendLine($"Oil Quality: {logic._oilYield:P}");
            if (_timer)
                sb.AppendLine("||");
            else if (!_timer)
                sb.AppendLine("|");
        }

        public override void OnRemovedFromScene()
        {
            if (_derrick != null) _derrick.AppendingCustomInfo -= AppendCustomInfo;
        }

    }

}
