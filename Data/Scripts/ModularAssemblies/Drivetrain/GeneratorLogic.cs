


namespace NavalPowerSystems.Drivetrain
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), false,
            ""
    )]
    public class GeneratorLogic : MyGameLogicComponent
    {
        private IMyTerminalBlock _generator;
        private IMyPowerProducer _generatorPowerProducer;
        private IMyGasTank _linkedEngine = null;
        private NavalEngineLogicBase _linkedEngineLogic = null;
        private float _outputMW = 0f;
        private float _inputMW = 0f;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _generator = (IMyTerminalBlock)Entity;
            _generatorPowerProducer = (IMyPowerProducer)Entity;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            _generator.AppendingCustomInfo += AppendCustomInfo;

            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateAfterSimulation100()
        {
            if (_linkedEngine == null || _linkedEngine.MarkedForClose || _linkedEngineLogic == null)
            {
                CacheLinkedEngine();
                _generatorPowerProducer.MaxOutput = 0f;
            }
            else
            {
                if (_linkedEngineLogic.EngineState == CombustionEngineLogic.EngineState.Running)
                {
                    _inputMW = _linkedEngineLogic._currentOutputMW;
                    _linkedEngineLogic._isEngaged = true;
                    _outputMW = _inputMW * 0.94f;
                    _generatorPowerProducer.MaxOutput = _outputMW;
                    _linkedEngineLogic.RequestedThrottleSync.Value = 0.75f;
                }
                else
                {
                    _generatorPowerProducer.MaxOutput = 0f;
                    _outputMW = 0f;
                }
            }
        }

        private void CacheLinkedEngine()
        {
            var blocks = _generator.GetNeighbours();
            foreach (var block in blocks)
            {
                if (block is null)
                    continue;
                var subtype = block.BlockDefinition.SubtypeName;
                if (Config.EngineSubtypes.Contains(subtype))
                {
                    _linkedEngine = block as IMyGasTank;
                    _linkedEngineLogic = _linkedEngine.GameLogic.GetAs<NavalEngineLogicBase>();
                    _linkedEngineLogic._isLinkedToGenerator = true;
                    _linkedEngineLogic.RefreshCustomControls();
                    break;
                }
            }
        }  

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            sb.AppendLine($"Status: {_status}");
        }   

        public override void OnRemovedFromScene()
        {
            if (_linkedEngine != null)
            {
                _linkedEngineLogic._isLinkedToGenerator = false;
                _linkedEngineLogic.RefreshCustomControls();
            }
            if (_generator != null)
            {
                _generator.AppendingCustomInfo -= AppendCustomInfo;
            }
        }   
    }
}