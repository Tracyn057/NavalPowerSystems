using System.Collections.Generic;
using System.Linq;
using static NavalPowerSystems.Config;

namespace NavalPowerSystems
{
    public static class Config
    {
        //Global variables
        public const float globalFuelMult = 1.75f;          //Multiplier for fuel consumption
        public const bool requiresMaintenance = false;    //Whether or not to apply wear and tear to engines and propellers, causing them to lose efficiency and eventually fail without repairs
        public const float cavitationDmgMult = 0.1f;      //Multiplier for damage caused by cavitation, applied to propeller blocks
        public const float throttleVariance = 0.015f;    //Amount of random variance in throttle response

        //Fuel refining variables
        public const float crudeFuelOilRatio = 0.75f;   //Ratio of conversion from crude oil to fuel oil
        public const float fuelOilDieselRatio = 0.66f;  //Ratio of conversion from fuel oil to diesel fuel
        public const float baseRefineRate = 100;         //Base rate in liters for oil cracker and refinery

        //Oil extraction variables
        public const double rarityThreshold = 0.8;      //How much of the available oil spawn locations are empty. 1.0 = No oil ever 
        public const float gridSize = 250;            //Size of grid to determine oil deposits
        public const double baseRadius = 150;           //Radius from center of grid for deposit size
        public const int derrickExtractRate = 160;       //Base liters per second extraction rate
        public const float derrickOceanMult = 3.5f;     //Multiplier for extraction rate for deep sea drill platforms
        public const float minWaterDepth = 25;            //Depth in m to utilize OceanMult
        public const int scanSize = 100;                //Grid size for the LCD scan component

        //Viable component lists
        public static readonly HashSet<string> EngineSubtypes = new HashSet<string>
        {
            "NPS_Turbine_MT7",
            "NPS_Turbine_LM2500",
            "NPS_Turbine_LM2500Plus",
            "NPS_Turbine_LM2500PlusG4",
            "NPS_Turbine_MT30"
        };

        public static readonly HashSet<string> MotorSubtypes = new HashSet<string>
        {
            
        };

        public static readonly HashSet<string> PropellerSubtypes = new HashSet<string>
        {
            "NPS_Propeller_4m3b",
            "NPS_Propeller_4m4b",
            "NPS_Propeller_4m5b"
        };

        public static readonly HashSet<string> DriveshaftSubtypes = new HashSet<string>
        {
            "NPS_Driveshaft",
            "NPS_Driveshaft_Marked",
            "NPS_Driveshaft_Long",
            "NPS_Driveshaft_Long_Marked",
            "NPS_Driveshaft_LinearGearbox1",
            "NPS_Driveshaft_LinearGearbox2",
            "NPS_Driveshaft_TubeSealEnclosed1",
            "NPS_Driveshaft_TubeSealSlope1",
            "NPS_Driveshaft_TubeSealSlope1Corner",
            "NPS_Driveshaft_TubeSealSlope2",
            "NPS_Driveshaft_TubeSealSlope2Corner",
            "NPS_Driveshaft_EndTubeV1"
        };

        public static readonly HashSet<string> GearboxSubtypes = new HashSet<string>
        {
            "NPS_Gearbox_MRG",
            "NPS_Gearbox_DoublePlanetary"
        };

        public static readonly HashSet<string> RudderSubtypes = new HashSet<string>
        {
            "NPS_Rudder_Small_CenteredV1",
            "NPS_Rudder_Small_OffsetLeftV1",
            "NPS_Rudder_Small_OffsetRightV1",
            "NPS_Rudder_Small_CenteredV2",
            "NPS_Rudder_Small_OffsetLeftV2",
            "NPS_Rudder_Small_OffsetRightV2"
        };

        public static readonly HashSet<string> TransformerSubtypes = new HashSet<string>
        {
            
        };

        public static readonly HashSet<string> SwitchboardSubtypes = new HashSet<string>
        {
            
        };

        public static readonly HashSet<string> VFDSubtypes = new HashSet<string>
        {
            
        };
    }

    
}
