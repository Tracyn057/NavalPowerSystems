using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NavalPowerSystems.Drivetrain.Producers.Steam
{
    public class Config_Steam
    {
        public static readonly Dictionary<string, SteamTurbineStats> SteamTurbineSettings = new Dictionary<string, SteamTurbineStats>
        {
            {"NPSSteamTurbineDestroyerHP", new SteamTurbineStats { MinFlow = 0.05f, MaxFlow = 0.6f } },
            {"NPSSteamTurbineDestroyerLP", new SteamTurbineStats { MinFlow = 0.05f, MaxFlow = 0.6f } },
            {"NPSSteamTurbineCruiserHP", new SteamTurbineStats { MinFlow = 0.15f, MaxFlow = 1.5f } },
            {"NPSSteamTurbineCruiserLP", new SteamTurbineStats { MinFlow = 0.15f, MaxFlow = 1.5f } },
            {"NPSSteamTurbineCapitalHP", new SteamTurbineStats { MinFlow = 0.5f, MaxFlow = 5f } },
            {"NPSSteamTurbineCapitalLP", new SteamTurbineStats { MinFlow = 0.5f, MaxFlow = 5f } }
        };

        public static readonly Dictionary<string, BoilerStats> BoilerSettings = new Dictionary<string, BoilerStats>
        {
            {"NPSBoilerBnWExpress", new BoilerStats {
                OperatingBar = 41.4f,
                MaxBar = 46f,
                OperatingTemp = 727.6f,
                MaxMassFlow = 7f,
                MaxFuelFlow = 1.03f,
                ThermalMass = 120000f,
                WaterCapacity = 1006f,
                DrumVolume = 1f } },
            {"NPSBoilerAdmiralty", new BoilerStats {
                OperatingBar = 20.7f,
                MaxBar = 24f,
                OperatingTemp = 588.7f,
                MaxMassFlow = 6.2f,
                MaxFuelFlow = 0.85f,
                ThermalMass = 150000f,
                WaterCapacity = 772f,
                DrumVolume = 3 } },
            {"NPSBoilerWagner", new BoilerStats {
                OperatingBar = 68.7f,
                MaxBar = 75f,
                OperatingTemp = 732.2f,
                MaxMassFlow = 20.8f,
                MaxFuelFlow = 1.65f,
                ThermalMass = 450000f,
                WaterCapacity = 3954f,
                DrumVolume = 3.2f } },
            {"NPSBoilerBenson", new BoilerStats {
                OperatingBar = 107.9f,
                MaxBar = 120f,
                OperatingTemp = 783.2f,
                MaxMassFlow = 5.5f,
                MaxFuelFlow = 1.25f,
                ThermalMass = 95000f,
                WaterCapacity = 1298f,
                DrumVolume = 0.9f } },
            {"NPSBoilerKampon", new BoilerStats {
                OperatingBar = 39.4f,
                MaxBar = 44f,
                OperatingTemp = 673.2f,
                MaxMassFlow = 23.3f,
                MaxFuelFlow = 2.1f,
                ThermalMass = 243000f,
                WaterCapacity = 4633f,
                DrumVolume = 5f } },
        };

        public static readonly float[,] EnthalpyTable = new float[5, 5] {
            { 2733, 0, 0, 0, 0 },           // 400K
            { 2926, 2883, 2828, 0, 0 },      // 500K
            { 3074, 3052, 3026, 2966, 2888 },// 600K
            { 3224, 3211, 3197, 3167, 3134 },// 700K
            { 3448, 3440, 3432, 3415, 3397 } // 800K
        };
    }

    public class SteamTurbineStats
    {
        public float MinFlow;
        public float MaxFlow;
    }

    public class BoilerStats
    {
        public float OperatingBar;
        public float MaxBar;
        public float OperatingTemp; // Kelvin
        public float MaxMassFlow;  // kg per second
        public float MaxFuelFlow; // liters per second
        public float ThermalMass;   // KJ per K
        public float WaterCapacity;  // liters
        public float DrumVolume;    // m^3
    }

    public static class SteamTables
    {
        public static readonly float[,] EnthalpyTable = new float[10, 10] {
            // P: 1     15     30     45     60     75     90     105    125    145  (Bar)
            { 112,   0,     0,     0,     0,     0,     0,     0,     0,     0   }, // 300K
            { 322,   0,     0,     0,     0,     0,     0,     0,     0,     0   }, // 350Ks
            { 2675,  535,   0,     0,     0,     0,     0,     0,     0,     0   }, // 400K
            { 2776,  2730,  762,   0,     0,     0,     0,     0,     0,     0   }, // 450K
            { 2875,  2845,  2802,  2745,  1008,  0,     0,     0,     0,     0   }, // 500K
            { 2975,  2952,  2925,  2892,  2854,  2810,  2745,  1215,  0,     0   }, // 550K
            { 3074,  3055,  3038,  3016,  2992,  2965,  2934,  2898,  2832,  2755}, // 600K
            { 3175,  3160,  3145,  3132,  3115,  3100,  3080,  3060,  3028,  2995}, // 650K
            { 3378,  3368,  3358,  3348,  3338,  3325,  3315,  3305,  3285,  3265}, // 750K
            { 3582,  3575,  3568,  3560,  3552,  3545,  3535,  3525,  3515,  3500}  // 850K
        };

        public static readonly float[,] SpecVolumeTable = new float[10, 10] {
            // P: 1      15      30      45      60      75      90      105     125     145 (Bar)
            { 0.001f, 0,      0,      0,      0,      0,      0,      0,      0,      0     }, // 300K
            { 0.001f, 0,      0,      0,      0,      0,      0,      0,      0,      0     }, // 350K
            { 1.694f, 0.001f, 0,      0,      0,      0,      0,      0,      0,      0     }, // 400K
            { 1.935f, 0.125f, 0.001f, 0,      0,      0,      0,      0,      0,      0     }, // 450K
            { 2.172f, 0.142f, 0.068f, 0.043f, 0.001f, 0,      0,      0,      0,      0     }, // 500K
            { 2.405f, 0.158f, 0.077f, 0.049f, 0.035f, 0.026f, 0.019f, 0.001f, 0,      0     }, // 550K
            { 2.635f, 0.174f, 0.085f, 0.055f, 0.040f, 0.031f, 0.025f, 0.020f, 0.015f, 0.011f}, // 600K
            { 2.865f, 0.189f, 0.093f, 0.061f, 0.044f, 0.035f, 0.028f, 0.023f, 0.018f, 0.015f}, // 650K
            { 3.320f, 0.220f, 0.109f, 0.072f, 0.053f, 0.042f, 0.034f, 0.029f, 0.024f, 0.020f}, // 750K
            { 3.780f, 0.251f, 0.124f, 0.082f, 0.061f, 0.048f, 0.040f, 0.034f, 0.028f, 0.024f}  // 850K
        };

        public struct SteamState
        {
            public float Enthalpy;
            public float SpecificVolume;
        }
    }
}
