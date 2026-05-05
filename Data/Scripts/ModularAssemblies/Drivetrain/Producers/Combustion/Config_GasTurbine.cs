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
                MaxFuelKgs = 1.66,
                MaxAirKgs = 70.5,
                PressureRatio = 24.3,
                CompressorEfficiency = 0.82,
                TurbineEfficiency = 0.86
            } },
            {"NPS_Turbine_LM2500Plus", new GasTurbineSettings{
                MaxFuelKgs = 1.847,
                MaxAirKgs = 85.9,
                PressureRatio = 27.3,
                CompressorEfficiency = 0.84,
                TurbineEfficiency = 0.87
            } },
            {"NPS_Turbine_LM2500PlusG4", new GasTurbineSettings{
                MaxFuelKgs = 2.149,
                MaxAirKgs = 93,
                PressureRatio = 29.3, 
                CompressorEfficiency = 0.84,
                TurbineEfficiency = 0.88
            } },
        };
    }

    public class GasTurbineSettings
    {
        public double MaxFuelKgs;
        public double MaxAirKgs;
        public double PressureRatio;
        public double CompressorEfficiency;
        public double TurbineEfficiency;
    }
}
