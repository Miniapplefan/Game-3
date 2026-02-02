using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour, InputController
{
    public KeyCode forwardKey;
    public KeyCode backwardKey;
    public KeyCode leftKey;
    public KeyCode rightKey;
    public KeyCode siphonKey;
    public KeyCode aimRightKey;
    public KeyCode aimLeftKey;
    public KeyCode reloadKey;
    public KeyCode restartKey;

    private bool pressingForward;
    private bool pressingBackward;
    private bool pressingLeft;
    private bool pressingRight;
    private bool pressingAimRight;
    private bool pressingAimLeft;
    private bool pressingAimMiddle;
    private bool pressingSiphon;
    private bool pressingRestart;
    private bool pressingReload;

    public float sensitivity;
    private float mouseXrotation;
    private float mouseYrotation;

    public float scrollSensitivity = 0.05f;

    public bool pressingFire1;
    public bool pressingFire2;
    public bool pressingFire3;


    // Start is called before the first frame update
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
        mouseYrotation = Input.GetAxis("Mouse X") * sensitivity * Time.fixedDeltaTime;
        mouseXrotation = Input.GetAxis("Mouse Y") * sensitivity * Time.fixedDeltaTime;

        //if(Input.GetAxis("Mouse X") != 0)
        //{
        //    Debug.Log("mouse input registered");
        //}

        mouseXrotation = Mathf.Clamp(mouseXrotation, -90, 90);

        return new Vector2(mouseXrotation, mouseYrotation);
    }

    public bool getAimRight()
    {
        return pressingAimRight;
    }


    public bool getAimLeft()
    {
        return pressingAimLeft;
    }

    public bool getAimMiddle()
    {
        return pressingAimMiddle;
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
        // Check if mouse wheel is scrolled
        float scrollWheelInput = Input.GetAxis("Mouse ScrollWheel");

        // If scrollWheelInput is not within the sensitivity threshold, return
        if (Mathf.Abs(scrollWheelInput) < scrollSensitivity)
        {
            return false;
        }

        // Set didScroll to true
        return true;
    }

    public bool getScrollUp()
    {
        // Check if mouse wheel is scrolled
        float scrollWheelInput = Input.GetAxis("Mouse ScrollWheel");

        if (scrollWheelInput > scrollSensitivity)
        {
            return true;
        }

        return false;
    }

    public bool getScrollDown()
    {
        // Check if mouse wheel is scrolled
        float scrollWheelInput = Input.GetAxis("Mouse ScrollWheel");

        if (scrollWheelInput < -scrollSensitivity)
        {
            return true;
        }

        return false;
    }

    public bool getSiphon()
    {
        return pressingSiphon;
    }

    //    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

    public bool getRestart()
    {
        return pressingRestart;
    }

    public bool getReload()
    {
        return pressingReload;
    }

    public void doRestart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Update is called once per frame
    void Update()
    {
        pressingForward = Input.GetKey(forwardKey);
        pressingBackward = Input.GetKey(backwardKey);
        pressingLeft = Input.GetKey(leftKey);
        pressingRight = Input.GetKey(rightKey);

        pressingFire1 = Input.GetMouseButton(0);
        pressingFire2 = Input.GetMouseButton(1);
        pressingAimMiddle = Input.GetMouseButton(2);

        pressingReload = Input.GetKey(reloadKey);

        pressingAimRight = Input.GetKey(aimRightKey);
        pressingAimLeft = Input.GetKey(aimLeftKey);


        pressingSiphon = Input.GetKey(siphonKey);
        pressingRestart = Input.GetKey(restartKey);
    }
}
