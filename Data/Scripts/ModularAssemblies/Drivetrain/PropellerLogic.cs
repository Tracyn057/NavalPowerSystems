using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NavalPowerSystems.Drivetrain
{
    internal class PropellerLogic
    {


            // Example for your ApplyForce method
public void ApplyPropellerForce(float thrustNewtons)
{
    var physics = Entity.CubeGrid?.Physics;
    if (physics == null) return;

    // Direction the propeller 'pushes' (Backward matrix to go Forward)
    Vector3D forceDirection = Entity.WorldMatrix.Backward;
    Vector3D forceVector = forceDirection * thrustNewtons;

    // Position of the propeller block in world space
    Vector3D blockPosition = Entity.WorldMatrix.Translation;

    // Based on the docs you found:
    // APPLY_WORLD_FORCE: Interprets the vector as World coordinates
    // null: This is the 'torque' argument. By passing null, SE calculates 
    // torque naturally based on the offset from the Center of Mass.
    physics.AddForce(
        MyPhysicsForceType.APPLY_WORLD_FORCE, 
        forceVector, 
        blockPosition, 
        null
    );
}


        
    }
}
