using SpaceEngineers.Game.Utils;
using System;

public class LM2500Simulator
{
    // Constants (based on LM2500+ 60Hz specs)
    private const double MAX_PT_RPM = 3600.0;
    private const double MAX_GG_RPM = 12000.0;
    private const double FUEL_FLOW_IDLE_KG_S = 0.101; // 800 lbs/hr
    private const double FUEL_HEATING_VALUE_J_KG = 42e6;
    private const double THERMAL_EFFICIENCY = 0.388; // 38.8% for LM2500+
    private const double PRESSURE_RATIO = 23.1;

    // State Variables
    public double GasGeneratorRPM { get; private set; } = 1200.0; // Initial idle
    public double PowerTurbineRPM { get; private set; } = 0.0; // Can start at 0
    public double FuelFlowKgS { get; private set; } = FUEL_FLOW_IDLE_KG_S;

    // Inertia (J) calculated from H=5s, Sbase=35MW, at max speed
    private readonly double J_PT = (2 * 5.0 * 35e6) / Math.Pow((2 * Math.PI * MAX_PT_RPM / 60), 2);

    // PID Controller for Power Turbine Speed
    private readonly PIDController speedController = new PIDController { Kp = 0.8, Ki = 0.02, Kd = 0.0 };

    public void Step(double dt, double targetRPM, double loadTorqueN_m = 0.0)
    {
        // 1. Calculate PT Angular Velocity
        double omega_PT = (2.0 * Math.PI * PowerTurbineRPM) / 60.0;

        // 2. Calculate Frictional Torque (simplified)
        double frictionTorque_N_m = 11000.0; // ~11,252 N·m at 3600 rpm

        // 3. Calculate Net Torque on PT Shaft
        double netTorque_N_m = 0.0;
        if (omega_PT > 0.1) // Avoid divide-by-zero
        {
            // Estimate gas power from fuel flow and efficiency
            double gasPower_W = FuelFlowKgS * FUEL_HEATING_VALUE_J_KG * THERMAL_EFFICIENCY;
            double drivingTorque_N_m = gasPower_W / omega_PT;
            netTorque_N_m = drivingTorque_N_m - frictionTorque_N_m - loadTorqueN_m;
        }

        // 4. Update PT Speed (Newton's 2nd Law for rotation)
        double angularAcceleration = netTorque_N_m / J_PT;
        double newOmega = omega_PT + (angularAcceleration * dt);
        PowerTurbineRPM = Math.Max(0.0, (newOmega * 60.0) / (2.0 * Math.PI));

        // 5. Update GG RPM based on fuel flow (simplified dynamics)
        double targetGG_RPM = Math.Min(MAX_GG_RPM, 10000.0 * FuelFlowKgS); // Linear approx.
        GasGeneratorRPM += (targetGG_RPM - GasGeneratorRPM) * 0.1 * dt; // 1st order lag

        // 6. PID Controller adjusts fuel flow based on PT RPM error
        FuelFlowKgS = speedController.Calculate(targetRPM, PowerTurbineRPM, dt);
        FuelFlowKgS = Math.Max(FUEL_FLOW_IDLE_KG_S, FuelFlowKgS); // Enforce minimum
    }
}