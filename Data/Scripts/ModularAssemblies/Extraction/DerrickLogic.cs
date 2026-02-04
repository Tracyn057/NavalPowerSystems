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
    public class DerrickLogic : MyGameLogicComponent
    {
      internal static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;

        private IMyFunctionalBlock _derrick;
        private IMyFunctionalBlock _drillHead;
        private long _derrickId = -1;
        private int _assemblyId = -1;
        private string _status = "Idle";
        private float _extractionRate = 0f;
        private string _itemSubtype = "Crude";
        private bool _isComplete = false;

        public bool _needsRefresh { get; set; }
        public bool _isDebug { get; set; }
        public bool _isDebugOcean { get; set; }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _derrick = Entity as IMyFunctionalBlock;
            _needsRefresh = true;
            if (_derrick == null) return;
            
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeNextFrame()
        {
            _assemblyId = GetContainingAssembly(_derrick, "Extraction_Definition");
            _derrick.AppendingCustomInfo += AppendCustomInfo;
            _derrickId = _derrick.FatBlock.EntityId;
            
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateBeforeSimulation100()
        {
          if (!MyAPIGateway.Session.IsServer) return;
          
          if (_needsRefresh) 
          {
            ValidateRig();
            continue;
          };

          if (_derrick.IsWorking)
          {
            UpdateExtract();
          }

        }

        private void ValidateRig()
        {
          if (_assemblyId != -1)
          {
            bool drillHead = false;
            bool drillRod = false;
            bool drillRig = false;
            
            foreach (IMyCubeBlock block in ModularApi.GetMemberParts(_assemblyId))
	          {
              if (block == null) return;
              
              var subtype = block.BlockDefinition.SubtypeName;
  		        if (subtype = "NPSExtractionDrillHead")
              {
                drillHead = true;
                _drillHead = block.Fatblock.EntityId;
              }
              if (subtype = "NPSExtractionDrillRod") drillRod = true;
              if (subtype = "NPSExtractorOilDerrick") drillRig = true;                
	          }
            if (drillHead = true && drillRod = true && drillRig = true) _isComplete = true;

            
          }
        }

        private void UpdateExtract()
        {
          var inventory = _derrick.GetInventory();
          if (inventory == null) return;

          var oilItem = new MyDefinitionId(typeof(MyObjectBuilder_Ore), _itemSubtype);
          int count = Config.derrickExtractRate;
          float countOcean = count * Config.derrickOceanMult;

          if (inventory.CurrentVolume < inventory.MaxVolume)
          {
              if (
            
              inventory.AddItems(count, (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(oilItem));
          }
        }

    }

}
