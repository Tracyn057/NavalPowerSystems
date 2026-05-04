using NavalPowerSystems.Common;
using Sandbox.ModAPI;
using SpaceEngineers.Game.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace NavalPowerSystems.Drivetrain.Producers.Combustion
{
    public class TestGasTurbine : DrivetrainPart<IMyFunctionalBlock>
    {
        #region Turbine Stats
        private double MaxAirFlowKgs = 70.5, MaxAirFlowLps = 57528; // Lps = 816 * maxKgs
        private double MaxFuelKgs = 1.66, MaxFuelLps = 1.955; // kgs = lps * 0.85 (density)
        private double CompEGT = 658, EGT = 839.15; // CompEGT = 288 * Math.Pow(PressureRatio, 0.287)
        private double SpecificHeatAir = 1005, DieselEnergy = 42000000;
        private double ComponentEfficiency = 0.90;
        private double MaxCompressorPower = 26215425; // MassFlow * SpecificHeatAir * (CompEGT - 288)
        private double RadToRPMMult = 9.5493, RPMToRadMult = 0.1047197551197;
        private double GearRatio = 16.36;
        #endregion

        #region Turbine Variables
        private double TargetThrottle, CurrentThrottle, CurrentFuelKgs;
        private double CurrentFuelLps => CurrentThrottle * MaxFuelLps;
        private double CurrentAirLps => CurrentAirPct * MaxAirFlowLps;
        private double CurrentAirKgs => CurrentAirPct * MaxAirFlowKgs;
        private double CurrentAirPct => CurrentRPM_GG / MaxRPM_GG;
        private double MoI_GG = 297, MoI_PT = 2292;
        private double TargetRPM_GG, CurrentRPM_GG, IdleRPM_GG = 4500, MaxRPM_GG = 10000;
        private double TargetRPM_PT, CurrentRPM_PT, IdleRPM_PT = 500, MaxRPM_PT = 3600;
        #endregion

        #region PID
        private PIDController GGIdleController = new PIDController(0.25, 0.01, 0.001, 0.5);
        private PIDController FuelController = new PIDController(0.4, 0.1, 0.01, 0.5);
        #endregion

        private void TurbineSimulation()
        {
            double thermalEnergy = CurrentFuelKgs * DieselEnergy;
            double deltaTemp;
            if (CurrentAirKgs <= 0.1)
                deltaTemp = 0;
            else
                deltaTemp = thermalEnergy / (CurrentAirKgs * SpecificHeatAir);
            double turbineInTemp = CompEGT + deltaTemp;
            double compressorPowerRequired = MaxCompressorPower * Math.Pow(CurrentRPM_GG / MaxRPM_GG, 3);
            double grossPowerGG = CurrentAirKgs * SpecificHeatAir * (turbineInTemp - CompEGT) * ComponentEfficiency;
            double netPowerGG = grossPowerGG - compressorPowerRequired;
            double netTorqueGG = RPMToRadMult * netPowerGG / Math.Max(CurrentRPM_GG, 10);
            double accelGG = netTorqueGG / MoI_GG * RadToRPMMult;
            CurrentRPM_GG += accelGG * PhysicsStep;
            CurrentRPM_GG = MathHelper.Clamp(CurrentRPM_GG, 0, MaxRPM_GG);

            double tempDropGG = compressorPowerRequired / (Math.Max(CurrentAirKgs, 1) * SpecificHeatAir * ComponentEfficiency);
            double ptInTemp = turbineInTemp - tempDropGG;
            double tempDropPT = Math.Max(0, (ptInTemp - EGT) * ComponentEfficiency);
            double availPowerPT = CurrentAirKgs * SpecificHeatAir * tempDropPT;
            double ratedTorquePT = RPMToRadMult * availPowerPT / Math.Max(CurrentRPM_PT, 10);
            double availTorquePT = ratedTorquePT * (2 - (CurrentRPM_PT / MaxRPM_PT));
            double netTorquePT = availTorquePT - Load_In;
            double accelPT = netTorquePT / MoI_PT * RadToRPMMult;
            CurrentRPM_PT += accelPT * PhysicsStep;
            CurrentRPM_PT = MathHelper.Clamp(CurrentRPM_PT, 0, MaxRPM_PT);
        }

        private void Controller()
        {
            double normIdleTarget = IdleRPM_GG / MaxRPM_GG;
            double normGG = CurrentRPM_GG / MaxRPM_GG;

            double userRequest = TargetThrottle;
            double idleRequest = GGIdleController.Update(normIdleTarget, normGG);

            if (userRequest > idleRequest)
                GGIdleController.isFrozen = true;
            else
                GGIdleController.isFrozen = false;

            double finalRequest = Math.Max(idleRequest, userRequest);
            double normFuel = CurrentFuelKgs / MaxFuelKgs;
            double fuelPct = FuelController.Update(finalRequest, normFuel);

            CurrentFuelKgs = MathHelper.Clamp(fuelPct * MaxFuelKgs, 0, MaxFuelKgs);
            CurrentThrottle = CurrentFuelKgs / MaxFuelKgs;
        }
    }
}
