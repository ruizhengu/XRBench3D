using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class XRIntTest : MonoBehaviour
{
    public List<Utils.InteractableObject> interactableObjects;
    public Utils.InteractableObject targetInteractable;
    private Utils.InteractionInfo targetInteractionInfo; // Specific interaction to perform
    public int interactionCount = 0;
    public GameObject rightController;
    private float gameSpeed = 2.0f; // May alter gameSpeed to speed up the test execution process
    // Movement parameters
    private float moveSpeed = 1.0f;
    private float rotateSpeed = 1.0f;
    private float updateInterval = 0.001f;
    private float timeSinceLastUpdate = 0f;
    private float interactionAngle = 5.0f; // The angle for transiting from rotation to interaction
    private float controllerMovementThreshold = 0.15f; // The distance of controller movement to continue interaction
    private float stateTransitionDelay = 0.1f; // Delay between state transitions
    private ControllerState currentControllerState = ControllerState.None; // Default state
    private ExplorationState currentExplorationState = ExplorationState.Navigation; // Default state
    private bool isGrabHeld = false; // Track if grab is currently held
    private int grabActionCount = 0; // Track number of grab actions
    private int combinedActionCount = 0; // Track number of combined actions
    private bool waitingForSocketCheck = false; // Flag to pause updates while checking for socket
    private bool isSocketGrabActive = false; // Flag to track if we are holding grab for socket
    private float reportInterval = 30f; // Report interval in seconds
    private float reportTimer = 0f; // Timer for report interval
    private float totalTime = 0f; // Total time of the test
    private float minuteCount = 0.5f;
    private float timeBudget = 600f; // 10 minutes time budget in seconds
    private float startTime; // Time when the program started
    private bool isTimeBudgetExceeded = false; // Flag to track if time budget is exceeded
    
    // Socket interaction support
    private GameObject targetSocket;

    private enum ControllerState // Controller manipulation state
    {
        None,
        LeftController,
        RightController,
        Both,
        HMD
    }
    private enum ExplorationState
    {
        // None,
        Navigation,
        ControllerMovement,
        ThreeDInteraction,
        SocketInteraction
    }

    void Start()
    {
        interactableObjects = Utils.GetInteractableObjects();
        interactionCount = Utils.GetInteractableEventsCount(interactableObjects);
        RegisterListeners();
        Utils.FindSimulatedDevices(); // Find the simulated devices
        startTime = Time.time;
    }

    // Flag to track if we are currently processing an interaction sequence
    private bool isInteracting = false; 
    private bool isActionInProgress = false; // Flag to track if an atomic action is currently executing
    private int currentInteractionRetries = 0;
    private const int MaxRetries = 3;
    private bool hasCurrentInteractionGrabbed = false;
    private bool hasCurrentInteractionTriggered = false;

    private bool isArrivalTriggered = false;
    private bool isTransitioning = false;

    void FixedUpdate()
    {
        Time.timeScale = gameSpeed;
        reportTimer += Time.deltaTime;
        totalTime += Time.deltaTime;
        if (reportTimer >= reportInterval)
        {
            int currentInteracted = Utils.CountInteracted(interactableObjects);
            float currentInteractedPercentage = (float)currentInteracted / (float)interactionCount * 100;
            Debug.Log($"Current Interacted {minuteCount}m: {currentInteracted} / {interactionCount} ({currentInteractedPercentage}%)");
            minuteCount += 0.5f;
            reportTimer = 0f;
        }
        if (!isTimeBudgetExceeded && Time.time - startTime >= timeBudget)
        {
            isTimeBudgetExceeded = true;
            Debug.Log($"Time budget exceeded. Stopping script execution.");
            int currentInteracted = Utils.CountInteracted(interactableObjects, true);
            float currentInteractedPercentage = (float)currentInteracted / (float)interactionCount * 100;
            Debug.Log($"Interaction Results: {currentInteracted} / {interactionCount} ({currentInteractedPercentage}%)");
            this.enabled = false;
            return;
        }
        
        // Check if all interactions are attempted
        bool allAttempted = interactableObjects.All(obj => obj.Interactions.All(i => i.Attempted));
        LogAllInteractionStatus();
        // Only end if ALL interactions are attempted AND we are not currently in the middle of an interaction sequence
        if (allAttempted && !isInteracting && currentExplorationState == ExplorationState.Navigation)
        {
            LogAllInteractionStatus();
            Debug.Log($"Test End: execution time {totalTime}s");
            int currentActivated = Utils.CountInteracted(interactableObjects, true);
            float currentActivatedPercentage = (float)currentActivated / (float)interactionCount * 100;
            Debug.Log($"Number of Activated Interactions: {currentActivated} / {interactionCount} ({currentActivatedPercentage}%)");
            this.enabled = false;
            return;
        }
        // Handle different exploration states
        switch (currentExplorationState)
        {
            case ExplorationState.Navigation:
                Navigation();
                break;
            case ExplorationState.ControllerMovement:
                ControllerMovement();
                break;
            case ExplorationState.ThreeDInteraction:
                ThreeDInteraction();
                break;
            case ExplorationState.SocketInteraction:
                SocketInteraction();
                break;
        }
    }

    /// <summary>
    /// Handle navigation state - move towards the closest interactable
    /// </summary>
    private void Navigation()
    {
        // Debug.Log("Navigation State");
        targetInteractable = GetCloestInteractable();
        if (targetInteractable == null)
        {
            // If no interactable found, we are not interacting.
            // This will allow the end condition in FixedUpdate to trigger if all are attempted.
            isInteracting = false; 
            return;
        }

        // Select the next unattempted interaction
        targetInteractionInfo = targetInteractable.Interactions.FirstOrDefault(i => !i.Attempted);
        if (targetInteractionInfo != null)
        {
             Debug.Log($"Starting Interaction: {targetInteractable.Name} - {targetInteractionInfo.Type}");
        }
        else
        {
             // Should not happen if GetClosestInteractable works correctly, but safe guard
             Debug.LogWarning($"Object {targetInteractable.Name} selected but no unattempted interactions found.");
             return;
        }
        
        // Start of a new interaction sequence
        isInteracting = true;
        
        // Reset interaction state
        grabActionCount = 0;
        combinedActionCount = 0;
        isGrabHeld = false;
        targetSocket = null;
        waitingForSocketCheck = false;
        currentInteractionRetries = 0;
        hasCurrentInteractionGrabbed = false;
        hasCurrentInteractionTriggered = false;

        ResetControllerPosition();
        GameObject targetObject = targetInteractable.Interactable;
        
        // IMPORTANT: Ensure we actually move to the object!
        MoveToTarget(targetObject, ExplorationState.ControllerMovement);
    }

    /// <summary>
    /// Generic movement logic to reach a target object
    /// </summary>
    private void MoveToTarget(GameObject targetObject, ExplorationState nextState)
    {
        Vector3 currentPos = transform.position;
        Vector3 targetPos = targetObject.transform.position;

        // Rotation (only rotate y-axis)
        Vector3 targetDirection = (targetPos - currentPos).normalized;
        targetDirection.y = 0;
        
        // Ensure we are not too close to zero vector
        if (targetDirection.sqrMagnitude > 0.001f)
        {
            float angle = Vector3.Angle(transform.forward, targetDirection);
            // If angle is large, we must rotate.
            if (angle > interactionAngle)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
                
                // If we are still rotating, DO NOT transition yet.
                // We return here to wait for next frame.
                return; 
            }
        }

        // Player Movement (calculate distance ignoring Y axis)
        Vector3 flatCurrentPos = new Vector3(currentPos.x, 0, currentPos.z);
        Vector3 flatTargetPos = new Vector3(targetPos.x, 0, targetPos.z);
        // Use World Distance instead of Viewport Distance for robust navigation
        float worldDistance = Vector3.Distance(flatCurrentPos, flatTargetPos);
        float interactionDistance = Utils.GetInteractionDistance();
        
        // If we are too far, move closer.
        if (worldDistance > interactionDistance)
        {
            Vector3 newPosition = Vector3.MoveTowards(
                new Vector3(currentPos.x, currentPos.y, currentPos.z),
                new Vector3(targetPos.x, currentPos.y, targetPos.z),
                moveSpeed * Time.deltaTime
            );
            transform.position = newPosition;
            return; // Don't proceed until close enough
        }
        
        // If we reach here, we are facing the object AND are close enough.
        // NOW we can transition to controller movement.
        StartCoroutine(TransitionToState(nextState));
    }

    /// <summary>
    /// Handle controller movement state - move the controller to the target
    /// </summary>
    private void ControllerMovement()
    {
        if (isArrivalTriggered) return;

        rightController = GameObject.Find("Right Controller");
        if (rightController == null) return;

        if (targetInteractable == null)
        {
            StartCoroutine(TransitionToState(ExplorationState.Navigation));
            return;
        }

        GameObject targetObject = targetInteractable.Interactable;
        
        // Custom logic for arrival at interactable
        Action onArrival = () => {
            isArrivalTriggered = true;
            targetInteractable.Visited = true;
            // Updated to use Interactions list
            bool intersection = Utils.GetIntersected(targetInteractable.Interactable, rightController);
            targetInteractable.Intersected = intersection;
            StartCoroutine(TransitionToState(ExplorationState.ThreeDInteraction));
        };

        MoveControllerToTarget(targetObject, onArrival);
    }

    private void MoveControllerToTarget(GameObject targetObject, Action onArrival)
    {
        // Debug.Log("Moving to:" + targetObject.name);
        timeSinceLastUpdate += Time.deltaTime;
        if (timeSinceLastUpdate >= updateInterval)
        {
            timeSinceLastUpdate = 0f;
            // Controller Movement
            Vector3 controllerCurrentPos = rightController.transform.position;
            Vector3 controllerTargetPos = targetObject.transform.position;
            Vector3 controllerWorldDirection = Utils.GetControllerWorldDirection(controllerCurrentPos, controllerTargetPos);
            float distanceToTarget = Vector3.Distance(controllerCurrentPos, controllerTargetPos);
            if (distanceToTarget > controllerMovementThreshold)
            {
                // Set to the right controller state
                SwitchControllerState(ControllerState.RightController);
                // Move towards the target
                MoveControllerInDirection(controllerWorldDirection.normalized);
            }
            else
            {
                // We are close enough.
                // NOTE: We do not check !isControllerMoving here because controller movement is done by key simulation.
                // We just stop sending move commands and call onArrival.
                onArrival?.Invoke();
            }
        }
    }

    /// <summary>
    /// Handle 3D interaction state
    /// </summary>
    private void ThreeDInteraction()
    {
        if (targetInteractionInfo == null)
        {
            StartCoroutine(TransitionToState(ExplorationState.Navigation));
            return;
        }

        if (!isArrivalTriggered)
        {
            Debug.LogWarning("ThreeDInteraction entered without arrival trigger! Resetting to Navigation.");
            StartCoroutine(TransitionToState(ExplorationState.Navigation));
            return;
        }

        string type = targetInteractionInfo.Type;
        Debug.Log($"ThreeDInteraction: {targetInteractable.Name} Type: {type} GrabCount: {grabActionCount}");

        if (type == "trigger")
        {
            // Trigger usually requires grab.
            if (!isGrabHeld && grabActionCount == 0 && combinedActionCount == 0)
            {
                StartCoroutine(HoldGrabAndTrigger());
            }
        }
        else if (type == "socket")
        {
             if (waitingForSocketCheck) return;

             if (grabActionCount == 0)
             {
                 // Start socket sequence
                 StartHoldingGrab();
                 grabActionCount++;
                 waitingForSocketCheck = true;
                 StartCoroutine(CheckAndStartSocketInteraction(targetInteractionInfo));
             }
        }
        else if (type == "grab")
        {
            if (isActionInProgress) return; // Wait for atomic action to complete
            if (targetInteractionInfo.Attempted) return; // Prevent re-entry

            // Simple grab and release (Tap)
            if (grabActionCount == 0)
            {
                StartCoroutine(PerformGrabAction());
            }
            else if (grabActionCount == 1)
            {
                 // Check if grab was successful
                 if (!hasCurrentInteractionGrabbed)
                 {
                     Debug.LogWarning($"Grab failed for {targetInteractable.Name}. Retrying ({currentInteractionRetries}/{MaxRetries})...");
                     if (currentInteractionRetries < MaxRetries)
                     {
                         currentInteractionRetries++;
                         grabActionCount = 0; // Reset to try grabbing again
                         return;
                     }
                     else
                     {
                         Debug.LogError($"Grab failed max retries for {targetInteractable.Name}. Skipping.");
                         targetInteractionInfo.Attempted = true;
                         StartCoroutine(TransitionToState(ExplorationState.Navigation));
                         return;
                     }
                 }
                 
                 // Release
                 StartCoroutine(PerformGrabAction());
            }
            else if (grabActionCount >= 2)
            {
                 // Verify Release
                //  if (isTargetCurrentlyGrabbed)
                //  {
                //      Debug.LogWarning($"Release check failed for {targetInteractable.Name} (still grabbed). Retrying release ({currentInteractionRetries}/{MaxRetries})...");
                     
                //      if (currentInteractionRetries < MaxRetries)
                //      {
                //          currentInteractionRetries++;
                //          grabActionCount = 1; // Go back to release step
                //          return;
                //      }
                //      else
                //      {
                //          Debug.LogError($"Release failed max retries for {targetInteractable.Name}. Forcing completion.");
                //          StopHoldingGrab(); // Ensure keys are released
                //          targetInteractionInfo.Attempted = true;
                //          StartCoroutine(TransitionToState(ExplorationState.Navigation));
                //          return;
                //      }
                //  }

                 targetInteractionInfo.Attempted = true;
                 grabActionCount = 0;
                 StartCoroutine(TransitionToState(ExplorationState.Navigation));
            }
        }
        else
        {
             Debug.LogWarning($"Unknown interaction type: {type}");
             targetInteractionInfo.Attempted = true;
             StartCoroutine(TransitionToState(ExplorationState.Navigation));
        }
    }

    private IEnumerator CheckAndStartSocketInteraction(Utils.InteractionInfo socketInfo)
    {
        yield return new WaitForSeconds(0.1f); // Wait for grab to potentially register
        bool socketFound = false;
        if (socketInfo != null && !string.IsNullOrEmpty(socketInfo.TargetInteractor))
        {
            targetSocket = GameObject.Find(socketInfo.TargetInteractor);
            if (targetSocket != null)
            {
                Debug.Log($"Transitioning to SocketInteraction. Target Socket: {targetSocket.name}");
                
                // Note: Listener is now registered globally in RegisterListeners()

                socketFound = true;
                StartCoroutine(TransitionToState(ExplorationState.SocketInteraction));
            }
            else
            {
                Debug.LogWarning($"Socket interaction found but target socket '{socketInfo.TargetInteractor}' not found in scene.");
            }
        }
        
        if (!socketFound)
        {
            Debug.LogError($"Failed to find socket for interaction. Aborting.");
            waitingForSocketCheck = false;
            if (isSocketGrabActive) StopHoldingGrab(); // Cleanup if we started holding but failed
            
            // Mark as attempted so we don't get stuck
            if (socketInfo != null) socketInfo.Attempted = true;
            
            StartCoroutine(TransitionToState(ExplorationState.Navigation));
        }
    }

    private void OnSocketSnap(SelectEnterEventArgs args)
    {
        Debug.Log($"Socket Interaction Successful: Object '{args.interactableObject.transform.name}' snapped into '{args.interactorObject.transform.name}'");
        
        // Mark the object as socketed
        var objName = args.interactableObject.transform.name;
        var interactableObj = interactableObjects.FirstOrDefault(o => o.Interactable.name == objName);
        if (interactableObj != null)
        {
            interactableObj.Socketed = true;
        }
    }

    // --- Socket Interaction States ---

    private void SocketInteraction()
    {
        // Debug.Log("Start Socket Interaction: " + targetSocket);
        if (targetSocket == null)
        {
            StartCoroutine(TransitionToState(ExplorationState.Navigation));
            return;
        }

        if (isArrivalTriggered) return;

        Action onArrival = () => {
            isArrivalTriggered = true;
            //  Debug.Log("Arrived at socket, releasing object.");
             // Release the object
             StartCoroutine(ReleaseObjectWithDelay());
        };
        
        MoveControllerToTarget(targetSocket, onArrival);
    }

    private IEnumerator ReleaseObjectWithDelay()
    {
        yield return new WaitForSeconds(1.0f); // Wait for 1.0s to ensure the object is in the socket
        StopHoldingGrab(); // Release held object
        grabActionCount++; 
             
        if (targetInteractionInfo != null)
        {
            targetInteractionInfo.Attempted = true;
        }
        
        // Ensure we properly reset state for the next object
        isGrabHeld = false;
        waitingForSocketCheck = false;
        isSocketGrabActive = false;
        
        // IMPORTANT: Clear the targetSocket to prevent ControllerMovement from trying to move to it in the next cycle
        targetSocket = null; 

        StartCoroutine(TransitionToState(ExplorationState.Navigation));
    }

    // ---------------------------------

    private IEnumerator HoldGrabAndTrigger()
    {
        if (targetInteractable == null || targetInteractionInfo == null || targetInteractionInfo.Attempted) yield break;
        isGrabHeld = true;
        var keyboard = InputSystem.GetDevice<Keyboard>();
        if (keyboard == null) yield break;
        // Hold grab
        if (grabActionCount == 0 && !targetInteractionInfo.Attempted)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.G));
            grabActionCount++;
            yield return new WaitForSeconds(0.5f); // Wait for grab to register
            
            if (!hasCurrentInteractionGrabbed)
            {
                Debug.LogWarning($"Grab failed during Trigger sequence for {targetInteractable.Name}. Retrying...");
                 // If grab failed, we can't trigger.
                 if (currentInteractionRetries < MaxRetries)
                 {
                     currentInteractionRetries++;
                     grabActionCount = 0;
                     InputSystem.QueueStateEvent(keyboard, new KeyboardState()); // Release keys
                     isGrabHeld = false;
                     yield break; // Exit this coroutine to allow retry in next frame loop
                 }
                 else
                 {
                     Debug.LogError($"Grab failed max retries during Trigger sequence for {targetInteractable.Name}. Skipping.");
                     targetInteractionInfo.Attempted = true;
                     StartCoroutine(TransitionToState(ExplorationState.Navigation));
                     yield break;
                 }
            }
        }
        // Execute trigger action while grab is held
        if (grabActionCount > 0 && combinedActionCount == 0 && !targetInteractionInfo.Attempted)
        {
            yield return new WaitForSeconds(0.1f);
            Key[] keys = { Key.T, Key.G };
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            combinedActionCount++;
            yield return new WaitForSeconds(0.5f); // Wait for trigger to register
            
            if (!hasCurrentInteractionTriggered)
            {
                 Debug.LogWarning($"Trigger failed for {targetInteractable.Name}.");
                 // We grabbed but didn't trigger. 
                 // Maybe we should retry trigger? or retry whole sequence?
                 // Let's retry trigger? But we might need to release and re-grab if physics is weird.
                 // For simplicity, retry whole sequence if retries left.
                 if (currentInteractionRetries < MaxRetries)
                 {
                     currentInteractionRetries++;
                     grabActionCount = 0;
                     combinedActionCount = 0;
                     InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                     isGrabHeld = false;
                     yield break;
                 }
            }
        }
        // Keep grab held after trigger
        if (grabActionCount > 0 && combinedActionCount > 0 && !targetInteractionInfo.Attempted)
        {
            yield return new WaitForSeconds(0.1f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            isGrabHeld = false;
            targetInteractionInfo.Attempted = true;
            StartCoroutine(TransitionToState(ExplorationState.Navigation));
        }
    }

    IEnumerator PerformGrabAction()
    {
        isActionInProgress = true;
        // Press and release G (Tap)
        yield return ExecuteKeyWithDuration(Key.G, 0.1f);
        // Wait for physics/events to register
        yield return new WaitForSeconds(0.5f);
        grabActionCount++;
        isActionInProgress = false;
    }

    /// <summary>
    /// Ensure we're in the desired controller manipulation state
    /// </summary>
    void SwitchControllerState(ControllerState targetState)
    {
        if (currentControllerState == targetState)
            return;
        Key key = Key.None;
        switch (targetState)
        {
            case ControllerState.LeftController:
                key = Key.LeftBracket;
                break;
            case ControllerState.RightController:
                key = Key.RightBracket;
                break;
        }
        if (key != Key.None)
        {
            StartCoroutine(ExecuteKeyWithDuration(key, 0.1f));
            currentControllerState = targetState;
        }
    }

    IEnumerator ExecuteKeyWithDuration(Key key, float duration)
    {
        var keyboard = InputSystem.GetDevice<Keyboard>();
        if (keyboard == null) yield break;
        
        // Construct state with preserved keys if needed
        List<Key> keysToPress = new List<Key>();
        if (key != Key.None) keysToPress.Add(key);
        if (isSocketGrabActive) keysToPress.Add(Key.G);

        // Press the key(s)
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keysToPress.ToArray()));
        
        // Wait for the specified duration
        yield return new WaitForSeconds(duration);
        
        // Release the key (but keep G if holding)
        if (isSocketGrabActive)
        {
             InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.G));
        }
        else
        {
             InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        }
    }

    void StartHoldingGrab()
    {
        // Debug.Log("Start Holding Grab");
        isSocketGrabActive = true;
        var keyboard = InputSystem.GetDevice<Keyboard>();
        if (keyboard != null)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.G));
        }
    }

    void StopHoldingGrab()
    {
        // Debug.Log("Stop Holding Grab");
        isSocketGrabActive = false;
        var keyboard = InputSystem.GetDevice<Keyboard>();
        if (keyboard != null)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        }
    }

    /// <summary>
    /// Move the controller in the given direction using input simulation
    /// </summary>
    /// <param name="direction">Direction from the controller to the target</param>
    void MoveControllerInDirection(Vector3 direction)
    {
        // Move in the controller's local direction, rather than in the world space's direction
        Vector3 controllerForward = rightController.transform.forward;
        Vector3 controllerRight = rightController.transform.right;
        Vector3 controllerUp = rightController.transform.up;
        float zAxis = Vector3.Dot(direction, controllerForward);
        float xAxis = Vector3.Dot(direction, controllerRight);
        float yAxis = Vector3.Dot(direction, controllerUp);
        EnqueueMovementKeys(xAxis, yAxis, zAxis);
    }

    /// <summary>
    /// Enqueue movement keys based on direction
    /// Greedy approach: move to the direction with largest distance first
    /// Using key commands for movement
    /// </summary>
    void EnqueueMovementKeys(float x, float y, float z)
    {
        float threshold = controllerMovementThreshold;
        float absX = Mathf.Abs(x);
        float absY = Mathf.Abs(y);
        float absZ = Mathf.Abs(z);
        // Forward-first policy: move the controller towards the target first, then tweak the x and y axis
        if (absZ > threshold)
        {
            Key zKey = z > 0 ? Key.W : Key.S;
            StartCoroutine(ExecuteKeyWithDuration(zKey, 0.01f));
            return;
        }
        if (absX > threshold)
        {
            Key xKey = x > 0 ? Key.D : Key.A;
            StartCoroutine(ExecuteKeyWithDuration(xKey, 0.01f));
            return;
        }
        if (absY > threshold)
        {
            Key yKey = y > 0 ? Key.E : Key.Q;
            StartCoroutine(ExecuteKeyWithDuration(yKey, 0.01f));
            return;
        }
    }

    /// <summary>
    /// Reset controller position by XR Interaction Simulator shortcut
    /// </summary>
    void ResetControllerPosition()
    {
        Key resetKey = Key.R;
        StartCoroutine(ExecuteKeyWithDuration(resetKey, 0.1f));
    }

    /// <summary>
    /// Greedy policy: move to and interact with the closest interactable based on the current position
    /// </summary>
    /// <returns></returns>
    public Utils.InteractableObject GetCloestInteractable()
    {
        Utils.InteractableObject closest = null;
        float minDistance = Mathf.Infinity;
        foreach (Utils.InteractableObject interactable in interactableObjects)
        {
            if (interactable.Interactions.Any(i => !i.Attempted))
            {
                float distance = Vector3.Distance(transform.position, interactable.Interactable.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = interactable;
                }
            }
        }
        
        // If we found a closest one, check if it's the same as the previous one and we just failed?
        // But here we rely on InteractionAttempted flag which is set at the end of interaction.
        // If we released the object, we should have set InteractionAttempted = true.
        
        return closest;
    }

    void RegisterListeners()
    {
        // Register listeners for interactable objects (Grab, etc.)
        foreach (var obj in interactableObjects)
        {
            var baseInteractable = obj.Interactable.GetComponent<XRBaseInteractable>();
            if (baseInteractable != null)
            {
                baseInteractable.selectEntered.AddListener(OnSelectEntered);
                baseInteractable.selectExited.AddListener(OnSelectExited);
                baseInteractable.activated.AddListener(OnActivated);
            }
        }

        // Register listeners for all socket interactors in the scene
        var allSockets = FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>(FindObjectsSortMode.None);
        foreach (var socket in allSockets)
        {
            socket.selectEntered.AddListener(OnSocketSnap);
        }
    }

    void SetObjectGrabbed(string interactableName)
    {
        foreach (var obj in interactableObjects)
        {
            if (obj.Interactable.name == interactableName && !obj.Interacted)
            {
                obj.Grabbed = true;
                if (!obj.IsTrigger)
                {
                    obj.Interacted = true;
                }
                Debug.Log("Grabbed: " + obj.Name + " " + obj.Interactable.name);
                break;
            }
        }
    }

    void SetObjectTriggered(string interactableName)
    {
        foreach (var obj in interactableObjects)
        {
            if (obj.Interactable.name == interactableName && !obj.Interacted)
            {
                obj.Triggered = true;
                if (obj.Grabbed)
                {
                    obj.Interacted = true;
                }
                Debug.Log("Triggered: " + obj.Name + " " + obj.Interactable.name);
                break;
            }
        }
    }

    private bool isTargetCurrentlyGrabbed = false; // Real-time status of target grab

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        var xrInteractable = args.interactableObject;
        // Debug.Log("OnSelectEntered: " + xrInteractable.transform.name);
        SetObjectGrabbed(xrInteractable.transform.name);
        
        if (targetInteractable != null && xrInteractable.transform.name == targetInteractable.Interactable.name)
        {
            hasCurrentInteractionGrabbed = true;
            isTargetCurrentlyGrabbed = true;
        }
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        var xrInteractable = args.interactableObject;
        if (targetInteractable != null && xrInteractable.transform.name == targetInteractable.Interactable.name)
        {
            isTargetCurrentlyGrabbed = false;
        }
    }

    private void OnActivated(ActivateEventArgs args)
    {
        var interactable = args.interactableObject;
        // Debug.Log($"OnActivated: {interactable.transform.name}");
        SetObjectTriggered(interactable.transform.name);
        
        if (targetInteractable != null && interactable.transform.name == targetInteractable.Interactable.name)
        {
            hasCurrentInteractionTriggered = true;
        }
    }

    /// <summary>
    /// Transition to a new state with a delay
    /// </summary>
    private IEnumerator TransitionToState(ExplorationState newState)
    {
        if (isTransitioning) yield break;
        isTransitioning = true;

        yield return new WaitForSeconds(stateTransitionDelay);
        // Reset the action flags when transitioning to a new state
        // NOTE: Don't reset if we are moving to socket states and want to keep grab count?
        // But we handle grabActionCount manually in ThreeDInteraction.
        // Actually, if we transition to SocketNavigation, we are HOLDING the object.
        // So we should NOT reset isGrabHeld or grabActionCount completely if we want to track it.
        // However, grabActionCount logic in ThreeDInteraction relies on it being 0 or 1.
        
        // Ensure we release any held keys if we are resetting state completely
        if (newState == ExplorationState.Navigation)
        {
            if (isGrabHeld) StopHoldingGrab();
            
            // Fully reset when going back to Navigation
            isGrabHeld = false;
            grabActionCount = 0;
            combinedActionCount = 0;
            isInteracting = false; // IMPORTANT: Allow Navigation to pick next target
            isArrivalTriggered = false;
        }
        else if (newState == ExplorationState.ThreeDInteraction)
        {
            // Reset when starting interaction? No, we might want to carry over? 
            // Usually ThreeDInteraction starts fresh.
             isGrabHeld = false;
             grabActionCount = 0;
             combinedActionCount = 0;
        }
        else if (newState == ExplorationState.SocketInteraction)
        {
            isArrivalTriggered = false;
        }
        // For Socket transitions, we preserve state (holding object)
        
        currentExplorationState = newState;
        isTransitioning = false;
    }

    private void LogAllInteractionStatus()
    {
        Debug.Log("=== Interaction Status Report ===");
        foreach (var obj in interactableObjects)
        {
            foreach (var interaction in obj.Interactions)
            {
                 Debug.Log($"Object: {obj.Name} | Interaction: {interaction}");
            }
        }
        Debug.Log("=================================");
    }
}
