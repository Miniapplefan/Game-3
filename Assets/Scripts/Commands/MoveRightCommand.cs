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
        model.rb.AddForce(-model.headOrientation.transform.right * model.getMoveSpeed() * model.moveAcceleration * Time.deltaTime);
    }
}
