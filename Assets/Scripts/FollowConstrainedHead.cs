using UnityEngine;
using UnityEngine.Animations.Rigging;

public class FollowConstrainedHead : MonoBehaviour
{
    public Transform constrainedHead;   // the rig-driven head bone
    public RigBuilder rigBuilder;       // on your character root

    void OnEnable()
    {
        // if (rigBuilder != null) rigBuilder.onPostUpdate += SyncNow;
    }
    void OnDisable()
    {
        // if (rigBuilder != null) rigBuilder.onPostUpdate -= SyncNow;
    }

    void SyncNow()
    {
        transform.SetPositionAndRotation(constrainedHead.position, constrainedHead.rotation);
    }

    // Optional fallback if you don't use rigBuilder:
    void LateUpdate()
    {
        if (rigBuilder == null)
            transform.SetPositionAndRotation(constrainedHead.position, constrainedHead.rotation);
    }
}