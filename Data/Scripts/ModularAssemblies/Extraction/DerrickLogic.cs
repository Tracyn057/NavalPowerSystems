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
        private string _itemSubtype = "Crude";
        private bool _hasDrillHead = false;
        private bool _hasDrillRod = false;
        private bool _isComplete = false;

        public bool _needsRefresh { get; set; }
        public bool _isDebug { get; set; }
        public bool _isDebugOcean { get; set; }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }


        public override void UpdateOnceBeforeFrame()
        {
            _derrick = Entity as IMyFunctionalBlock;
            if (_derrick == null) return;
            
            _needsRefresh = true;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateBeforeSimulation100()
        {
            if (_needsRefresh)
            {
                ValidateRig();
                _assemblyId = ModularApi.GetContainingAssembly(_derrick, "Extraction_Definition");
            }
            else if (_derrick.IsWorking)
            {
                UpdateExtract();
            }
            else if (!_isComplete)
            {
                MyAPIGateway.Utilities.SendMessage($"Derrick Incomplete. Drill:{_hasDrillHead}, Rods:{_hasDrillRod}");
                if (!_hasDrillRod)
                    _status = "Assembly Incomplete (Missing Drill Rod)";
                else if (!_hasDrillHead)
                    _status = "Assembly Incomplete (Missing Drill Head)";
                _location = "-";
                _extractionRate = 0;
            }
            _derrick.AppendingCustomInfo += AppendCustomInfo;
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
                        _drillHead = system.DrillHead;
                    }

                    if (system.Pipes.Count > 0)
                    {
                        _hasDrillRod = true;
                        _drillrods.AddRange(system.Pipes);
                    }
                }

                _isComplete = _hasDrillHead && _hasDrillRod;
                _needsRefresh = false;
            }
        }

        private void UpdateExtract()
        {
                MyAPIGateway.Utilities.SendMessage("Derrick Operational");
                var inventory = _derrick.GetInventory();
                var logic = _drillHead.GameLogic?.GetAs<DrillHeadLogic>();

                if (inventory == null || logic == null) return;

                if (inventory.CurrentVolume >= inventory.MaxVolume * 0.95f)
                {
                    _status = "Inventory Full";
                    _extractionRate = 0;
                    return;
                }

                var oilItem = new MyDefinitionId(typeof(MyObjectBuilder_Ore), "CrudeDummy");
                var newItem = (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(oilItem);

                float baseRate = (Config.derrickExtractRate * 1.6f) * logic._oilYield;
                float oceanRate = baseRate * Config.derrickOceanMult;

                VRage.MyFixedPoint count = (VRage.MyFixedPoint)baseRate;
                VRage.MyFixedPoint countOcean = (VRage.MyFixedPoint)oceanRate;

                if (logic._oilYield > 0.25)
                {
                    if (_isDebug)
                    {
                        _status = "Extracting Oil (Debug)";
                        _extractionRate = (float)count;
                        if (MyAPIGateway.Session.IsServer || MyAPIGateway.Session.Player != null)
                        {
                            inventory.AddItems(count, newItem);
                            if (newItem == null)
                                MyAPIGateway.Utilities.ShowMessage("NPS Error", "Failed to create ObjectBuilder for item!");
                        }
                        else if (_isDebugOcean)
                        {
                            _status = "Extracting Oil (Debug Ocean)";
                            _extractionRate = (float)countOcean;
                            if (MyAPIGateway.Session.IsServer || MyAPIGateway.Session.Player != null)
                                inventory.AddItems(countOcean, (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(oilItem));
                        }
                        else if (!logic._isAtGround)
                        {
                            _status = "Searching for Ground";
                            _location = "In Air";
                            _extractionRate = 0;
                        }
                        else if (logic._isAtGround && !logic._isUnderwater)
                        {
                            _extractionRate = (float)count;
                            _status = "Extracting Oil";
                            _location = "On Land";
                            if (MyAPIGateway.Session.IsServer || MyAPIGateway.Session.Player != null)
                                inventory.AddItems(count, (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(oilItem));

                        }
                        else if (logic._isAtGround && logic._isUnderwater)
                        {
                            _extractionRate = (float)countOcean;
                            _status = "Extracting Oil";
                            _location = "At Sea";
                            if (MyAPIGateway.Session.IsServer || MyAPIGateway.Session.Player != null)
                                inventory.AddItems(countOcean, (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(oilItem));
                        }
                    }
                    else if (logic._oilYield <= 0.25)
                    {
                        if (_isDebug)
                        {
                            _status = "Extracting Oil (Debug)";
                            _extractionRate = (float)count;
                            if (MyAPIGateway.Session.IsServer || MyAPIGateway.Session.Player != null)
                                inventory.AddItems(count, (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(oilItem));
                        }
                        else if (_isDebugOcean)
                        {
                            _status = "Extracting Oil (Debug Ocean)";
                            _extractionRate = (float)countOcean;
                            if (MyAPIGateway.Session.IsServer || MyAPIGateway.Session.Player != null)
                                inventory.AddItems(countOcean, (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(oilItem));
                        }
                        else if (!logic._isAtGround)
                        {
                            _status = "Searching for Ground";
                            _location = "In Air";
                            _extractionRate = 0;
                        }
                        else if (logic._isAtGround && !logic._isUnderwater)
                        {
                            _extractionRate = 0;
                            _status = "No Oil Found";
                            _location = "On Land";

                        }
                        else if (logic._isAtGround && logic._isUnderwater)
                        {
                            _extractionRate = 0;
                            _status = "No Oil Found";
                            _location = "At Sea";
                        }
                    }
                    
            }
        }

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            sb.AppendLine($"Assembly ID: {_assemblyId}");
            sb.AppendLine($"Status: {_status}");
            sb.AppendLine($"Location: {_location}");
            sb.AppendLine($"Drill Rods: {_drillrods.Count}");
        }

        public override void OnRemovedFromScene()
        {
            if (_derrick!= null) _derrick.AppendingCustomInfo -= AppendCustomInfo;
        }

    }

}
