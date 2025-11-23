using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class RotateHeadCommand : ICommand
{
    public SensorsModel sensors;

    float currentXrotation;
    float currentYrotation;

    float xRotationRef;
    float yRotationRef;

    public RotateHeadCommand(SensorsModel m)
    {
        sensors = m;
        //currentXrotation = m.head.transform.rotation.eulerAngles.x;
        //currentYrotation = m.head.transform.rotation.eulerAngles.y;
    }

    private float desiredX;
    private float xRotation;
    public void Execute()
    {
        Vector3 rot = sensors.head.transform.localRotation.eulerAngles;
        desiredX = rot.y + sensors.yRotation;

        xRotation -= sensors.xRotation;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        //currentXrotation = Mathf.SmoothDamp(currentXrotation, xRotation, ref xRotationRef, sensors.rotationSmoothDamp);
        //currentYrotation = Mathf.SmoothDamp(currentYrotation, desiredX, ref yRotationRef, sensors.rotationSmoothDamp);

        if (sensors.bodyController.isAimingRight)
        {
            sensors.head.transform.localRotation = Quaternion.Euler(xRotation, desiredX, desiredX);
        }
        else if (sensors.bodyController.isAimingLeft)
        {
            rot = sensors.headL.transform.localRotation.eulerAngles;
            desiredX = rot.y + sensors.yRotation;
            sensors.headL.transform.localRotation = Quaternion.Euler(xRotation, desiredX, desiredX);
        }
        else
        {
            sensors.head.transform.localRotation = Quaternion.Euler(xRotation, desiredX, desiredX);
        }
    }
}
