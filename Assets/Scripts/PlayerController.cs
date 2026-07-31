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
    public KeyCode shiftKey;

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
    private bool pressingShift;

    public float sensitivity;
    private float mouseXrotation;
    private float mouseYrotation;

    public float scrollSensitivity = 0.05f;
    public float scrollDebounceTime = 0.06f;
    public float scrollDirectionChangeBlockTime = 0.05f;
    [Range(0f, 1f)]
    public float scrollReleaseThresholdRatio = 0.35f;
    private int pendingScrollDirection = 0;
    private bool scrollTriggerArmed = true;
    private float nextScrollEventTime = 0f;
    private float blockOppositeDirectionUntil = 0f;
    private int lastScrollDirection = 0;

    public bool pressingFire1;
    public bool pressingFire2;
    public bool pressingFire3;
    private bool pendingFire1Down;
    private bool pendingFire2Down;


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

    public bool getFire1Down()
    {
        bool pressed = pendingFire1Down;
        pendingFire1Down = false;
        return pressed;
    }

    public bool getFire2Down()
    {
        bool pressed = pendingFire2Down;
        pendingFire2Down = false;
        return pressed;
    }

    public bool getScroll()
    {
        return pendingScrollDirection != 0;
    }

    public bool getScrollUp()
    {
        return ConsumeScrollDirection(1);
    }

    public bool getScrollDown()
    {
        return ConsumeScrollDirection(-1);
    }

    private bool ConsumeScrollDirection(int direction)
    {
        if (pendingScrollDirection != direction)
        {
            return false;
        }

        pendingScrollDirection = 0;
        return true;
    }

    private void UpdateScrollInput()
    {
        float scrollWheelInput = Input.GetAxis("Mouse ScrollWheel");
        float threshold = Mathf.Max(0.0001f, scrollSensitivity);
        float releaseThreshold = threshold * Mathf.Clamp01(scrollReleaseThresholdRatio);

        if (Mathf.Abs(scrollWheelInput) <= releaseThreshold)
        {
            scrollTriggerArmed = true;
            return;
        }

        if (!scrollTriggerArmed || Mathf.Abs(scrollWheelInput) < threshold)
        {
            return;
        }

        int direction = scrollWheelInput > 0f ? 1 : -1;
        float now = Time.unscaledTime;
        bool inDebounceWindow = now < nextScrollEventTime;
        bool isOppositeBounce = lastScrollDirection != 0
            && direction != lastScrollDirection
            && now < blockOppositeDirectionUntil;

        // Require a release back toward neutral before accepting another scroll event.
        scrollTriggerArmed = false;

        if (inDebounceWindow || isOppositeBounce)
        {
            return;
        }

        if (pendingScrollDirection == 0)
        {
            pendingScrollDirection = direction;
        }

        lastScrollDirection = direction;
        nextScrollEventTime = now + Mathf.Max(0f, scrollDebounceTime);
        blockOppositeDirectionUntil = now + Mathf.Max(0f, scrollDirectionChangeBlockTime);
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

    public bool getShift()
    {
        return pressingShift;
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

        pendingFire1Down |= Input.GetMouseButtonDown(0);
        pendingFire2Down |= Input.GetMouseButtonDown(1);
        pressingFire1 = Input.GetMouseButton(0);
        pressingFire2 = Input.GetMouseButton(1);
        pressingAimMiddle = Input.GetMouseButton(2);

        pressingReload = Input.GetKey(reloadKey);

        pressingAimRight = Input.GetKey(aimRightKey);
        pressingAimLeft = Input.GetKey(aimLeftKey);


        pressingSiphon = Input.GetKey(siphonKey);
        pressingRestart = Input.GetKey(restartKey);
        pressingShift = Input.GetKey(shiftKey);
        UpdateScrollInput();
    }
}
