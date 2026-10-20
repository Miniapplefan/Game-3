using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum PlayerInputBindingSource
{
    None,
    [InspectorName("Keyboard or Mouse Button")]
    KeyCode,
    [InspectorName("Scroll Up")]
    ScrollUp,
    [InspectorName("Scroll Down")]
    ScrollDown
}

[System.Serializable]
public struct PlayerInputBinding
{
    [SerializeField] private PlayerInputBindingSource source;
    [SerializeField] private KeyCode key;

    public PlayerInputBindingSource Source => source;
    public KeyCode Key => key;

    public PlayerInputBinding(PlayerInputBindingSource source, KeyCode key = KeyCode.None)
    {
        this.source = source;
        this.key = key;
    }

    public bool IsConfigured()
    {
        return source != PlayerInputBindingSource.None
            && (source != PlayerInputBindingSource.KeyCode || key != KeyCode.None);
    }

    public bool UsesSameInputAs(PlayerInputBinding other)
    {
        if (!IsConfigured() || !other.IsConfigured() || source != other.source)
        {
            return false;
        }

        return source != PlayerInputBindingSource.KeyCode || key == other.key;
    }
}

[DefaultExecutionOrder(-100)]
public class PlayerController : MonoBehaviour, InputController
{
    public KeyCode forwardKey;
    public KeyCode backwardKey;
    public KeyCode leftKey;
    public KeyCode rightKey;
    public KeyCode siphonKey;
    public KeyCode leanRightKey;
    public KeyCode leanLeftKey;
    public KeyCode reloadKey;
    public KeyCode restartKey;
    public KeyCode shiftKey;

    [Header("Arm Input Bindings")]
    [SerializeField, Tooltip("Input that breaks out or retriggers the left arm. This replaces the default Scroll Up input.")]
    private PlayerInputBinding breakoutLeftBinding = new PlayerInputBinding(PlayerInputBindingSource.ScrollUp);
    [SerializeField, Tooltip("Input that breaks out or retriggers the right arm. This replaces the default Scroll Down input.")]
    private PlayerInputBinding breakoutRightBinding = new PlayerInputBinding(PlayerInputBindingSource.ScrollDown);
    [SerializeField, Tooltip("Input that recenters the active arm. This replaces the default Middle Mouse Button input.")]
    private PlayerInputBinding recenterArmBinding = new PlayerInputBinding(PlayerInputBindingSource.KeyCode, KeyCode.Mouse2);

    private bool pressingForward;
    private bool pressingBackward;
    private bool pressingLeft;
    private bool pressingRight;
    private bool pressingAimRight;
    private bool pressingAimLeft;
    private bool pressingSiphon;
    private bool pressingRestart;
    private bool pressingReload;
    private bool pressingShift;
    private bool gameplayInputEnabled = true;
    private bool pendingBreakoutLeft;
    private bool pendingBreakoutRight;
    private bool pendingRecenterArm;

    public float sensitivity;
    [Min(0f), Tooltip("Mouse-look delta required to interrupt aim assist while the arm is moving toward its target.")]
    public float aimAssistBreakoutMouseDeadZone = 0.01f;
    private float mouseXrotation;
    private float mouseYrotation;

    public float scrollSensitivity = 0.05f;
    public float scrollDebounceTime = 0.06f;
    public float scrollDirectionChangeBlockTime = 0.05f;
    [Range(0f, 1f)]
    public float scrollReleaseThresholdRatio = 0.35f;
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
        return new Vector2(mouseXrotation, mouseYrotation);
    }

    public bool HasAimAssistBreakoutInput(Vector2 headRotation)
    {
        float deadZone = Mathf.Max(0f, aimAssistBreakoutMouseDeadZone);
        return headRotation.sqrMagnitude > deadZone * deadZone;
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
        return ConsumePendingAction(ref pendingRecenterArm);
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
        return pendingBreakoutLeft || pendingBreakoutRight;
    }

    public bool getScrollUp()
    {
        return ConsumePendingAction(ref pendingBreakoutLeft);
    }

    public bool getScrollDown()
    {
        return ConsumePendingAction(ref pendingBreakoutRight);
    }

    private static bool ConsumePendingAction(ref bool pendingAction)
    {
        bool wasPending = pendingAction;
        pendingAction = false;
        return wasPending;
    }

    private int ReadScrollDirection()
    {
        float scrollWheelInput = Input.GetAxis("Mouse ScrollWheel");
        float threshold = Mathf.Max(0.0001f, scrollSensitivity);
        float releaseThreshold = threshold * Mathf.Clamp01(scrollReleaseThresholdRatio);

        if (Mathf.Abs(scrollWheelInput) <= releaseThreshold)
        {
            scrollTriggerArmed = true;
            return 0;
        }

        if (!scrollTriggerArmed || Mathf.Abs(scrollWheelInput) < threshold)
        {
            return 0;
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
            return 0;
        }

        lastScrollDirection = direction;
        nextScrollEventTime = now + Mathf.Max(0f, scrollDebounceTime);
        blockOppositeDirectionUntil = now + Mathf.Max(0f, scrollDirectionChangeBlockTime);
        return direction;
    }

    private void UpdateArmActionInput()
    {
        int scrollDirection = ReadScrollDirection();
        bool breakoutLeftPressed = WasBindingPressed(breakoutLeftBinding, scrollDirection);
        bool breakoutRightPressed = WasBindingPressed(breakoutRightBinding, scrollDirection);
        bool recenterPressed = WasBindingPressed(recenterArmBinding, scrollDirection);

        bool breakoutLeftConflicts = breakoutLeftPressed
            && ((breakoutRightPressed && breakoutLeftBinding.UsesSameInputAs(breakoutRightBinding))
                || (recenterPressed && breakoutLeftBinding.UsesSameInputAs(recenterArmBinding)));
        bool breakoutRightConflicts = breakoutRightPressed
            && ((breakoutLeftPressed && breakoutRightBinding.UsesSameInputAs(breakoutLeftBinding))
                || (recenterPressed && breakoutRightBinding.UsesSameInputAs(recenterArmBinding)));
        bool recenterConflicts = recenterPressed
            && ((breakoutLeftPressed && recenterArmBinding.UsesSameInputAs(breakoutLeftBinding))
                || (breakoutRightPressed && recenterArmBinding.UsesSameInputAs(breakoutRightBinding)));

        pendingBreakoutLeft |= breakoutLeftPressed && !breakoutLeftConflicts;
        pendingBreakoutRight |= breakoutRightPressed && !breakoutRightConflicts;
        pendingRecenterArm |= recenterPressed && !recenterConflicts;
    }

    private static bool WasBindingPressed(PlayerInputBinding binding, int scrollDirection)
    {
        switch (binding.Source)
        {
            case PlayerInputBindingSource.KeyCode:
                return binding.Key != KeyCode.None && Input.GetKeyDown(binding.Key);
            case PlayerInputBindingSource.ScrollUp:
                return scrollDirection > 0;
            case PlayerInputBindingSource.ScrollDown:
                return scrollDirection < 0;
            default:
                return false;
        }
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

    public void SetGameplayInputEnabled(bool enabled)
    {
        gameplayInputEnabled = enabled;

        if (!enabled)
        {
            ClearGameplayInput();
        }
    }

    private void ClearGameplayInput()
    {
        mouseXrotation = 0f;
        mouseYrotation = 0f;

        pressingForward = false;
        pressingBackward = false;
        pressingLeft = false;
        pressingRight = false;
        pressingAimRight = false;
        pressingAimLeft = false;
        pressingSiphon = false;
        pressingRestart = false;
        pressingReload = false;
        pressingShift = false;

        pressingFire1 = false;
        pressingFire2 = false;
        pressingFire3 = false;
        pendingFire1Down = false;
        pendingFire2Down = false;
        pendingBreakoutLeft = false;
        pendingBreakoutRight = false;
        pendingRecenterArm = false;
        scrollTriggerArmed = true;
        nextScrollEventTime = 0f;
        blockOppositeDirectionUntil = 0f;
        lastScrollDirection = 0;
    }

    // Update is called once per frame
    void Update()
    {
        if (!gameplayInputEnabled)
        {
            ClearGameplayInput();
            return;
        }

        mouseYrotation = Input.GetAxis("Mouse X") * sensitivity * Time.deltaTime;
        mouseXrotation = Input.GetAxis("Mouse Y") * sensitivity * Time.deltaTime;
        mouseXrotation = Mathf.Clamp(mouseXrotation, -90f, 90f);

        pressingForward = Input.GetKey(forwardKey);
        pressingBackward = Input.GetKey(backwardKey);
        pressingLeft = Input.GetKey(leftKey);
        pressingRight = Input.GetKey(rightKey);

        pendingFire1Down |= Input.GetMouseButtonDown(0);
        pendingFire2Down |= Input.GetMouseButtonDown(1);
        pressingFire1 = Input.GetMouseButton(0);
        pressingFire2 = Input.GetMouseButton(1);

        pressingReload = Input.GetKey(reloadKey);

        pressingAimRight = Input.GetKey(leanRightKey);
        pressingAimLeft = Input.GetKey(leanLeftKey);


        pressingSiphon = Input.GetKey(siphonKey);
        pressingRestart = Input.GetKey(restartKey);
        pressingShift = Input.GetKey(shiftKey);
        UpdateArmActionInput();
    }
}
