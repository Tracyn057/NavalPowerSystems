using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NavalPowerSystems.Drivetrain_2
{
    public static class Drivetrain_Config
    {

        public static readonly Dictionary<string, EngineStats_V2> NewEngineSettings = new Dictionary<string, EngineStats_V2>
        {
            //Gas Turbines
            {"NPS_Turbine_MT7", new EngineStats_V2 { PeakRPM = 12500, PeakTorque = 2160, HeatRate = 13846, PowerCurveConstant = 0.45f, SystemInertia = 250 } },
            {"NPS_Turbine_LM2500", new EngineStats_V2 { PeakRPM = 3300, PeakTorque = 66459, HeatRate = 9705, PowerCurveConstant = 0.35f, SystemInertia = 425 } },
            {"NPS_Turbine_LM2500Plus", new EngineStats_V2 { PeakRPM = 3300, PeakTorque = 80109, HeatRate = 9227, PowerCurveConstant = 0.325f, SystemInertia = 425 } },
            {"NPS_Turbine_LM2500PlusG4", new EngineStats_V2 { PeakRPM = 3300, PeakTorque = 93698, HeatRate = 9150, PowerCurveConstant = 0.315f, SystemInertia = 425 } },
            {"NPS_Turbine_MT30", new EngineStats_V2 { PeakRPM = 3300, PeakTorque = 106101, HeatRate = 9000, PowerCurveConstant = 0.3f, SystemInertia = 350 } },
        
            //Internal Combustion Diesel
        };

        public static readonly Dictionary<string, PropellerStats_V2> NewPropellerSettings = new Dictionary<string, PropellerStats_V2>
        {
            {"NPS_Propeller_4m3b", new PropellerStats_V2 { Diameter = 4f, Mass = 500 } },
            {"NPS_Propeller_4m4b", new PropellerStats_V2 { Diameter = 4f, Mass = 2000 } },
            {"NPS_Propeller_4m5b", new PropellerStats_V2 { Diameter = 4f, Mass = 5000 } },
        };

        public static readonly Dictionary<string, GearboxStats_V2> NewGearboxSettings = new Dictionary<string, GearboxStats_V2>
        {
            {"NPS_Gearbox_MRG", new GearboxStats_V2 { GearRatio = 10f } },
            {"NPS_Gearbox_DoublePlanetary", new GearboxStats_V2 { GearRatio = 10f } },
        };

    }

    public class EngineStats_V2
    {
        // Torque Output in Nm = PeakTorque * ( 1 - PowerCurveConstant * ((CurrentRPM - PeakRPM) / PeakRPM)^2)
        // Power Output in Watts = CurrentTorque * CurrentRPM / 9.5488
        // Power Required in KJ/sec = PowerKw * HeatRate / 3600
        // Fuel use in Liters/sec = (PowerRequired / DieselEnergyDensity) * globalFuelMult
        public double PeakRPM; //Output shaft max RPM
        public double PeakTorque; //Peak torque in Nm
        public double HeatRate; //KJ per kWh at peak power
        public float PowerCurveConstant; //Constant to shape the power curve, higher values make it more peaky
        public double SystemInertia; //Inertia of the engine system, affecting how quickly it responds to changes in load and throttle
    }

    public class PropellerStats_V2
    {
        public float Diameter; // Diameter in meters
        public double Mass; // Mass in kg, used for calculating inertia and response to load changes
    }

    public class GearboxStats_V2
    {
        public float GearRatio;
    }
}
