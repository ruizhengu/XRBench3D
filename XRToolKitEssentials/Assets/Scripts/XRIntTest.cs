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
    private bool isControllerMoving = false; // Flag to track if controller is currently moving
    private ControllerState currentControllerState = ControllerState.None; // Default state
    private ExplorationState currentExplorationState = ExplorationState.Navigation; // Default state
    private bool isMovedController = false; // Track if controller has been moved
    private string current3DInteractionPattern = ""; // Store the current 3D interaction pattern
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
        if (interactableObjects.All(obj => obj.InteractionAttempted))
        {
            // Only end if there are no ongoing interactions and at least one ThreeDInteraction attempt has been made
            if (!isGrabHeld && grabActionCount == 0 && combinedActionCount == 0 && currentExplorationState == ExplorationState.Navigation)
            {
                Debug.Log($"Test End: execution time {totalTime}s");
                int currentActivated = Utils.CountInteracted(interactableObjects, true);
                float currentActivatedPercentage = (float)currentActivated / (float)interactionCount * 100;
                Debug.Log($"Number of Activated Interactions: {currentActivated} / {interactionCount} ({currentActivatedPercentage}%)");
                this.enabled = false;
                return;
            }
            if (currentExplorationState == ExplorationState.Navigation) return; // Don't end yet if we are navigating/interacting
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
        targetInteractable = GetCloestInteractable();
        if (targetInteractable == null)
        {
            return;
        }
        
        // Reset interaction state
        grabActionCount = 0;
        combinedActionCount = 0;
        isGrabHeld = false;
        targetSocket = null;
        current3DInteractionPattern = "";
        waitingForSocketCheck = false;

        ResetControllerPosition();
        GameObject targetObject = targetInteractable.Interactable;
        
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
        if (targetDirection != Vector3.zero)
        {
            float angle = Vector3.Angle(transform.forward, targetDirection);
            if (angle > interactionAngle)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
                return; // Don't proceed with controller actions until angle difference is small enough
            }
        }

        // Player Movement (calculate distance ignoring Y axis)
        Vector3 flatCurrentPos = new Vector3(currentPos.x, 0, currentPos.z);
        Vector3 flatTargetPos = new Vector3(targetPos.x, 0, targetPos.z);
        float viewportDistance = Utils.GetUserViewportDistance(flatCurrentPos, flatTargetPos);
        float interactionDistance = Utils.GetInteractionDistance();
        if (viewportDistance > interactionDistance)
        {
            Vector3 newPosition = Vector3.MoveTowards(
                new Vector3(currentPos.x, currentPos.y, currentPos.z),
                new Vector3(targetPos.x, currentPos.y, targetPos.z),
                moveSpeed * Time.deltaTime
            );
            transform.position = newPosition;
            return; // Don't proceed with controller actions until close enough
        }
        // If we're close enough, switch to controller movement with delay
        StartCoroutine(TransitionToState(nextState));
    }

    /// <summary>
    /// Handle controller movement state - move the controller to the target
    /// </summary>
    private void ControllerMovement()
    {
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
            targetInteractable.Visited = true;
            // Updated to use Interactions list
            var types = targetInteractable.Interactions.Select(i => i.Type);
            current3DInteractionPattern = string.Join(",", types);
            bool intersection = Utils.GetIntersected(targetInteractable.Interactable, rightController);
            targetInteractable.Intersected = intersection;
            StartCoroutine(TransitionToState(ExplorationState.ThreeDInteraction));
        };

        MoveControllerToTarget(targetObject, onArrival);
    }

    private void MoveControllerToTarget(GameObject targetObject, Action onArrival)
    {
        Debug.Log("Moving to:" + targetObject.name);
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
                isControllerMoving = true;
            }
            else
            {
                // We are close enough.
                // NOTE: We do not check !isControllerMoving here because controller movement is done by key simulation.
                // We just stop sending move commands and call onArrival.
                isControllerMoving = false;
                onArrival?.Invoke();
            }
        }
    }

    /// <summary>
    /// Handle 3D interaction state
    /// </summary>
    private void ThreeDInteraction()
    {
        // Grab and trigger action
        if (current3DInteractionPattern.Contains("grab") && current3DInteractionPattern.Contains("trigger"))
        {
            if (!isGrabHeld && grabActionCount == 0 && combinedActionCount == 0)
            {
                StartCoroutine(HoldGrabAndTrigger());
            }
        }
        // Normal grab action (or start of socket interaction)
        else if (current3DInteractionPattern.Contains("grab") || current3DInteractionPattern.Contains("socket"))
        {
            if (waitingForSocketCheck) return;

            if (grabActionCount < 2)
            {
                // Only perform grab if we haven't grabbed yet (grabActionCount 0 -> 1)
                if (grabActionCount == 0)
                {
                    // Check if we need to do socket interaction
                    var socketInfo = targetInteractable.Interactions.FirstOrDefault(i => i.Type == "socket");

                    if (socketInfo != null)
                    {
                        // For socket, use Hold
                        StartHoldingGrab();
                        grabActionCount++;
                        waitingForSocketCheck = true;
                        StartCoroutine(CheckAndStartSocketInteraction(socketInfo));
                    }
                    else
                    {
                        // Normal grab (Tap)
                        ControllerGrabAction();
                        grabActionCount++;
                    }
                }
                else if (grabActionCount == 1)
                {
                    // If we are here, it means we grabbed, but didn't transition to socket (or finished socket and returned? No).
                    // This is the release phase for normal grab.
                    ControllerGrabAction();
                    grabActionCount++;
                    if (grabActionCount >= 2)
                    {
                        targetInteractable.InteractionAttempted = true;
                        StartCoroutine(TransitionToState(ExplorationState.Navigation));
                    }
                }
            }
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
            waitingForSocketCheck = false;
            if (isSocketGrabActive) StopHoldingGrab(); // Cleanup if we started holding but failed
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
        Debug.Log("Start Socket Interaction: " + targetSocket);
        if (targetSocket == null)
        {
            StartCoroutine(TransitionToState(ExplorationState.Navigation));
            return;
        }

        Action onArrival = () => {
             Debug.Log("Arrived at socket, releasing object.");
             // Release the object
             StartCoroutine(ReleaseObjectWithDelay());
        };
        
        MoveControllerToTarget(targetSocket, onArrival);
    }

    private IEnumerator ReleaseObjectWithDelay()
    {
        yield return new WaitForSeconds(1.0f); // Wait for 0.5s to ensure the object is in the socket
        StopHoldingGrab(); // Release held object
        grabActionCount++; 
             
        targetInteractable.InteractionAttempted = true;
        StartCoroutine(TransitionToState(ExplorationState.Navigation));
    }

    // ---------------------------------

    private IEnumerator HoldGrabAndTrigger()
    {
        if (targetInteractable == null || targetInteractable.InteractionAttempted) yield break;
        isGrabHeld = true;
        var keyboard = InputSystem.GetDevice<Keyboard>();
        if (keyboard == null) yield break;
        // Hold grab
        if (grabActionCount == 0 && !targetInteractable.InteractionAttempted)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.G));
            grabActionCount++;
        }
        // Execute trigger action while grab is held
        if (grabActionCount > 0 && combinedActionCount == 0 && !targetInteractable.InteractionAttempted)
        {
            yield return new WaitForSeconds(0.1f);
            Key[] keys = { Key.T, Key.G };
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            combinedActionCount++;
        }
        // Keep grab held after trigger
        if (grabActionCount > 0 && combinedActionCount > 0 && !targetInteractable.InteractionAttempted)
        {
            yield return new WaitForSeconds(0.1f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            isGrabHeld = false;
            targetInteractable.InteractionAttempted = true;
            StartCoroutine(TransitionToState(ExplorationState.Navigation));
        }
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
        if (isMovedController) return;
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
        isMovedController = true;
    }

    /// <summary>
    /// Reset controller position by XR Interaction Simulator shortcut
    /// </summary>
    void ResetControllerPosition()
    {
        Key resetKey = Key.R;
        StartCoroutine(ExecuteKeyWithDuration(resetKey, 0.1f));
    }

    void ControllerGrabAction()
    {
        // Debug.Log("Controller Grab Action");
        Key grabKey = Key.G;
        StartCoroutine(ExecuteKeyWithDuration(grabKey, 0.1f));
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
            if (!interactable.Visited && !interactable.InteractionAttempted)
            {
                float distance = Vector3.Distance(transform.position, interactable.Interactable.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = interactable;
                }
            }
        }
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
                baseInteractable.activated.AddListener(OnActivated);
            }
        }

        // Register listeners for all socket interactors in the scene
        var allSockets = FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
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

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        var xrInteractable = args.interactableObject;
        // Debug.Log("OnSelectEntered: " + xrInteractable.transform.name);
        SetObjectGrabbed(xrInteractable.transform.name);
    }

    private void OnActivated(ActivateEventArgs args)
    {
        var interactable = args.interactableObject;
        // Debug.Log($"OnActivated: {interactable.transform.name}");
        SetObjectTriggered(interactable.transform.name);
    }

    /// <summary>
    /// Transition to a new state with a delay
    /// </summary>
    private IEnumerator TransitionToState(ExplorationState newState)
    {
        yield return new WaitForSeconds(stateTransitionDelay);
        // Reset the action flags when transitioning to a new state
        // NOTE: Don't reset if we are moving to socket states and want to keep grab count?
        // But we handle grabActionCount manually in ThreeDInteraction.
        // Actually, if we transition to SocketNavigation, we are HOLDING the object.
        // So we should NOT reset isGrabHeld or grabActionCount completely if we want to track it.
        // However, grabActionCount logic in ThreeDInteraction relies on it being 0 or 1.
        
        if (newState == ExplorationState.Navigation)
        {
            // Fully reset when going back to Navigation
            current3DInteractionPattern = "";
            isGrabHeld = false;
            grabActionCount = 0;
            combinedActionCount = 0;
        }
        else if (newState == ExplorationState.ThreeDInteraction)
        {
            // Reset when starting interaction? No, we might want to carry over? 
            // Usually ThreeDInteraction starts fresh.
             isGrabHeld = false;
             grabActionCount = 0;
             combinedActionCount = 0;
        }
        // For Socket transitions, we preserve state (holding object)
        
        currentExplorationState = newState;
    }

    private void GetComponentAttributes()
    {
        GameObject blaster = GameObject.Find("Blaster").transform.parent.gameObject;
        Debug.Log(blaster.GetComponent<XRGrabInteractable>());
        var grabInteractable = blaster.GetComponent<XRGrabInteractable>();
        if (grabInteractable != null)
        {
            // Get the activated event
            var activatedEvent = grabInteractable.activated;
            Debug.Log($"Blaster Activate Event: {activatedEvent}");
            activatedEvent.AddListener((args) =>
            {
                Debug.Log("Blaster activated event was fired!");
            });
            // You can also check if there are any listeners attached to this event
            var hasListeners = activatedEvent.GetPersistentEventCount() > 0;
            Debug.Log($"Blaster Activate Event has listeners: {hasListeners}");
            for (int i = 0; i < activatedEvent.GetPersistentEventCount(); i++)
            {
                var target = activatedEvent.GetPersistentTarget(i);
                var method = activatedEvent.GetPersistentMethodName(i);
                Debug.Log($"Activate Listener {i}: Target={target}, Method={method}");
            }
        }
    }
}
