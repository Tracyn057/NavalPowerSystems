using NavalPowerSystems.Communication;
using NavalPowerSystems.Utilities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using static NavalPowerSystems.Config;

namespace NavalPowerSystems.DieselEngines
{
    public class EngineChild
    {
        public IMyGasTank _engine;
        public EngineStats _engineStats;
        public EfficiencyPoint[] _engineEfficiency;

        public double CurrentThrottle = 0.0;

        public EngineChild(IMyGasTank engine, EngineStats stats, EfficiencyPoint[] efficiency)
        {
            _engine = engine;
            _engineStats = stats;
            _engineEfficiency = efficiency;
            
            CurrentThrottle = 0;
        }

        public void Spool(double target)
        {
            if (Math.Abs(CurrentThrottle - target) < _engineStats.SpoolRate)
                CurrentThrottle = target;
            else
                CurrentThrottle += (CurrentThrottle < target) ? _engineStats.SpoolRate : -_engineStats.SpoolRate;
        }

        
    }

    public class EngineLogic
    {
        internal static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        private List<EngineChild> _engines = new List<EngineChild>();
        public int _assemblyId = -1;

        public EngineLogic(int id)
        {
            _assemblyId = id;
        }

        public void AddEngine(IMyGasTank block, EngineStats stats, EfficiencyPoint[] table)
        {
            block.Stockpile = true;
            _engines.Add(new EngineChild(block, stats, table));
        }

        public void RemoveEngine(IMyCubeBlock block)
        {
            _engines.RemoveAll(e => e._engine.EntityId == block.EntityId);
        }

        public void Update10(float targetThrottle)
        {
            double totalOutputMW = 0;
            double totalFuelNeeded = 0;

            foreach (var engine in _engines)
            {
                var block = engine._engine as IMyFunctionalBlock;
                if (block== null || !block.IsWorking)
                {
                    engine.CurrentThrottle = 0;
                    continue;
                }

                engine.Spool(targetThrottle);

                float fuelMult = GetFuelMultiplier(engine._engineEfficiency, (float)engine.CurrentThrottle);
                double fuelBurn = (engine._engineStats.FuelRate * fuelMult) / 6;
                totalFuelNeeded += fuelBurn;

                CommonUtilities.ChangeTankLevel(engine._engine, -fuelBurn);

                if (Math.Abs(engine.CurrentThrottle - targetThrottle) < 0.01)
                {
                    totalOutputMW += engine._engineStats.MaxMW * engine.CurrentThrottle;
                }
            }

            ModularApi.SetAssemblyProperty(_assemblyId, "TotalAvailableMW", totalOutputMW);
            ModularApi.SetAssemblyProperty(_assemblyId, "TotalFuelNeeded", totalFuelNeeded);
            ModularApi.SetAssemblyProperty(_assemblyId, "SystemThrottle", targetThrottle);


        }

        public static float GetFuelMultiplier(EfficiencyPoint[] table, float currentThrottle)
        {
            if (currentThrottle <= table[0].Throttle) return table[0].Multiplier;

            if (currentThrottle >= table[table.Length - 1].Throttle)
                return table[table.Length - 1].Multiplier;

            for (int i = 0; i < table.Length - 1; i++)
            {
                if (currentThrottle <= table[i + 1].Throttle)
                {
                    EfficiencyPoint start = table[i];
                    EfficiencyPoint end = table[i + 1];

                    float percentage = (currentThrottle - start.Throttle) / (end.Throttle - start.Throttle);

                    return start.Multiplier + (end.Multiplier - start.Multiplier) * percentage;
                }
            }
            return 1.0f;
        }
    }
}
