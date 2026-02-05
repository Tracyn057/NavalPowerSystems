using NavalPowerSystems.Communication;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace NavalPowerSystems.Extraction
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OxygenGenerator), false, "NPSExtractorOilDerrick")]
    public class DerrickLogic : MyGameLogicComponent
    {
        internal static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;

        private IMyFunctionalBlock _derrick;
        private IMyFunctionalBlock _drillHead;
        private int _assemblyId = -1;
        private string _status = "Idle";
        private string _location = "Void";
        private float _extractionRate = 0f;
        private string _itemSubtype = "Crude";
        private bool _hasDrillHead = false;
        private bool _hasDrillRig = false;
        private bool _hasDrillRod = false;
        private bool _isComplete = false;

        public bool _needsRefresh { get; set; }
        public bool _isDebug { get; set; }
        public bool _isDebugOcean { get; set; }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _derrick = Entity as IMyFunctionalBlock;
            if (_derrick == null) return;
            _needsRefresh = true;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }


        public override void UpdateOnceBeforeFrame()
        {
            _assemblyId = ModularApi.GetContainingAssembly(_derrick, "Extraction_Definition");
            _derrick.AppendingCustomInfo += AppendCustomInfo;

            if (_derrick != null) ValidateRig();

            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateBeforeSimulation100()
        {
            if (!MyAPIGateway.Session.IsServer) return;

            if (_needsRefresh)
            {
                ValidateRig();
            }

            if (_derrick.IsWorking)
            {
                UpdateExtract();
            }
            _derrick.RefreshCustomInfo();
        }

        private void ValidateRig()
        {
            _isComplete = false;
            if (_assemblyId != -1)
            {
                _hasDrillHead = false;
                _hasDrillRod = false;
                _hasDrillRig = false;

                foreach (IMyCubeBlock block in ModularApi.GetMemberParts(_assemblyId))
                {
                    if (block == null) return;

                    var subtype = block.BlockDefinition.SubtypeName;
                    if (subtype == "NPSExtractionDrillHead")
                    {
                        _hasDrillHead = true;
                        _drillHead = block as IMyFunctionalBlock;
                    }
                    if (subtype == "NPSExtractionDrillPipe") _hasDrillRod = true;
                    if (subtype == "NPSExtractorOilDerrick") _hasDrillRig = true;
                }
                if (_hasDrillHead == true && _hasDrillRod == true && _hasDrillRig == true) _isComplete = true;
            }
        }

        private void UpdateExtract()
        {
            if (_isComplete)
            {
                var inventory = _derrick.GetInventory();
                var logic = _drillHead.GameLogic?.GetAs<DrillHeadLogic>();

                if (inventory == null || logic == null) return;

                if (inventory.CurrentVolume >= inventory.MaxVolume * 0.95f)
                {
                    _status = "Inventory Full";
                    _extractionRate = 0;
                    return;
                }

                var oilItem = new MyDefinitionId(typeof(MyObjectBuilder_Ore), _itemSubtype);

                float baseRate = (Config.derrickExtractRate * 1.6f) * logic._oilYield;
                float oceanRate = baseRate * Config.derrickOceanMult;
                
                VRage.MyFixedPoint count = (VRage.MyFixedPoint)baseRate;
                VRage.MyFixedPoint countOcean = (VRage.MyFixedPoint)oceanRate;

                if (_isDebug)
                {
                    _status = "Extracting Oil (Debug)";
                    _extractionRate = (float)count;
                    inventory.AddItems(count, (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(oilItem));
                }
                else if (_isDebugOcean)
                {
                    _status = "Extracting Oil (Debug Ocean)";
                    _extractionRate = (float)countOcean;
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
                    inventory.AddItems(count, (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(oilItem));
                    _extractionRate = (float)count;
                    _status = "Extracting Oil";
                    _location = "On Land";
                }
                else if (logic._isAtGround && logic._isUnderwater)
                {
                    inventory.AddItems(countOcean, (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(oilItem));
                    _extractionRate = (float)countOcean;
                    _status = "Extracting Oil";
                    _location = "At Sea";
                }
            }
            else
            {
                if (!_hasDrillRig)
                    _status = "Assembly Incomplete (Missing Rig, wtf?)";
                else if (!_hasDrillHead)
                    _status = "Assembly Incomplete (Missing Drill Head)";
                else if (!_hasDrillRod)
                    _status = "Assembly Incomplete (Missing Drill Rod)";
                _location = "Irrelevant";
                _extractionRate = 0;
            }
        }

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            sb.AppendLine($"Status: {_status}");
            sb.AppendLine($"Location: {_location}");
            sb.AppendLine($"Extraction Rate: {(_extractionRate + " l/s")}");
        }

        public override void OnRemovedFromScene()
        {
            if (_derrick!= null) _derrick.AppendingCustomInfo -= AppendCustomInfo;
        }

    }

}
