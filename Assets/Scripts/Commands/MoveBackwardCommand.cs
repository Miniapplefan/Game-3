using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveBackwardCommand : ICommand
{
    LegsModel model;

    public MoveBackwardCommand(LegsModel m)
    {
        model = m;
    }

    public void Execute()
    {
        //Debug.Log("move forward");
        model.rb.AddForce(-model.headOrientation.transform.up.normalized * model.getMoveSpeed() * model.moveAcceleration * Time.deltaTime);
    }
}
