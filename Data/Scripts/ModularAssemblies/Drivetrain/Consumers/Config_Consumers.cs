using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NavalPowerSystems.Drivetrain.Consumers
{
    public class Config_Consumers
    {
        public static readonly Dictionary<string, PropellerSettings> PropellerStats = new Dictionary<string, PropellerSettings>
        {
            {"NPS_Propeller_4m3b", new PropellerSettings { Diameter = 4f, Blades = 3, DesignRPM = 220, Inertia = 8679, IsCRP = false, IsCCW = false } },
            {"NPS_Propeller_4m4b", new PropellerSettings { Diameter = 4f, Blades = 4, DesignRPM = 220, Inertia = 11078, IsCRP = false, IsCCW = false } },
            {"NPS_Propeller_4m5b", new PropellerSettings { Diameter = 4f, Blades = 5, DesignRPM = 220, Inertia = 14120, IsCRP = false, IsCCW = false } },
            {"NPS_Propeller_4m3b_CCW", new PropellerSettings { Diameter = 4f, Blades = 3, DesignRPM = 220, Inertia = 8679, IsCRP = false, IsCCW = true } },
            {"NPS_Propeller_4m4b_CCW", new PropellerSettings { Diameter = 4f, Blades = 4, DesignRPM = 220, Inertia = 11078, IsCRP = false, IsCCW = true } },
            {"NPS_Propeller_4m5b_CCW", new PropellerSettings { Diameter = 4f, Blades = 5, DesignRPM = 220, Inertia = 14120, IsCRP = false, IsCCW = true } },

        };
    }

    public class PropellerSettings
    {
        public float Diameter; // Diameter in meters
        public int Blades;
        public double DesignRPM;
        public double Inertia;
        public bool IsCRP; // Whether the propeller is a controllable pitch propeller, which affects how torque and thrust are calculated
        public bool IsCCW; // Whether the propeller rotates counterclockwise, used for visual effects and potential future logic

        /* 
         * NIBRAL density = 7640 kg/m3
         * Mass values used for props:
         * - 4m 3b = 10,849kg
         * - 4m 4b = 11,078kg
         * - 4m 5b = 11,767kg
         * 
         * Inertia calc: K * mass * radius^2
         * Where K =
         * 3b: 0.2
         * 4b: 0.25
         * 5b: 0.3
         */
    }
}
