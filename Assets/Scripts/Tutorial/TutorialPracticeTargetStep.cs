using System.Collections.Generic;
using TMPro;
using UnityEngine;

[System.Serializable]
public class TutorialPracticeTargetSpawn
{
    public GameObject targetPrefab;
    public Transform spawnPoint;
}

[AddComponentMenu("Tutorial/Tutorial Practice Target Step")]
public class TutorialPracticeTargetStep : MonoBehaviour
{
    [Header("Targets")]
    public List<TutorialPracticeTargetSpawn> targetSpawns = new List<TutorialPracticeTargetSpawn>();

    [Header("Inputs")]
    public List<TutorialStepInputAction> requiredInputs = new List<TutorialStepInputAction>();

    [Header("Flow")]
    public GameObject nextStepObject;
    public bool deactivateSelfOnComplete = true;
    public float retryDelaySeconds = 0.25f;

    [Header("Tutorial Text")]
    public TMP_Text tutorialText;
    [TextArea]
    public string tutorialTextValue;

    readonly HashSet<TutorialStepInputAction> remainingInputs = new HashSet<TutorialStepInputAction>();
    readonly HashSet<PracticeTarget> remainingTargets = new HashSet<PracticeTarget>();
    readonly HashSet<BodyController> remainingBodyTargets = new HashSet<BodyController>();
    readonly List<PracticeTarget> subscribedTargets = new List<PracticeTarget>();
    readonly List<BodyController> subscribedBodyTargets = new List<BodyController>();
    readonly List<GameObject> spawnedTargetObjects = new List<GameObject>();

    InputController playerInput;
    bool hasCompleted;
    int trackedTargetCount;
    Coroutine retryCoroutine;

    void OnEnable()
    {
        ResetStepState();
        ResolvePlayerInput();
        ApplyTutorialText();
        ActivateTrackedTargets();
        TryCompleteStep();
    }

    void OnDisable()
    {
        StopRetryCoroutine();
        UnsubscribeFromTargets();
        DestroySpawnedTargets();
    }

    void Update()
    {
        if (hasCompleted || retryCoroutine != null)
        {
            return;
        }

        if (playerInput == null)
        {
            ResolvePlayerInput();
        }

        if (playerInput == null || remainingInputs.Count == 0)
        {
            return;
        }

        bool changed = false;
        TutorialStepInputAction[] pendingInputs = new TutorialStepInputAction[remainingInputs.Count];
        remainingInputs.CopyTo(pendingInputs);

        for (int i = 0; i < pendingInputs.Length; i++)
        {
            TutorialStepInputAction action = pendingInputs[i];
            if (!IsInputSatisfied(action))
            {
                continue;
            }

            changed |= remainingInputs.Remove(action);
        }

        if (changed)
        {
            TryCompleteStep();
        }
    }

    void ResetStepState()
    {
        hasCompleted = false;
        StopRetryCoroutine();
        ResetInputProgress();
        remainingTargets.Clear();
        remainingBodyTargets.Clear();
        UnsubscribeFromTargets();
        DestroySpawnedTargets();
        trackedTargetCount = 0;
    }

    void ResetInputProgress()
    {
        remainingInputs.Clear();

        for (int i = 0; i < requiredInputs.Count; i++)
        {
            remainingInputs.Add(requiredInputs[i]);
        }
    }

    void ResolvePlayerInput()
    {
        PlayerController playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerInput = playerController;
        }
    }

    void ApplyTutorialText()
    {
        if (tutorialText != null)
        {
            tutorialText.text = tutorialTextValue;
        }
    }

    void ActivateTrackedTargets()
    {
        trackedTargetCount = 0;

        for (int i = 0; i < targetSpawns.Count; i++)
        {
            TutorialPracticeTargetSpawn spawn = targetSpawns[i];
            if (spawn == null || spawn.targetPrefab == null || spawn.spawnPoint == null)
            {
                Debug.LogWarning($"TutorialPracticeTargetStep on {name} has an incomplete target spawn entry at index {i}.", this);
                continue;
            }

            GameObject spawnedObject = Instantiate(
                spawn.targetPrefab,
                spawn.spawnPoint.position,
                spawn.spawnPoint.rotation);
            spawnedTargetObjects.Add(spawnedObject);

            BodyController bodyTarget = spawnedObject.GetComponentInChildren<BodyController>(true);
            if (bodyTarget != null)
            {
                if (remainingBodyTargets.Add(bodyTarget))
                {
                    trackedTargetCount++;
                    bodyTarget.Died += HandleBodyTargetDied;
                    subscribedBodyTargets.Add(bodyTarget);
                }

                continue;
            }

            PracticeTarget practiceTarget = spawnedObject.GetComponentInChildren<PracticeTarget>(true);
            if (practiceTarget == null)
            {
                Debug.LogWarning($"TutorialPracticeTargetStep on {name} could not find a PracticeTarget or BodyController under spawned prefab {spawn.targetPrefab.name}.", this);
                continue;
            }

            if (remainingTargets.Add(practiceTarget))
            {
                trackedTargetCount++;
                practiceTarget.DestroyedByPlayer += HandleTargetDestroyedByPlayer;
                subscribedTargets.Add(practiceTarget);
            }
        }
    }

    void UnsubscribeFromTargets()
    {
        for (int i = 0; i < subscribedTargets.Count; i++)
        {
            PracticeTarget practiceTarget = subscribedTargets[i];
            if (practiceTarget != null)
            {
                practiceTarget.DestroyedByPlayer -= HandleTargetDestroyedByPlayer;
            }
        }

        for (int i = 0; i < subscribedBodyTargets.Count; i++)
        {
            BodyController bodyTarget = subscribedBodyTargets[i];
            if (bodyTarget != null)
            {
                bodyTarget.Died -= HandleBodyTargetDied;
            }
        }

        subscribedTargets.Clear();
        subscribedBodyTargets.Clear();
    }

    void DestroySpawnedTargets()
    {
        for (int i = 0; i < spawnedTargetObjects.Count; i++)
        {
            GameObject spawnedObject = spawnedTargetObjects[i];
            if (spawnedObject != null)
            {
                Destroy(spawnedObject);
            }
        }

        spawnedTargetObjects.Clear();
    }

    void HandleTargetDestroyedByPlayer(PracticeTarget practiceTarget)
    {
        if (hasCompleted)
        {
            return;
        }

        if (practiceTarget != null)
        {
            practiceTarget.DestroyedByPlayer -= HandleTargetDestroyedByPlayer;
            subscribedTargets.Remove(practiceTarget);
            remainingTargets.Remove(practiceTarget);
        }

        TryCompleteStep();
    }

    void HandleBodyTargetDied(BodyController bodyTarget)
    {
        if (hasCompleted)
        {
            return;
        }

        if (bodyTarget != null)
        {
            bodyTarget.Died -= HandleBodyTargetDied;
            subscribedBodyTargets.Remove(bodyTarget);
            remainingBodyTargets.Remove(bodyTarget);
        }

        TryCompleteStep();
    }

    void RestartTargetAttempt()
    {
        if (retryCoroutine != null)
        {
            return;
        }

        retryCoroutine = StartCoroutine(RestartTargetAttemptAfterDelay());
    }

    System.Collections.IEnumerator RestartTargetAttemptAfterDelay()
    {
        float delay = Mathf.Max(0f, retryDelaySeconds);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        retryCoroutine = null;
        ResetInputProgress();
        remainingTargets.Clear();
        remainingBodyTargets.Clear();
        UnsubscribeFromTargets();
        DestroySpawnedTargets();
        ActivateTrackedTargets();
    }

    void StopRetryCoroutine()
    {
        if (retryCoroutine == null)
        {
            return;
        }

        StopCoroutine(retryCoroutine);
        retryCoroutine = null;
    }

    bool IsInputSatisfied(TutorialStepInputAction action)
    {
        switch (action)
        {
            case TutorialStepInputAction.AimLeft:
                return playerInput.getAimLeft();
            case TutorialStepInputAction.AimRight:
                return playerInput.getAimRight();
            case TutorialStepInputAction.Fire1:
                return playerInput.getFire1() || playerInput.getFire1Down();
            case TutorialStepInputAction.Fire2:
                return playerInput.getFire2() || playerInput.getFire2Down();
            case TutorialStepInputAction.Reload:
                return playerInput.getReload();
            case TutorialStepInputAction.Siphon:
                return playerInput.getSiphon();
            case TutorialStepInputAction.ScrollUp:
                return playerInput.getScrollUp();
            case TutorialStepInputAction.ScrollDown:
                return playerInput.getScrollDown();
            case TutorialStepInputAction.AimMiddle:
                return playerInput.getAimMiddle();
            case TutorialStepInputAction.Shift:
                return playerInput.getShift();
            default:
                return false;
        }
    }

    void TryCompleteStep()
    {
        if (hasCompleted)
        {
            return;
        }

        if (trackedTargetCount > 0 && remainingTargets.Count == 0 && remainingBodyTargets.Count == 0)
        {
            if (remainingInputs.Count == 0)
            {
                hasCompleted = true;

                if (nextStepObject != null)
                {
                    nextStepObject.SetActive(true);
                }

                if (deactivateSelfOnComplete)
                {
                    gameObject.SetActive(false);
                }
            }
            else
            {
                RestartTargetAttempt();
            }

            return;
        }

        if (remainingInputs.Count > 0 || remainingTargets.Count > 0 || remainingBodyTargets.Count > 0)
        {
            return;
        }

        hasCompleted = true;

        if (nextStepObject != null)
        {
            nextStepObject.SetActive(true);
        }

        if (deactivateSelfOnComplete)
        {
            gameObject.SetActive(false);
        }
    }
}
