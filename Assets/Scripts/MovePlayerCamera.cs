using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovePlayerCamera : MonoBehaviour
{
    public Transform player;
    public Transform playerL;

    public BodyController bodyController;

    void LateUpdate()
    {
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
