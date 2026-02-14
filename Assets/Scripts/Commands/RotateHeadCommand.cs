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
    private float xRotationRight;
    private float xRotationLeft;

    public void ResetPitch()
    {
        xRotationRight = 0f;
        xRotationLeft = 0f;
    }
    public void Execute()
    {
        if (sensors.bodyController.isAimingLeft)
        {
            Vector3 rot = sensors.headL.transform.localRotation.eulerAngles;
            desiredX = rot.y + sensors.yRotation;

            xRotationLeft -= sensors.xRotation;
            xRotationLeft = Mathf.Clamp(xRotationLeft, -90f, 90f);

            //currentXrotation = Mathf.SmoothDamp(currentXrotation, xRotationLeft, ref xRotationRef, sensors.rotationSmoothDamp);
            //currentYrotation = Mathf.SmoothDamp(currentYrotation, desiredX, ref yRotationRef, sensors.rotationSmoothDamp);

            sensors.headL.transform.localRotation = Quaternion.Euler(xRotationLeft, desiredX, desiredX);
            return;
        }

        Vector3 headRot = sensors.head.transform.localRotation.eulerAngles;
        desiredX = headRot.y + sensors.yRotation;

        xRotationRight -= sensors.xRotation;
        xRotationRight = Mathf.Clamp(xRotationRight, -90f, 90f);

        //currentXrotation = Mathf.SmoothDamp(currentXrotation, xRotationRight, ref xRotationRef, sensors.rotationSmoothDamp);
        //currentYrotation = Mathf.SmoothDamp(currentYrotation, desiredX, ref yRotationRef, sensors.rotationSmoothDamp);

        sensors.head.transform.localRotation = Quaternion.Euler(xRotationRight, desiredX, desiredX);

        // Keep the left aim reference in sync with current pitch for player-controlled aiming.
        // AI uses a single-arm flow and should remain unchanged.
        if (!sensors.bodyController.isAI && sensors.headL != null)
        {
            xRotationLeft = xRotationRight;
            Vector3 leftRot = sensors.headL.transform.localRotation.eulerAngles;
            sensors.headL.transform.localRotation = Quaternion.Euler(xRotationLeft, leftRot.y, leftRot.z);
        }
    }
}
