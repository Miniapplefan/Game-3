using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProceduralAnimation : MonoBehaviour
{
    public LayerMask proceduralanimationLayerMask;

    /* Some useful functions we may need */

    Vector3[] CastOnSurface(Vector3 point, float halfRange, Vector3 up)
    {
        Vector3[] res = new Vector3[2];
        RaycastHit hit;
        Ray ray = new Ray(new Vector3(point.x, point.y + halfRange, point.z), -up);

        if (Physics.Raycast(ray, out hit, 2f * halfRange, proceduralanimationLayerMask))
        {
            res[0] = hit.point;
            res[1] = hit.normal;
        }
        else
        {
            res[0] = point;
        }
        return res;
    }

    /*************************************/


    public Transform leftFootTarget;
    public Transform rightFootTarget;
    public Transform leftFootTargetRig;
    public Transform rightFootTargetRig;
    public Transform pivot;
    public Transform scaler;

    public float smoothness = 2f;
    public float stepHeight = 0.2f;
    public float stepLength = 1f;
    public float targetStepLength = 0.23f;
    public float angularSpeed = 0.1f;
    public float velocityMultiplier = 80f;
    public float bounceAmplitude = 0.05f;
    public float minFeetDistance = 0.2f;
    public bool running = false;

    private Vector3 initLeftFootPos;
    private Vector3 initRightFootPos;

    private Vector3 lastLeftFootPos;
    private Vector3 lastRightFootPos;

    private Vector3 lastBodyPos;
    private Vector3 initBodyPos;

    private Vector3 velocity;
    private Vector3 lastVelocity;

    private Vector3 stop;

    private enum FootState
    {
        Grounded,
        InAir
    }

    private FootState leftFootState = FootState.Grounded;
    private FootState rightFootState = FootState.Grounded;

    private string lastFootCase = "";
    private int lastSign = 0;
    private float lastZ = 1;
    private bool backwards = false;

    // Start is called before the first frame update
    void Start()
    {
        initLeftFootPos = leftFootTarget.localPosition;
        initRightFootPos = rightFootTarget.localPosition;

        lastLeftFootPos = leftFootTarget.position;
        lastRightFootPos = rightFootTarget.position;

        lastBodyPos = transform.position;
        initBodyPos = transform.localPosition;
    }

    string signOfNum(float n)
    {
        if (n > 0f) return "+";
        else return "-";

    }

    // Update is called once per frame
    void FixedUpdate()
    {
        velocity = transform.position - lastBodyPos;
        velocity *= velocityMultiplier;
        velocity = (velocity + smoothness * lastVelocity) / (smoothness + 1f);

        if (velocity.magnitude < 0.000025f * velocityMultiplier)
            velocity = lastVelocity;
        lastVelocity = velocity;

        int sign = lastSign;
        //&& Mathf.Abs(velocity.z) > 0f
        //Debug.Log(Vector3.Dot(velocity.normalized, transform.forward));
        sign = (Vector3.Dot(velocity.normalized, transform.forward) < 0 ? -1 : 1);
        //Debug.Log(Vector3.Dot(velocity.normalized, transform.forward));
        if (Mathf.Abs(Vector3.Dot(velocity.normalized, transform.forward)) < 0.7f)
        {
            sign = 1;
        }
        //Debug.Log("x:" + signOfNum(velocity.x) + " z:" + signOfNum(velocity.z) + " lastZ:" + signOfNum(lastZ));
        //if (velocity.x > 1 || velocity.x < -1)
        //{
        //    sign = 1;
        //}
        //if (velocity.z > 1 || velocity.z < -1)
        //{
        //    sign = (Vector3.Dot(velocity.normalized, transform.forward) < 0 ? -1 : 1);
        //}
        lastSign = sign;

        //Debug.Log(velocity.magnitude);
        scaler.localScale = new Vector3(scaler.localScale.x, stepHeight * 2f * 7.7f, stepLength * 7.7f);

        scaler.rotation = Quaternion.LookRotation(sign * velocity.normalized, Vector3.up);
        if (velocity.magnitude > 1)
        {
            pivot.Rotate(Vector3.right, sign * angularSpeed, Space.Self);
        }

        stepLength = Mathf.Lerp(stepLength, (velocity.magnitude / 7.7f) * targetStepLength, velocity.magnitude);

        if (velocity.magnitude < 1)
        {
            stepLength = Mathf.Lerp(stepLength, targetStepLength, 0.5f);
        }

        if (velocity.magnitude < 1 && (leftFootTargetRig.localPosition.y > 0.2f || rightFootTargetRig.localPosition.y > 0.2f) && lastFootCase == "")
        {
            if (leftFootTargetRig.localPosition.y > 0.2f && leftFootTargetRig.localPosition.z >= rightFootTargetRig.localPosition.z)
            {
                pivot.Rotate(Vector3.right, sign * angularSpeed / 2, Space.Self);
                lastFootCase = "A";
            }
            else if (rightFootTargetRig.localPosition.y > 0.2f && rightFootTargetRig.localPosition.z >= leftFootTargetRig.localPosition.z)
            {
                pivot.Rotate(Vector3.right, sign * angularSpeed / 2, Space.Self);
                lastFootCase = "B";
            }
            else if (leftFootTargetRig.localPosition.y > 0.2f && leftFootTargetRig.localPosition.z <= rightFootTargetRig.localPosition.z)
            {
                pivot.Rotate(Vector3.right, -sign * angularSpeed / 2, Space.Self);
                lastFootCase = "C";
            }
            else if (rightFootTargetRig.localPosition.y > 0.2f && rightFootTargetRig.localPosition.z <= leftFootTargetRig.localPosition.z)
            {
                pivot.Rotate(Vector3.right, -sign * angularSpeed / 2, Space.Self);
                lastFootCase = "D";
            }
        }

        // if (velocity.magnitude < 1
        //     && (
        //     Vector3.Distance(leftFootTargetRig.position, rightFootTarget.position) <
        //     Vector3.Distance(rightFootTargetRig.position, rightFootTarget.position)
        //     ||
        //     Vector3.Distance(rightFootTargetRig.position, leftFootTarget.position) <
        //     Vector3.Distance(leftFootTargetRig.position, leftFootTarget.position)))
        // {
        //     //Transform tempTarget = leftFootTarget;
        //     //leftFootTarget = rightFootTarget;
        //     //rightFootTarget = tempTarget;
        //     //Vector3.Dot(pivot.localScale, new Vector3(-1, -1, 1));
        // }

        Vector3 desiredPositionLeft = leftFootTarget.position;
        Vector3 desiredPositionRight = rightFootTarget.position;

        if (leftFootTargetRig.localPosition.y < 0.2)
        {
            leftFootState = FootState.Grounded;
        }
        else
        {
            leftFootState = FootState.InAir;
        }

        if (rightFootTargetRig.localPosition.y < 0.2)
        {
            rightFootState = FootState.Grounded;
        }
        else
        {
            rightFootState = FootState.InAir;
        }

        Vector3[] posNormLeft = CastOnSurface(desiredPositionLeft, 2f, Vector3.up);
        //if (posNormLeft[0].y > desiredPositionLeft.y)
        //{
        //    leftFootTargetRig.position = posNormLeft[0];
        //}
        //else
        //{
        //    leftFootTargetRig.position = desiredPositionLeft;
        //}
        if (posNormLeft[0].y > desiredPositionLeft.y)
        {
            if (leftFootTarget.localPosition.y > 0)
            {
                leftFootTargetRig.position = new Vector3(posNormLeft[0].x, posNormLeft[0].y, desiredPositionLeft.z);
            }
            else
            {
                leftFootTargetRig.position = lastLeftFootPos;
            }
        }
        else
        {
            leftFootTargetRig.position = desiredPositionLeft;
        }
        if (posNormLeft[1] != Vector3.zero)
        {
            leftFootTargetRig.rotation = Quaternion.LookRotation(sign * velocity.normalized, posNormLeft[1]);
        }

        Vector3[] posNormRight = CastOnSurface(desiredPositionRight, 2f, Vector3.up);
        //if (posNormRight[0].y > desiredPositionRight.y)
        //{
        //    rightFootTargetRig.position = posNormRight[0];
        //}
        //else
        //{
        //    rightFootTargetRig.position = desiredPositionRight;
        //}
        if (posNormRight[0].y > desiredPositionRight.y)
        {
            if (rightFootTarget.localPosition.y > 0 || velocity.magnitude < 1)
            {
                rightFootTargetRig.position = new Vector3(posNormRight[0].x, posNormRight[0].y, desiredPositionRight.z);
            }
            else
            {
                rightFootTargetRig.position = lastRightFootPos;
            }
        }
        else
        {
            rightFootTargetRig.position = desiredPositionRight;
        }
        if (posNormRight[1] != Vector3.zero)
        {
            rightFootTargetRig.rotation = Quaternion.LookRotation(sign * velocity.normalized, posNormRight[1]);
        }

        lastLeftFootPos = leftFootTargetRig.position;
        lastRightFootPos = rightFootTargetRig.position;
        float feetDistance = Mathf.Clamp01(Mathf.Abs(leftFootTargetRig.localPosition.z - rightFootTargetRig.localPosition.z) / (stepLength / 4f));

        //if (velocity.magnitude > 0.000025f * velocityMultiplier || feetDistance < minFeetDistance)
        //{
        //    float heightReduction = (running ? bounceAmplitude - bounceAmplitude * Mathf.Clamp01(velocity.magnitude) * feetDistance : bounceAmplitude * Mathf.Clamp01(velocity.magnitude) * feetDistance);
        //    transform.localPosition = initBodyPos - heightReduction * Vector3.up;
        //    scaler.localPosition = new Vector3(0f, heightReduction, 0f);
        //}

        if (lastFootCase == "A" || lastFootCase == "B")
        {
            pivot.Rotate(Vector3.right, sign * angularSpeed / 4, Space.Self);
            if (leftFootTargetRig.localPosition.y < 0.2f && rightFootTargetRig.localPosition.y < 0.2f)
            {
                lastFootCase = "";
            }
        }
        if (lastFootCase == "C" || lastFootCase == "D")
        {
            pivot.Rotate(Vector3.right, -sign * angularSpeed / 4, Space.Self);
            if (leftFootTargetRig.localPosition.y < 0.2f && rightFootTargetRig.localPosition.y < 0.2f)
            {
                lastFootCase = "";
            }
        }


        lastBodyPos = transform.position;
    }


    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(leftFootTarget.position, 0.2f);
        Gizmos.DrawWireSphere(rightFootTarget.position, 0.2f);
    }
}


// HEY!
// To solve the jankiness when you switch from strafing left to right, add a check
// that when the player's velocity is near zero (they are switching directions) and
// the current position of their left foot is closer to the right foot target, and
// the current position of their right foot is closer to the left foot target,
// swap the targets of the feet.

// thought of this at 3:46 am but was too sleepy to implement it so get to it!