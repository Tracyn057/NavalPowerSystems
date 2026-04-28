using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NavalPowerSystems.Drivetrain.Producers.Combustion
{
    public class Config_GasTurbine
    {
        public static readonly Dictionary<string, GasTurbineSettings> GasTurbineStats = new Dictionary<string, GasTurbineSettings>
        {
            {"NPS_Turbine_LM2500", new GasTurbineSettings{
                MaxPowerWatts = 25060000, 
                MaxFuelFlow = 1.89, 
                MinFuelFlow = 0.115,
                ThermalEfficiency = 0.36, 
                IdleRPM_Ng = 4500, 
                MaxRPM_Ng = 10000,
                Kdrive = 0.23929,
                DampingCoefficient = 89.54,
                InternalFriction = 11252,
                TPR = 18 } },
            {"NPS_Turbine_LM2500Plus", new GasTurbineSettings{
                MaxPowerWatts = 30200000, 
                MaxFuelFlow = 2.12, 
                MinFuelFlow = 0.115,
                ThermalEfficiency = 0.38, 
                IdleRPM_Ng = 4500, 
                MaxRPM_Ng = 10000,
                Kdrive = 0.28837,
                DampingCoefficient = 89.54,
                InternalFriction = 11252,
                TPR = 23 } },
            {"NPS_Turbine_LM2500PlusG4", new GasTurbineSettings{
                MaxPowerWatts = 35320000, 
                MaxFuelFlow = 2.47, 
                MinFuelFlow = 0.115,
                ThermalEfficiency = 0.39, 
                IdleRPM_Ng = 4500, 
                MaxRPM_Ng = 10000,
                Kdrive = 0.33726,
                DampingCoefficient = 89.54,
                InternalFriction = 11252,
                TPR = 23.3 } },
        };
    }

    public class GasTurbineSettings
    {
        public double MaxPowerWatts;
        public double MaxFuelFlow; //Liters per second
        public double MinFuelFlow; //Liters per second
        public double ThermalEfficiency; 
        public double MaxRPM_Ng; //Power Turbine Max RPM
        public double IdleRPM_Ng; //Gas Generator Max RPM
        public double Kdrive;
        public double DampingCoefficient;
        public double InternalFriction;
        public double TPR;
    }
}
