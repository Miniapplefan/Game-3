using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveRightCommand : ICommand
{
    LegsModel model;

    public MoveRightCommand(LegsModel m)
    {
        model = m;
    }

    public void Execute()
    {
        //Debug.Log("move forward");
        //model.rb.AddForce(-model.rb.transform.right * model.getMoveSpeed() * model.moveAcceleration * Time.deltaTime);

        Vector3 right = Vector3.ProjectOnPlane(model.headOrientation.transform.right, Vector3.up).normalized;

        model.rb.AddForce(-right * model.getMoveSpeed() * model.speedMultiplier * model.moveAcceleration * Time.deltaTime);
    }
}
