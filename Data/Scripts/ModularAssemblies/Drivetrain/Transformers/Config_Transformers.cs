using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NavalPowerSystems.Drivetrain.Transformers
{
    public static class Config_Transformers
    {
        public static readonly Dictionary<string, double> GearboxSettings = new Dictionary<string, double>()
        {
            {"NPS_Gearbox_MRG", 2000000 },
            {"NPS_Gearbox_DoublePlanetary", 750000 }
        };
    }

}
