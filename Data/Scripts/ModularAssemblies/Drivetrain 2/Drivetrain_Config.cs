using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NavalPowerSystems.Drivetrain_V2
{
    public static class Drivetrain_Config
    {
        // Torque Load = WaterDensity * Diameter^5 * PitchRatio * (RPM/60)^2
        public const double PitchRatio = 1.1; // Ratio of propeller pitch to diameter, used for calculating advance speed and efficiency
        public const float GlobalFuelMult = 0.66f; //Multiplier for fuel consumption
        public const double DieselEnergyDensity = 38295; //KJ/Liter for diesel fuel, used for calculating fuel consumption
        private const double DriveshaftDensity_Reference = 8000; //Informationl only. Density of 316L stainless steel in kg/m^3, used for calculating driveshaft inertia based on length and diameter. Not directly used in code as of 2.0, but useful for reference when creating new shaft blocks with different materials or dimensions.
        private const double PropellerDensity_Reference = 7640; //Informational only. Density of NIBRAL for calculating propeller mass and inertia
        public const double DriveshaftInertiaPerBlock = 92.5; //Inertia of driveshafts per 2.5m section, used for calculating response time
        public const double WaterDensity = 1024; //Density of water in kg/m^3. 1024 is used to account for seawater. Same value as Water Mod

        public static readonly Dictionary<string, EngineStats_V2> EngineSettings_V2 = new Dictionary<string, EngineStats_V2>
        {
            //Gas Turbines
            {"NPS_Turbine_MT7", new EngineStats_V2 { PeakRPM = 12500, PeakTorque = 2160, HeatRate = 13846, PowerCurveConstant = 0.45f, EngineInertia = 250 } },
            {"NPS_Turbine_LM2500", new EngineStats_V2 { PeakRPM = 3300, PeakTorque = 66459, HeatRate = 9705, PowerCurveConstant = 0.35f, EngineInertia = 425 } },
            {"NPS_Turbine_LM2500Plus", new EngineStats_V2 { PeakRPM = 3300, PeakTorque = 80109, HeatRate = 9227, PowerCurveConstant = 0.325f, EngineInertia = 425 } },
            {"NPS_Turbine_LM2500PlusG4", new EngineStats_V2 { PeakRPM = 3300, PeakTorque = 93698, HeatRate = 9150, PowerCurveConstant = 0.315f, EngineInertia = 425 } },
            {"NPS_Turbine_MT30", new EngineStats_V2 { PeakRPM = 3300, PeakTorque = 106101, HeatRate = 9000, PowerCurveConstant = 0.3f, EngineInertia = 350 } },
        
            //Internal Combustion Diesel
        };

        public static readonly Dictionary<string, PropellerStats_V2> PropellerSettings_V2 = new Dictionary<string, PropellerStats_V2>
        {
            {"NPS_Propeller_4m3b_CW", new PropellerStats_V2 { Diameter = 4f, Inertia = 12890, TorqueCoefficient = 0.03, ThrustCoefficient = 0.15, IsCRP = false, IsCCW = false } },
            {"NPS_Propeller_4m4b_CW", new PropellerStats_V2 { Diameter = 4f, Inertia = 2000, TorqueCoefficient = 0.04, ThrustCoefficient = 0.175, IsCRP = false, IsCCW = false } },
            {"NPS_Propeller_4m5b_CW", new PropellerStats_V2 { Diameter = 4f, Inertia = 5000, TorqueCoefficient = 0.05, ThrustCoefficient = 0.2, IsCRP = false, IsCCW = false } },
            {"NPS_Propeller_4m3b_CCW", new PropellerStats_V2 { Diameter = 4f, Inertia = 12890, TorqueCoefficient = 0.03, ThrustCoefficient = 0.15, IsCRP = false, IsCCW = true } },
            {"NPS_Propeller_4m4b_CCW", new PropellerStats_V2 { Diameter = 4f, Inertia = 2000, TorqueCoefficient = 0.04, ThrustCoefficient = 0.175, IsCRP = false, IsCCW = true } },
            {"NPS_Propeller_4m5b_CCW", new PropellerStats_V2 { Diameter = 4f, Inertia = 5000, TorqueCoefficient = 0.05, ThrustCoefficient = 0.2, IsCRP = false, IsCCW = true } },

        };

        public static readonly Dictionary<string, GearboxStats_V2> GearboxSettings_V2 = new Dictionary<string, GearboxStats_V2>
        {
            {"NPS_Gearbox_MRG", new GearboxStats_V2 { GearRatio = 10f, HasShaftBrake = true, MaxBrakeTorque = 2000000 } },
            {"NPS_Gearbox_DoublePlanetary", new GearboxStats_V2 { GearRatio = 10f, HasShaftBrake = true, MaxBrakeTorque = 750000 } },
        };

        public static readonly Dictionary<string, ShaftStats_V2> ShaftSettings_V2 = new Dictionary<string, ShaftStats_V2>
        {
            {"NPS_Driveshaft", new ShaftStats_V2 { BlockLength = 1 } },
            {"NPS_Driveshaft_Marked", new ShaftStats_V2 { BlockLength = 1 } },
            {"NPS_Driveshaft_Long", new ShaftStats_V2 { BlockLength = 2 } },
            {"NPS_Driveshaft_Long_Marked", new ShaftStats_V2 { BlockLength = 2 } },
            {"NPS_Driveshaft_LinearGearbox1", new ShaftStats_V2 { BlockLength = 2 } },
            {"NPS_Driveshaft_LinearGearbox2", new ShaftStats_V2 { BlockLength = 3 } },
            {"NPS_Driveshaft_TubeSealEnclosed1", new ShaftStats_V2 { BlockLength = 1 } },
            {"NPS_Driveshaft_TubeSealSlope1", new ShaftStats_V2 { BlockLength = 1 } },
            {"NPS_Driveshaft_TubeSealSlope1Corner", new ShaftStats_V2 { BlockLength = 2 } },
            {"NPS_Driveshaft_TubeSealSlope2", new ShaftStats_V2 { BlockLength = 2 } },
            {"NPS_Driveshaft_TubeSealSlope2Corner", new ShaftStats_V2 { BlockLength = 4 } },
            {"NPS_Driveshaft_EndTubeV1", new ShaftStats_V2 { BlockLength = 2 } },
        };

        public static readonly Dictionary<string, RudderStats_V2> RudderSettings_V2 = new Dictionary<string, RudderStats_V2>
        {
            {"NPS_Rudder_Small_CenteredV1", new RudderStats_V2 { SufaceArea = 38.9f } },
            {"NPS_Rudder_Small_OffsetLeftV1", new RudderStats_V2 { SufaceArea = 38.9f } },
            {"NPS_Rudder_Small_OffsetRightV1", new RudderStats_V2 { SufaceArea = 38.9f } },
            {"NPS_Rudder_Small_CenteredV2", new RudderStats_V2 { SufaceArea = 37.32f } },
            {"NPS_Rudder_Small_OffsetLeftV2", new RudderStats_V2 { SufaceArea = 37.32f } },
            {"NPS_Rudder_Small_OffsetRightV2", new RudderStats_V2 { SufaceArea = 37.32f } },
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
        public double EngineInertia; //Inertia of the engine system, affecting how quickly it responds to changes in load and throttle
    }

    public class PropellerStats_V2
    {
        public float Diameter; // Diameter in meters
        public double Inertia; // 0.5 * mass * (radius^2). Used in response time
        public double TorqueCoefficient; // Coefficient for calculating torque based on advance speed. 0.02-0.05
        public double ThrustCoefficient; // Coefficient for calculating thrust based on advance speed. 0.15-0.25
        public bool IsCRP; // Whether the propeller is a controllable pitch propeller, which affects how torque and thrust are calculated
        public bool IsCCW; // Whether the propeller rotates counterclockwise, used for visual effects and potential future logic
    }

    public class GearboxStats_V2
    {
        public float GearRatio; //Obvious
        public bool HasShaftBrake; // Whether the gearbox has a shaft brake, which can hold the output shaft stationary when the engine is running. Affects logic for applying brake force and calculating response.
        public double MaxBrakeTorque; // Maximum torque the shaft brake can hold, used for calculating how much braking force to apply based on engine output and current load. Should be set based on the strength of the brake components and the expected loads in the system.
    }

    public class ShaftStats_V2
    {
        public double BlockLength; // Length of the shaft section in meters
    }

    public class RudderStats_V2
    {
        public float SufaceArea; // Surface area of the rudder in square meters, used for calculating thrust and torque
    }
}
