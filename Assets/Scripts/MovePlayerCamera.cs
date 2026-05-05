using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovePlayerCamera : MonoBehaviour
{
    public Transform player;
    public Transform playerL;

    public BodyController bodyController;
    private bool hasFrozenRotation = false;
    private Quaternion frozenRotation;
    private bool isDetachedForMoveAim = false;
    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 frozenPositionOffset;
    private bool standbyAnchorBlendActive = false;
    private float standbyAnchorBlendStartTime = 0f;
    private float standbyAnchorBlendDuration = 0f;
    private Vector3 standbyAnchorBlendStartPos;
    private Quaternion standbyAnchorBlendStartRot;
    private Vector3 standbyAnchorBlendTargetPos;
    private Quaternion standbyAnchorBlendTargetRot;
    private bool standbyAnchorBlendTargetLeft = false;
    private bool hasPreviousAnchorState = false;
    private bool previousUseLeftAnchor = false;
    private bool previousWasStandbyNoArm = false;

    void LateUpdate()
    {
        ApplyCamera();
    }

    void OnPreCull()
    {
        ApplyCamera();
    }

    public void ApplyCameraImmediate()
    {
        ApplyCamera();
    }

    private void ApplyCamera()
    {
        if (bodyController != null && bodyController.IsMoveAimYawInProgress)
        {
            if (bodyController.HasFrozenCameraRotation)
            {
                frozenRotation = bodyController.FrozenCameraRotation;
                hasFrozenRotation = true;
            }
            else if (!hasFrozenRotation)
            {
                frozenRotation = transform.rotation;
                hasFrozenRotation = true;
            }

            Transform follow = bodyController.MoveAimYawSourceIsLeft ? playerL : player;
            if (!isDetachedForMoveAim)
            {
                originalParent = transform.parent;
                originalLocalPosition = transform.localPosition;
                originalLocalRotation = transform.localRotation;
                transform.SetParent(null, true);
                isDetachedForMoveAim = true;
                if (follow != null)
                {
                    frozenPositionOffset = transform.position - follow.position;
                }
            }

            if (follow != null)
            {
                transform.position = follow.position + frozenPositionOffset;
            }
            transform.rotation = frozenRotation;
            standbyAnchorBlendActive = false;
            hasPreviousAnchorState = false;
            previousWasStandbyNoArm = false;
            return;
        }

        hasFrozenRotation = false;
        if (isDetachedForMoveAim)
        {
            transform.SetParent(originalParent, true);
            transform.localPosition = originalLocalPosition;
            transform.localRotation = originalLocalRotation;
            isDetachedForMoveAim = false;
        }

        if (bodyController == null)
        {
            standbyAnchorBlendActive = false;
            hasPreviousAnchorState = false;
            return;
        }

        bool useLeftAnchor = bodyController.isAimingLeft
            || (bodyController.KeepCameraAimWithoutArm && bodyController.KeepCameraAimUsesLeft);
        Transform targetAnchor = useLeftAnchor ? playerL : player;
        if (targetAnchor == null)
        {
            standbyAnchorBlendActive = false;
            hasPreviousAnchorState = false;
            return;
        }

        bool isStandbyNoArm = bodyController.KeepCameraAimWithoutArm
            && !bodyController.isAimingRight
            && !bodyController.isAimingLeft;

        if (isStandbyNoArm)
        {
            if (standbyAnchorBlendActive && standbyAnchorBlendTargetLeft != useLeftAnchor)
            {
                BeginStandbyAnchorBlend(useLeftAnchor, targetAnchor);
            }

            if (!standbyAnchorBlendActive
                && hasPreviousAnchorState
                && previousWasStandbyNoArm
                && previousUseLeftAnchor != useLeftAnchor)
            {
                BeginStandbyAnchorBlend(useLeftAnchor, targetAnchor);
            }

            if (standbyAnchorBlendActive)
            {
                ApplyStandbyAnchorBlend(targetAnchor);
            }
            else
            {
                transform.position = targetAnchor.position;
                transform.rotation = targetAnchor.rotation;
            }
        }
        else
        {
            standbyAnchorBlendActive = false;
            transform.position = targetAnchor.position;
            transform.rotation = targetAnchor.rotation;
        }

        hasPreviousAnchorState = true;
        previousUseLeftAnchor = useLeftAnchor;
        previousWasStandbyNoArm = isStandbyNoArm;
    }

    private void BeginStandbyAnchorBlend(bool useLeftAnchor, Transform targetAnchor)
    {
        if (targetAnchor == null)
        {
            standbyAnchorBlendActive = false;
            return;
        }

        float duration = bodyController != null ? Mathf.Max(0f, bodyController.aimSwapDuration) : 0f;
        if (duration <= 0f)
        {
            standbyAnchorBlendActive = false;
            transform.position = targetAnchor.position;
            transform.rotation = targetAnchor.rotation;
            return;
        }

        standbyAnchorBlendActive = true;
        standbyAnchorBlendStartTime = Time.time;
        standbyAnchorBlendDuration = duration;
        standbyAnchorBlendStartPos = transform.position;
        standbyAnchorBlendStartRot = transform.rotation;
        standbyAnchorBlendTargetPos = targetAnchor.position;
        standbyAnchorBlendTargetRot = targetAnchor.rotation;
        standbyAnchorBlendTargetLeft = useLeftAnchor;
    }

    private void ApplyStandbyAnchorBlend(Transform targetAnchor)
    {
        if (targetAnchor == null)
        {
            standbyAnchorBlendActive = false;
            return;
        }

        standbyAnchorBlendTargetPos = targetAnchor.position;
        standbyAnchorBlendTargetRot = targetAnchor.rotation;

        float t = standbyAnchorBlendDuration > 0f
            ? Mathf.Clamp01((Time.time - standbyAnchorBlendStartTime) / standbyAnchorBlendDuration)
            : 1f;
        AnimationCurve curve = bodyController != null ? bodyController.aimSwapCurve : null;
        float curvedT = curve != null ? curve.Evaluate(t) : t;

        transform.position = Vector3.Lerp(standbyAnchorBlendStartPos, standbyAnchorBlendTargetPos, curvedT);
        transform.rotation = Quaternion.Slerp(standbyAnchorBlendStartRot, standbyAnchorBlendTargetRot, curvedT);

        if (t >= 1f)
        {
            standbyAnchorBlendActive = false;
        }
    }

    private void FixedUpdate()
    {
        //transform.rotation = player.transform.localRotation;
    }
}
