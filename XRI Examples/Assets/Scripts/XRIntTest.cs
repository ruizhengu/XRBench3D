using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class XRIntTest : MonoBehaviour
{
    [Header("Configuration")]
    public float GameSpeed = 2.0f;
    public float MoveSpeed = 1.0f;
    public float RotateSpeed = 1.0f;
    public float InteractionDistance = 2.0f; // Distance to stop navigation
    public float TimeBudget = 600f; // 10 minutes
    public float ReportInterval = 30f;

    [Header("References")]
    public GameObject RightController;

    // State
    public List<Utils.InteractableObject> InteractableObjects { get; private set; }
    public bool HasCurrentInteractionGrabbed { get; private set; }
    public bool HasCurrentInteractionTriggered { get; private set; }
    
    private XRInputManager inputManager;
    private int interactionCount;
    private float startTime;
    private float reportTimer;
    private float minuteCount = 0.5f;
    private Utils.InteractableObject currentTarget;

    void Start()
    {
        // Initialize Utils and Data
        InteractableObjects = Utils.GetInteractableObjects();
        interactionCount = Utils.GetInteractableEventsCount(InteractableObjects);
        Utils.FindSimulatedDevices();

        // Initialize Input Manager
        inputManager = gameObject.AddComponent<XRInputManager>();
        // RightController might be null at Start if it's dynamically spawned, usually found by name
        if (RightController == null) RightController = GameObject.Find("Right Controller");
        inputManager.Initialize(RightController);

        // Register Event Listeners
        RegisterListeners();

        startTime = Time.time;
        
        // Start Main Loop
        StartCoroutine(TestLoop());
    }

    void FixedUpdate()
    {
        Time.timeScale = GameSpeed;
        
        // Reporting and Time Budget
        HandleReporting();
        CheckTimeBudget();
    }

    private IEnumerator TestLoop()
    {
        while (enabled)
        {
            // 1. Check End Condition
            if (CheckAllAttempted())
            {
                FinishTest();
                yield break;
            }

            // 2. Select Target
            currentTarget = GetClosestUnattemptedInteractable();
            if (currentTarget == null)
            {
                // Should not happen if CheckAllAttempted is false, but safe guard
                yield return new WaitForSeconds(1f);
                continue;
            }

            // 3. Navigation (Move Player)
            yield return NavigateToTarget(currentTarget.Interactable);

            // 4. Select Interaction
            var interactionInfo = currentTarget.Interactions.FirstOrDefault(i => !i.Attempted);
            if (interactionInfo == null) continue;

            Debug.Log($"Starting Interaction: {currentTarget.Name} - {interactionInfo.Type}");

            // 5. Reset Interaction State Flags
            HasCurrentInteractionGrabbed = false;
            HasCurrentInteractionTriggered = false;
            
            // 6. Move Controller to Object
            yield return inputManager.ResetControllerPosition(); // Reset before moving
            yield return MoveControllerTo(currentTarget.Interactable);
            
            // Mark Visited/Intersected
            currentTarget.Visited = true;
            currentTarget.Intersected = Utils.GetIntersected(currentTarget.Interactable, inputManager.RightController);

            // 7. Execute Interaction Strategy
            IInteractionStrategy strategy = InteractionStrategyFactory.GetStrategy(interactionInfo.Type);
            if (strategy != null)
            {
                yield return strategy.Execute(this, inputManager, currentTarget, interactionInfo);
            }
            else
            {
                Debug.LogWarning($"Unknown interaction type: {interactionInfo.Type}");
                interactionInfo.Attempted = true;
            }

            // 8. Cleanup / Post-Interaction
            inputManager.StopHoldingGrab(); // Ensure keys are released
            yield return new WaitForSeconds(0.1f);
        }
    }

    // --- Navigation ---

    private IEnumerator NavigateToTarget(GameObject target)
    {
        while (true)
        {
            Vector3 currentPos = transform.position;
            Vector3 targetPos = target.transform.position;

            // 1. Rotation
            Vector3 targetDirection = (targetPos - currentPos).normalized;
            targetDirection.y = 0;
            if (targetDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                // Simple check if we need to rotate
                if (Quaternion.Angle(transform.rotation, targetRotation) > 5.0f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, RotateSpeed * Time.deltaTime);
                    yield return null; 
                    continue; // Keep rotating
                }
            }

            // 2. Position
            Vector3 flatCurrent = new Vector3(currentPos.x, 0, currentPos.z);
            Vector3 flatTarget = new Vector3(targetPos.x, 0, targetPos.z);
            float dist = Vector3.Distance(flatCurrent, flatTarget);
            float requiredDist = Utils.GetInteractionDistance(); // Or use InteractionDistance field

            if (dist > requiredDist)
            {
                transform.position = Vector3.MoveTowards(currentPos, new Vector3(targetPos.x, currentPos.y, targetPos.z), MoveSpeed * Time.deltaTime);
                yield return null;
            }
            else
            {
                // Arrived
                break;
            }
        }
    }

    // --- Controller Movement ---

    public IEnumerator MoveControllerTo(GameObject target)
    {
        // Refresh controller reference if needed
        if (inputManager.RightController == null)
        {
            inputManager.Initialize(GameObject.Find("Right Controller"));
        }

        yield return inputManager.SwitchControllerState(XRInputManager.ControllerState.RightController);

        // Loop until close enough
        while (true)
        {
            Vector3 controllerPos = inputManager.RightController.transform.position;
            Vector3 targetPos = target.transform.position;
            float dist = Vector3.Distance(controllerPos, targetPos);

            if (dist > inputManager.ControllerMovementThreshold)
            {
                // Use simple direction vector in world space
                Vector3 direction = targetPos - controllerPos;
                inputManager.MoveControllerInDirection(direction.normalized);
                yield return null;
            }
            else
            {
                break;
            }
        }
    }

    // --- Helpers ---

    private Utils.InteractableObject GetClosestUnattemptedInteractable()
    {
        Utils.InteractableObject closest = null;
        float minDistance = Mathf.Infinity;
        Vector3 currentPos = transform.position;

        foreach (var obj in InteractableObjects)
        {
            if (obj.Interactions.Any(i => !i.Attempted))
            {
                float dist = Vector3.Distance(currentPos, obj.Interactable.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closest = obj;
                }
            }
        }
        return closest;
    }

    private bool CheckAllAttempted()
    {
        return InteractableObjects.All(obj => obj.Interactions.All(i => i.Attempted));
    }

    private void FinishTest()
    {
        LogStatus();
        Debug.Log($"Test End: execution time {Time.time - startTime}s");
        int currentActivated = Utils.CountInteracted(InteractableObjects, true);
        Debug.Log($"Number of Activated Interactions: {currentActivated} / {interactionCount}");
        this.enabled = false;
    }

    private void HandleReporting()
    {
        reportTimer += Time.deltaTime;
        if (reportTimer >= ReportInterval)
        {
            int currentInteracted = Utils.CountInteracted(InteractableObjects);
            Debug.Log($"Current Interacted {minuteCount}m: {currentInteracted} / {interactionCount}");
            minuteCount += 0.5f;
            reportTimer = 0f;
        }
    }

    private void CheckTimeBudget()
    {
        if (Time.time - startTime >= TimeBudget)
        {
            Debug.Log("Time budget exceeded.");
            FinishTest();
        }
    }

    private void LogStatus()
    {
        Debug.Log("=== Interaction Status Report ===");
        foreach (var obj in InteractableObjects)
        {
            foreach (var interaction in obj.Interactions)
            {
                 Debug.Log($"Object: {obj.Name} | Interaction: {interaction} | Grabbed: {obj.Grabbed} | Socketed: {obj.Socketed}");
            }
        }
        Debug.Log("=================================");
    }

    // --- Event Listeners ---

    private void RegisterListeners()
    {
        foreach (var obj in InteractableObjects)
        {
            var baseInteractable = obj.Interactable.GetComponent<XRBaseInteractable>();
            if (baseInteractable != null)
            {
                baseInteractable.selectEntered.AddListener(OnSelectEntered);
                baseInteractable.activated.AddListener(OnActivated);
            }
        }

        // Register sockets
        var allSockets = FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>(FindObjectsSortMode.None);
        foreach (var socket in allSockets)
        {
            socket.selectEntered.AddListener(OnSocketSnap);
        }
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        string objName = args.interactableObject.transform.name;
        var interactableObj = InteractableObjects.FirstOrDefault(o => o.Interactable.name == objName);
        if (interactableObj != null && currentTarget != null && objName == currentTarget.Interactable.name)
        {
            HasCurrentInteractionGrabbed = true;
            interactableObj.Grabbed = true;
            Debug.Log($"Select Entered: {objName}");
        }
    }

    private void OnActivated(ActivateEventArgs args)
    {
        string objName = args.interactableObject.transform.name;
        var interactableObj = InteractableObjects.FirstOrDefault(o => o.Interactable.name == objName);
        if (interactableObj != null && currentTarget != null && objName == currentTarget.Interactable.name)
        {
            HasCurrentInteractionTriggered = true;
            Debug.Log($"Activated: {objName}");
        }
    }

    private void OnSocketSnap(SelectEnterEventArgs args)
    {
        string objName = args.interactableObject.transform.name;
        var interactableObj = InteractableObjects.FirstOrDefault(o => o.Interactable.name == objName);
        if (interactableObj != null)
        {
            interactableObj.Socketed = true;
            Debug.Log($"Socket Snap: {objName} -> {args.interactorObject.transform.name}");
        }
    }
}
