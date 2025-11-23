using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveLeftCommand : ICommand
{
    LegsModel model;

    public MoveLeftCommand(LegsModel m)
    {
        model = m;
    }

    public void Execute()
    {
        //Debug.Log("move forward");
        model.rb.AddForce(model.headOrientation.transform.right * model.getMoveSpeed() * model.moveAcceleration * Time.deltaTime);
    }
}
