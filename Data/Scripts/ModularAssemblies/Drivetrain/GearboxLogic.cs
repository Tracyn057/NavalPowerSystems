using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NavalPowerSystems.Drivetrain
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), false,
            "NPSDrivetrainMRG",
            "NPSDrivetrainDRG"
    )]
    internal class GearboxLogic : MyGameLogicComponent
    {

        internal static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        private int _assemblyId = -1;
        private IMyTerminalBlock _gearbox;
        private bool _isComplete;
        private bool _isReverse;
        private bool _needsRefresh = true;
        private bool _controlsInit = false;
        private int _inputCount;
        private int _outputCount;
        
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _gearbox = Entity as IMyFunctionalBlock;

            if (_gearbox == null) return;

            _gearbox.AppendingCustomInfo += AppendCustomInfo;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {

            

            NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
        }

        public override void UpdateBeforeSimulation10()
        {
            
            
            _gearbox.RefreshCustomInfo();
        }
    }
}
