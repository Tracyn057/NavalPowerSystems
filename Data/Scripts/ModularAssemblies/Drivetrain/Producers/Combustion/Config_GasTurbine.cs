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
            {"NPS_Turbine_LM2500", new GasTurbineSettings{MaxPowerWatts = 25000000, MaxFuelFlow = 1.89, MinFuelFlow = 0.08, MaxAccelRate = 500, IdleRPM_Ng = 4500, MaxRPM_Ng = 9500} },
            {"NPS_Turbine_LM2500Plus", new GasTurbineSettings{MaxPowerWatts = 30000000, MaxFuelFlow = 2.12, MinFuelFlow = 0.08, MaxAccelRate = 500, IdleRPM_Ng = 4500, MaxRPM_Ng = 9500} },
            {"NPS_Turbine_LM2500PlusG4", new GasTurbineSettings{MaxPowerWatts = 35000000, MaxFuelFlow = 2.47, MinFuelFlow = 0.08, MaxAccelRate = 500, IdleRPM_Ng = 4500, MaxRPM_Ng = 9500} },
        };
    }

    public class GasTurbineSettings
    {
        public double MaxPowerWatts;
        public double MaxFuelFlow; //Liters per second
        public double MinFuelFlow; //Liters per second
        public double MaxAccelRate; 
        public double MaxRPM_Ng; //Power Turbine Max RPM
        public double IdleRPM_Ng; //Gas Generator Max RPM

    }
}
