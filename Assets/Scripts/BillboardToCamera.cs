using UnityEngine;

[DisallowMultipleComponent]
public class BillboardToCamera : MonoBehaviour
{
    [Tooltip("Optional transform for the billboard to face. If left empty, the main camera or the player's active camera is used.")]
    public Transform viewTransform;

    void Awake()
    {
        ResolveViewTransform();
    }

    void LateUpdate()
    {
        if (viewTransform == null)
        {
            ResolveViewTransform();
        }

        if (viewTransform != null)
        {
            transform.LookAt(viewTransform.position, Vector3.up);
        }
    }

    void ResolveViewTransform()
    {
        if (viewTransform != null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            viewTransform = mainCamera.transform;
            return;
        }

        PlayerController playerController = FindObjectOfType<PlayerController>();
        if (playerController == null)
        {
            return;
        }

        Camera fallbackCamera = null;
        Camera[] playerCameras = playerController.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < playerCameras.Length; i++)
        {
            Camera playerCamera = playerCameras[i];
            if (fallbackCamera == null)
            {
                fallbackCamera = playerCamera;
            }

            if (playerCamera.enabled && playerCamera.gameObject.activeInHierarchy)
            {
                viewTransform = playerCamera.transform;
                return;
            }
        }

        if (fallbackCamera != null)
        {
            viewTransform = fallbackCamera.transform;
        }
    }
}
