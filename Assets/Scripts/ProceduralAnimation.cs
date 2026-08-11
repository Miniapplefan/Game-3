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

    [Header("Idle Stance Reorientation")]
    [SerializeField, Min(0f)] private float stopSpeedThreshold = 1f;
    [SerializeField, Min(0f)] private float resumeSpeedThreshold = 1.25f;
    [SerializeField, Min(0f)] private float stopConfirmationTime = 0.15f;
    [SerializeField, Min(0f)] private float idleStanceRotationSpeed = 360f;
    [SerializeField, Min(0f)] private float idleAlignmentTolerance = 1f;

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

    private enum LocomotionState
    {
        Moving,
        ConfirmingStop,
        Reorienting,
        Idle,
        Resuming
    }

    private LocomotionState locomotionState = LocomotionState.Moving;
    private float stopConfirmationTimer;

    private string lastFootCase = "";
    private int lastSign = 1;
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

        Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
        float planarSpeed = planarVelocity.magnitude;
        Vector3 planarDirection = planarSpeed > 0.0001f ? planarVelocity / planarSpeed : Vector3.zero;

        UpdateLocomotionState(planarSpeed);

        int sign = GetTravelSign(planarDirection);
        Quaternion movementRotation;
        bool hasMovementRotation = TryGetMovementRotation(sign, planarDirection, out movementRotation);

        scaler.localScale = new Vector3(scaler.localScale.x, stepHeight * 2f * 7.7f, stepLength * 7.7f);

        if ((locomotionState == LocomotionState.Moving || locomotionState == LocomotionState.ConfirmingStop) && hasMovementRotation)
        {
            scaler.rotation = movementRotation;
        }
        else if (locomotionState == LocomotionState.Resuming && hasMovementRotation)
        {
            scaler.rotation = Quaternion.RotateTowards(
                scaler.rotation,
                movementRotation,
                idleStanceRotationSpeed * Time.fixedDeltaTime);

            if (Quaternion.Angle(scaler.rotation, movementRotation) <= idleAlignmentTolerance)
            {
                scaler.rotation = movementRotation;
                locomotionState = LocomotionState.Moving;
            }
        }

        bool locomotionActive = locomotionState == LocomotionState.Moving
            || locomotionState == LocomotionState.ConfirmingStop
            || locomotionState == LocomotionState.Resuming;

        if (locomotionActive && planarSpeed > stopSpeedThreshold)
        {
            pivot.Rotate(Vector3.right, sign * angularSpeed, Space.Self);
        }

        stepLength = Mathf.Lerp(stepLength, (planarSpeed / 7.7f) * targetStepLength, planarSpeed);

        if (planarSpeed < stopSpeedThreshold)
        {
            stepLength = Mathf.Lerp(stepLength, targetStepLength, 0.5f);
        }

        if (locomotionState == LocomotionState.Reorienting
            && (leftFootTargetRig.localPosition.y > 0.2f || rightFootTargetRig.localPosition.y > 0.2f)
            && lastFootCase == "")
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

        ContinueLandingStep(sign);

        if (locomotionState == LocomotionState.Reorienting || locomotionState == LocomotionState.Idle)
        {
            RotateScalerTowardIdleStance();
        }

        Vector3 desiredPositionLeft = leftFootTarget.position;
        Vector3 desiredPositionRight = rightFootTarget.position;

        Vector3 footForward = hasMovementRotation
            ? movementRotation * Vector3.forward
            : Vector3.ProjectOnPlane(scaler.forward, Vector3.up).normalized;

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
            leftFootTargetRig.rotation = Quaternion.LookRotation(footForward, posNormLeft[1]);
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
            if (rightFootTarget.localPosition.y > 0 || planarSpeed < stopSpeedThreshold)
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
            rightFootTargetRig.rotation = Quaternion.LookRotation(footForward, posNormRight[1]);
        }

        leftFootState = leftFootTargetRig.localPosition.y < 0.2f ? FootState.Grounded : FootState.InAir;
        rightFootState = rightFootTargetRig.localPosition.y < 0.2f ? FootState.Grounded : FootState.InAir;

        lastLeftFootPos = leftFootTargetRig.position;
        lastRightFootPos = rightFootTargetRig.position;
        float feetDistance = Mathf.Clamp01(Mathf.Abs(leftFootTargetRig.localPosition.z - rightFootTargetRig.localPosition.z) / (stepLength / 4f));

        //if (velocity.magnitude > 0.000025f * velocityMultiplier || feetDistance < minFeetDistance)
        //{
        //    float heightReduction = (running ? bounceAmplitude - bounceAmplitude * Mathf.Clamp01(velocity.magnitude) * feetDistance : bounceAmplitude * Mathf.Clamp01(velocity.magnitude) * feetDistance);
        //    transform.localPosition = initBodyPos - heightReduction * Vector3.up;
        //    scaler.localPosition = new Vector3(0f, heightReduction, 0f);
        //}

        if (locomotionState == LocomotionState.Reorienting
            && leftFootState == FootState.Grounded
            && rightFootState == FootState.Grounded
            && lastFootCase == ""
            && GetIdleAlignmentError() <= idleAlignmentTolerance)
        {
            locomotionState = LocomotionState.Idle;
        }

        lastBodyPos = transform.position;
    }

    private void UpdateLocomotionState(float planarSpeed)
    {
        switch (locomotionState)
        {
            case LocomotionState.Moving:
                if (planarSpeed < stopSpeedThreshold)
                {
                    locomotionState = LocomotionState.ConfirmingStop;
                    stopConfirmationTimer = Time.fixedDeltaTime;

                    if (stopConfirmationTimer >= stopConfirmationTime)
                    {
                        locomotionState = LocomotionState.Reorienting;
                    }
                }
                break;

            case LocomotionState.ConfirmingStop:
                if (planarSpeed >= resumeSpeedThreshold)
                {
                    locomotionState = LocomotionState.Moving;
                    stopConfirmationTimer = 0f;
                }
                else if (planarSpeed < stopSpeedThreshold)
                {
                    stopConfirmationTimer += Time.fixedDeltaTime;
                    if (stopConfirmationTimer >= stopConfirmationTime)
                    {
                        locomotionState = LocomotionState.Reorienting;
                    }
                }
                else
                {
                    stopConfirmationTimer = 0f;
                }
                break;

            case LocomotionState.Reorienting:
            case LocomotionState.Idle:
                if (planarSpeed >= resumeSpeedThreshold)
                {
                    locomotionState = LocomotionState.Resuming;
                    stopConfirmationTimer = 0f;
                    lastFootCase = "";
                }
                break;

            case LocomotionState.Resuming:
                if (planarSpeed < stopSpeedThreshold)
                {
                    locomotionState = LocomotionState.ConfirmingStop;
                    stopConfirmationTimer = Time.fixedDeltaTime;
                }
                break;
        }
    }

    private int GetTravelSign(Vector3 planarDirection)
    {
        if (planarDirection.sqrMagnitude < 0.0001f)
        {
            return lastSign;
        }

        float forwardDot = Vector3.Dot(planarDirection, transform.forward);
        int sign = forwardDot < 0f ? -1 : 1;

        if (Mathf.Abs(forwardDot) < 0.7f)
        {
            sign = 1;
        }

        lastSign = sign;
        return sign;
    }

    private bool TryGetMovementRotation(int sign, Vector3 planarDirection, out Quaternion movementRotation)
    {
        Vector3 movementForward = sign * planarDirection;
        if (movementForward.sqrMagnitude < 0.0001f)
        {
            movementRotation = scaler.rotation;
            return false;
        }

        movementRotation = Quaternion.LookRotation(movementForward, Vector3.up);
        return true;
    }

    private void ContinueLandingStep(int sign)
    {
        if (lastFootCase == "A" || lastFootCase == "B")
        {
            pivot.Rotate(Vector3.right, sign * angularSpeed / 4f, Space.Self);
        }
        else if (lastFootCase == "C" || lastFootCase == "D")
        {
            pivot.Rotate(Vector3.right, -sign * angularSpeed / 4f, Space.Self);
        }

        if (lastFootCase != ""
            && leftFootTargetRig.localPosition.y < 0.2f
            && rightFootTargetRig.localPosition.y < 0.2f)
        {
            lastFootCase = "";
        }
    }

    private void RotateScalerTowardIdleStance()
    {
        Vector3 currentFootAxis = Vector3.ProjectOnPlane(
            rightFootTarget.position - leftFootTarget.position,
            Vector3.up);
        Vector3 desiredFootAxis = Vector3.ProjectOnPlane(transform.right, Vector3.up);

        if (currentFootAxis.sqrMagnitude < 0.0001f || desiredFootAxis.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float correctionAngle = Vector3.SignedAngle(currentFootAxis, desiredFootAxis, Vector3.up);
        Quaternion targetRotation = Quaternion.AngleAxis(correctionAngle, Vector3.up) * scaler.rotation;
        float maxDegreesDelta = idleStanceRotationSpeed * Time.fixedDeltaTime;

        scaler.rotation = maxDegreesDelta > 0f
            ? Quaternion.RotateTowards(scaler.rotation, targetRotation, maxDegreesDelta)
            : targetRotation;
    }

    private float GetIdleAlignmentError()
    {
        Vector3 currentFootAxis = Vector3.ProjectOnPlane(
            rightFootTarget.position - leftFootTarget.position,
            Vector3.up);
        Vector3 desiredFootAxis = Vector3.ProjectOnPlane(transform.right, Vector3.up);

        if (currentFootAxis.sqrMagnitude < 0.0001f || desiredFootAxis.sqrMagnitude < 0.0001f)
        {
            return 0f;
        }

        return Mathf.Abs(Vector3.SignedAngle(currentFootAxis, desiredFootAxis, Vector3.up));
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
