using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NavalPowerSystems.Drivetrain
{
    internal class PropellerLogic
    {


private void ApplySplitForce(float totalThrustNewtons)
{
    var grid = Entity.CubeGrid;
    if (grid?.Physics == null) return;

    // 1. Calculate the direction (Pushing backward to go forward)
    Vector3D forceDirection = Entity.WorldMatrix.Backward;
    Vector3D totalForceVector = forceDirection * totalThrustNewtons;

    // 2. Define your split ratio (e.g., 70% stable, 30% realistic torque)
    float stabilityFactor = 0.7f; 
    Vector3D stableForce = totalForceVector * stabilityFactor;
    Vector3D torqueForce = totalForceVector * (1.0f - stabilityFactor);

    // 3. Apply the 'Stable' portion to the Center of Mass
    // This moves the ship forward without turning it at all.
    grid.Physics.AddForce(
        MyPhysicsForceType.APPLY_WORLD_FORCE, 
        stableForce, 
        grid.Physics.CenterOfMassWorld, 
        null
    );

    // 4. Apply the 'Torque' portion to the Block Position
    // This creates the yaw/pitch/roll based on where the prop is mounted.
    grid.Physics.AddForce(
        MyPhysicsForceType.APPLY_WORLD_FORCE, 
        torqueForce, 
        Entity.WorldMatrix.Translation, 
        null
    );
}


        
    }
}
