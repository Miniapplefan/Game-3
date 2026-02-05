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

    void LateUpdate()
    {
        ApplyCamera();
    }

    void OnPreCull()
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

        if (bodyController.isAimingLeft)
        {
            transform.position = playerL.transform.position;
            transform.rotation = playerL.transform.rotation;
        }
        else
        {
            transform.position = player.transform.position;
            transform.rotation = player.transform.rotation;
        }
    }

    private void FixedUpdate()
    {
        //transform.rotation = player.transform.localRotation;
    }
}
