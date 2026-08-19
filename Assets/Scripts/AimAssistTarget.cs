using UnityEngine;

public class AimAssistTarget : MonoBehaviour, IEnemyPoolResettable
{
    [Tooltip("Optional point used for aim assist targeting and indicator placement.")]
    public Transform targetPoint;

    [Tooltip("Optional collider used when no target point is assigned.")]
    public Collider targetCollider;

    [Tooltip("Optional indicator object shown while the player is previewing this target.")]
    public GameObject aimIndicator;

    [Tooltip("If enabled, the indicator rotates to face the player's view while visible.")]
    public bool faceIndicatorToPlayer = true;

    [Tooltip("If disabled, this target will not participate in aim assist previewing.")]
    public bool isTargetable = true;

    [Tooltip("If enabled, this target stops being targetable when an attached BodyState is dead.")]
    public bool disableWhenBodyStateDead = true;

    BodyState bodyState;
    BodyController playerBodyController;
    Transform playerViewTransform;
    bool aimIndicatorVisible;

    void Awake()
    {
        bodyState = GetComponent<BodyState>();
        if (bodyState == null)
        {
            bodyState = GetComponentInChildren<BodyState>();
        }

        SetAimIndicatorActive(false);
        ResolvePlayerBodyController();
    }

    void LateUpdate()
    {
        if (playerBodyController == null)
        {
            ResolvePlayerBodyController();
        }

        bool shouldShow = playerBodyController != null
            && playerBodyController.BreakoutAimAssistPreviewTarget == this
            && IsAvailable();
        SetAimIndicatorActive(shouldShow);

        if (shouldShow)
        {
            UpdateIndicatorFacing();
        }
    }

    public bool IsAvailable()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy || !isTargetable)
        {
            return false;
        }

        if (disableWhenBodyStateDead && bodyState != null && bodyState.isDead)
        {
            return false;
        }

        return true;
    }

    public Vector3 GetAimPoint(Collider fallbackCollider = null)
    {
        if (targetPoint != null)
        {
            return targetPoint.position;
        }

        if (targetCollider != null)
        {
            return targetCollider.bounds.center;
        }

        if (fallbackCollider != null)
        {
            return fallbackCollider.bounds.center;
        }

        return transform.position;
    }

    public Transform GetTargetRoot()
    {
        return transform.root;
    }

    public void ResetForPoolReuse()
    {
        SetAimIndicatorActive(false);
        bodyState = GetComponent<BodyState>();
        if (bodyState == null)
        {
            bodyState = GetComponentInChildren<BodyState>();
        }

        ResolvePlayerBodyController();
    }

    void ResolvePlayerBodyController()
    {
        PlayerController playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerBodyController = playerController.GetComponent<BodyController>();
        }

        if (Camera.main != null)
        {
            playerViewTransform = Camera.main.transform;
        }
        else if (playerBodyController != null)
        {
            playerViewTransform = playerBodyController.transform;
        }
    }

    void SetAimIndicatorActive(bool active)
    {
        bool activeStateMatches = aimIndicator == null || aimIndicator.activeSelf == active;
        if (aimIndicatorVisible == active && activeStateMatches)
        {
            return;
        }

        aimIndicatorVisible = active;
        if (aimIndicator != null)
        {
            aimIndicator.SetActive(active);
        }
    }

    void UpdateIndicatorFacing()
    {
        if (!faceIndicatorToPlayer || aimIndicator == null)
        {
            return;
        }

        if (playerViewTransform == null)
        {
            ResolvePlayerBodyController();
        }

        if (playerViewTransform == null)
        {
            return;
        }

        aimIndicator.transform.LookAt(playerViewTransform.position, Vector3.up);
    }
}
