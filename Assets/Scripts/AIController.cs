using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIController : MonoBehaviour, InputController
{
    public KeyCode forwardKey;
    public KeyCode backwardKey;
    public KeyCode leftKey;
    public KeyCode rightKey;
    public KeyCode siphonKey;

    private bool pressingForward;
    private bool pressingBackward;
    private bool pressingLeft;
    private bool pressingRight;
    public bool pressingSiphon;

    public float sensitivity;
    private float mouseXrotation;
    private float mouseYrotation;

    public float scrollSensitivity = 1000f;

    public bool pressingFire1;
    public bool pressingFire2;
    public bool pressingFire3;

    public bool didScroll;

    private Vector3 AimTarget;
    public Transform headPosition;

    public Transform debugLookTarget;

    // Start is called before the first frame update
    void Start()
    {
        //Cursor.lockState = CursorLockMode.Locked;
        //Cursor.visible = false;
    }

    public bool getForward()
    {
        return pressingForward;
    }

    public bool getBackward()
    {
        return pressingBackward;
    }

    public bool getLeft()
    {
        return pressingLeft;
    }

    public bool getRight()
    {
        return pressingRight;
    }

    public Vector2 getHeadRotation()
    {
        //mouseYrotation = Input.GetAxis("Mouse X") * sensitivity * Time.fixedDeltaTime;
        //mouseXrotation = Input.GetAxis("Mouse Y") * sensitivity * Time.fixedDeltaTime;

        //if(Input.GetAxis("Mouse X") != 0)
        //{
        //    Debug.Log("mouse input registered");
        //}

        mouseXrotation = Mathf.Clamp(mouseXrotation, -90, 90);

        return new Vector2(mouseXrotation, mouseYrotation);
    }

    public bool getAimRight()
    {
        return false;
    }

    public bool getAimLeft()
    {
        return false;
    }

    public void setHeadXRotation(float rotation)
    {
        mouseXrotation = rotation;
    }

    public void setHeadYRotation(float rotation)
    {
        mouseYrotation = rotation;
    }

    public bool getFire1()
    {
        return pressingFire1;
    }

    public bool getFire2()
    {
        return pressingFire2;
    }

    public bool getFire3()
    {
        return pressingFire3;
    }

    public bool getScroll()
    {
        return didScroll;
    }

    public bool getScrollUp()
    {
        return false;
    }

    public bool getScrollDown()
    {
        return false;
    }

    public bool getAimMiddle()
    {
        return false;
    }

    public bool getSiphon()
    {
        return pressingSiphon;
    }

    // Update is called once per frame
    void Update()
    {
        //SetAimTarget(debugLookTarget.position);
        AimAtCurrentAimTarget();
        // mouseXrotation = 0;
        // mouseYrotation = 0;
        // pressingForward = Input.GetKey(forwardKey);
        // pressingBackward = Input.GetKey(backwardKey);
        // pressingLeft = Input.GetKey(leftKey);
        // pressingRight = Input.GetKey(rightKey);

        // pressingFire1 = Input.GetMouseButton(0);
        // pressingFire2 = Input.GetMouseButton(1);
        // pressingFire3 = Input.GetMouseButton(2);

        // pressingSiphon = Input.GetKey(siphonKey);
    }

    public void SetAimTarget(Vector3 target)
    {
        AimTarget = target;
    }

    private void AimAtCurrentAimTarget()
    {
        Vector3 targetDirection = AimTarget - headPosition.position;

        // Project the targetDirection onto the horizontal plane (X and Z axes)
        Vector3 horizontalPlaneNormal = headPosition.up; // Assuming the horizontal plane is the XZ plane
        Vector3 horizontalTargetDirection = Vector3.ProjectOnPlane(targetDirection, horizontalPlaneNormal);

        // Calculate mouseXrotation
        float x = Vector3.Dot(headPosition.right, horizontalTargetDirection.normalized);

        // Project the targetDirection onto the vertical plane (Y and Z axes)
        Vector3 verticalPlaneNormal = headPosition.right; // Assuming the vertical plane is the YZ plane
        Vector3 verticalTargetDirection = Vector3.ProjectOnPlane(targetDirection, verticalPlaneNormal);

        // Calculate mouseYrotation
        float y = Vector3.Dot(headPosition.up, verticalTargetDirection.normalized);

        // Clamp the values to the desired range (-1 to 1)
        mouseYrotation = x * sensitivity;
        mouseXrotation = y * sensitivity;
    }
}
