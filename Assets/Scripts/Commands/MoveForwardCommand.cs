using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveForwardCommand : ICommand
{
	LegsModel model;

	public MoveForwardCommand(LegsModel m)
	{
		model = m;
	}

	public void Execute()
	{
		//Debug.Log("move forward");
		//model.rb.AddForce(model.rb.transform.forward * model.getMoveSpeed() * model.moveAcceleration * Time.deltaTime);

		Vector3 forward = Vector3.ProjectOnPlane(model.headOrientation.transform.up, Vector3.up).normalized;

		model.rb.AddForce(forward * model.getMoveSpeed() * model.moveAcceleration * Time.deltaTime);
	}

}
