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
using UnityEngine.UI;
using TMPro;

public class InteractoBot : MonoBehaviour
{
    public List<InteractableObject> interactableObjects;
    public int interactionCount = 0;
    public GameObject rightController;
    private float gameSpeed = 3.0f; // May alter gameSpeed to speed up the test execution process
    // Movement parameters
    private float moveSpeed = 1.0f;
    private float rotateSpeed = 1.0f;
    private float updateInterval = 0.05f;
    private float timeSinceLastUpdate = 0f;
    private float interactionAngle = 5.0f; // The angle for transiting from rotation to interaction
    private float controllerMovementThreshold = 0.05f; // The distance of controller movement to continue interaction
    private float interactionOffset = 0.05f; // Small distance in front of the target for interaction
    private float stateTransitionDelay = 0.1f; // Delay between state transitions
    private bool isControllerMoving = false; // Flag to track if controller is currently moving
    private ControllerState currentControllerState = ControllerState.None; // Default state
    private ExplorationState currentExplorationState = ExplorationState.Navigation; // Default state
    private bool isMovedController = false; // Track if controller has been moved
    private float lastInteractionTime = 0f;
    private float interactionCooldown = 0.2f; // Cooldown period in seconds
    private int triggerActionCount = 0;
    private string current3DInteractionPattern = ""; // Store the current 3D interaction pattern
    private bool isGripHeld = false; // Track if grip is currently held
    private int gripActionCount = 0; // Track number of grip actions
    private int combinedActionCount = 0; // Track number of combined actions
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
        TwoDInteraction
    }

    void Start()
    {
        interactableObjects = Utils.GetInteractableObjects();
        interactionCount = interactableObjects.Count;
        RegisterListener(); // Register listeners for interactables and UIs
        Utils.FindSimulatedDevices(); // Find the simulated devices
    }

    void Update()
    {
        Time.timeScale = gameSpeed;
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
            case ExplorationState.TwoDInteraction:
                TwoDInteraction();
                break;
        }
    }

    /// <summary>
    /// Handle navigation state - move towards the closest interactable
    /// </summary>
    private void Navigation()
    {
        InteractableObject closestInteractable = GetCloestInteractable();
        if (closestInteractable == null)
        {
            if (interactableObjects.All(obj => obj.GetVisited()) &&
                (isGripHeld || gripActionCount > 0 || combinedActionCount > 0))
            {
                return; // Don't end the test yet, let the interaction complete
            }
            Debug.Log("Test End");
            Debug.Log("Number of Interacted Interactables: " + Utils.CountInteracted(interactableObjects) + " / " + interactionCount);
            return;
        }
        ResetControllerPosition();

        GameObject closestObject = closestInteractable.GetObject();
        Vector3 currentPos = transform.position;
        Vector3 targetPos = closestObject.transform.position;

        // Rotation (only rotate y-axis)
        Vector3 targetDirection = (targetPos - currentPos).normalized;
        targetDirection.y = 0;
        float angle = Vector3.Angle(transform.forward, targetDirection);
        if (angle > interactionAngle)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
            return; // Don't proceed with controller actions until angle difference is small enough
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
        StartCoroutine(TransitionToState(ExplorationState.ControllerMovement));
    }

    /// <summary>
    /// Handle controller movement state - move the controller to the target
    /// </summary>
    private void ControllerMovement()
    {
        rightController = GameObject.Find("Right Controller");
        if (rightController == null)
        {
            return;
        }

        InteractableObject closestInteractable = GetCloestInteractable();
        if (closestInteractable == null)
        {
            if (interactableObjects.All(obj => obj.GetVisited()) &&
                (isGripHeld || gripActionCount > 0 || combinedActionCount > 0))
            {
                return; // Don't end the test yet, let the interaction complete
            }
            StartCoroutine(TransitionToState(ExplorationState.Navigation));
            return;
        }

        GameObject closestObject = closestInteractable.GetObject();
        timeSinceLastUpdate += Time.deltaTime;
        if (timeSinceLastUpdate >= updateInterval)
        {
            timeSinceLastUpdate = 0f;
            // Controller Movement
            Vector3 controllerCurrentPos = rightController.transform.position;
            Vector3 controllerTargetPos = closestObject.transform.position;
            // Add offset in front of the target in the z axis
            controllerTargetPos += new Vector3(0, 0, interactionOffset);

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
                if (isControllerMoving) // Only proceed if the controller has stopped moving
                {
                    isControllerMoving = false;
                    closestInteractable.SetVisited(true);
                    var events = closestInteractable.GetEvents();
                    current3DInteractionPattern = string.Join(",", events);
                    if (closestInteractable.GetObjectType() == "3d")
                    {
                        StartCoroutine(TransitionToState(ExplorationState.ThreeDInteraction));
                    }
                    // else if (closestInteractable.GetObjectType() == "2d")
                    // {
                    //     StartCoroutine(TransitionToState(ExplorationState.TwoDInteraction));
                    // }
                }
            }
        }
    }

    /// <summary>
    /// Handle 3D interaction state
    /// </summary>
    private void ThreeDInteraction()
    {
        // Grip and trigger action
        if (current3DInteractionPattern.Contains("select") && current3DInteractionPattern.Contains("activate"))
        {
            if (!isGripHeld && gripActionCount == 0 && combinedActionCount == 0)
            {
                StartCoroutine(HoldGripAndTrigger());
            }
        }
        // Normal grip action
        else if (current3DInteractionPattern.Contains("select"))
        {
            if (gripActionCount < 1)
            {
                ControllerGripAction();
                gripActionCount++;
                if (gripActionCount >= 1)
                {
                    StartCoroutine(TransitionToState(ExplorationState.Navigation));
                }
            }
        }
    }

    private IEnumerator HoldGripAndTrigger()
    {
        if (isGripHeld) yield break;
        isGripHeld = true;
        var keyboard = InputSystem.GetDevice<Keyboard>();
        if (keyboard == null) yield break;
        // Hold grip
        if (gripActionCount == 0)
        {
            yield return new WaitForSeconds(0.5f); // Wait a moment to ensure grip is registered
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.G));
            gripActionCount++;
            yield return new WaitForSeconds(0.5f);
        }
        // Execute trigger action while grip is held
        if (gripActionCount > 0 && combinedActionCount == 0)
        {
            yield return new WaitForSeconds(0.5f);
            Key[] keys = { Key.T, Key.G };
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            combinedActionCount++;
            yield return new WaitForSeconds(0.5f);
        }
        // Keep grip held after trigger
        if (gripActionCount > 0 && combinedActionCount > 0)
        {
            yield return new WaitForSeconds(0.5f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.G));
            yield return new WaitForSeconds(0.5f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            isGripHeld = false;
            StartCoroutine(TransitionToState(ExplorationState.Navigation));
        }
    }

    /// <summary>
    /// Handle 2D interaction state
    /// </summary>
    private void TwoDInteraction()
    {
        // Check cool down time since the last interaction
        if (Time.time - lastInteractionTime < interactionCooldown)
        {
            return;
        }
        // Only perform the trigger action if haven't completed two actions
        if (triggerActionCount < 2)
        {
            ControllerTriggerAction();
            triggerActionCount++;
            lastInteractionTime = Time.time;
            // Only transition to Navigation after two trigger actions are completed
            if (triggerActionCount == 2)
            {
                StartCoroutine(TransitionToState(ExplorationState.Navigation));
            }
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
        // Debug.Log($"Executing key: {key} for {duration} seconds");
        var keyboard = InputSystem.GetDevice<Keyboard>();
        if (keyboard == null) yield break;
        // Press the key
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        // Wait for the specified duration
        yield return new WaitForSeconds(duration);
        // Release the key
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
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

    void ControllerGripAction()
    {
        // Debug.Log("Controller Grip Action");
        Key gripKey = Key.G;
        StartCoroutine(ExecuteKeyWithDuration(gripKey, 0.1f));
    }

    void ControllerTriggerAction()
    {
        Key triggerKey = Key.T;
        StartCoroutine(ExecuteKeyWithDuration(triggerKey, 0.1f));
    }

    /// <summary>
    /// Greedy policy: move to and interact with the closest interactable based on the current position
    /// </summary>
    /// <returns></returns>
    public InteractableObject GetCloestInteractable()
    {
        InteractableObject closest = null;
        float minDistance = Mathf.Infinity;
        foreach (InteractableObject interactable in interactableObjects)
        {
            // GameObject interactable = GameObject.Find(interactionEvent.interactable);
            // InteractableObject interactable = entry.Value;
            if (!interactable.GetVisited())
            {
                float distance = Vector3.Distance(transform.position, interactable.GetObject().transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = interactable;
                }
            }
        }
        return closest;
    }

    void RegisterListener()
    {
        // Register listeners for common interactable types
        foreach (var obj in interactableObjects)
        {
            var baseInteractable = obj.GetObject().GetComponent<XRBaseInteractable>();
            if (baseInteractable != null)
            {
                baseInteractable.selectEntered.AddListener(OnSelectEntered);
                baseInteractable.selectExited.AddListener(OnSelectExited);
                baseInteractable.activated.AddListener(OnActivated);
                baseInteractable.deactivated.AddListener(OnDeactivated);
            }
        }
        // Register EventTrigger listeners for UI elements
        // EventTrigger[] uiTriggers = FindObjectsByType<EventTrigger>(FindObjectsSortMode.None);
        // foreach (EventTrigger trigger in uiTriggers)
        // {
        //     // Create entry for pointer click
        //     EventTrigger.Entry pointerClickEntry = new EventTrigger.Entry();
        //     pointerClickEntry.eventID = EventTriggerType.PointerClick;
        //     pointerClickEntry.callback.AddListener((data) => { OnPointerClick((PointerEventData)data); });
        //     trigger.triggers.Add(pointerClickEntry);
        // }
    }

    void SetObjectInteracted(string interactableName)
    {
        foreach (var obj in interactableObjects)
        {
            if (obj.GetObject().name == interactableName && !obj.GetInteracted())
            {
                obj.SetInteracted(true);
                Debug.Log("Interacted: " + obj.GetName() + " " + obj.GetObject().name);
                break;
            }
        }
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        var xrInteractable = args.interactableObject;
        Debug.Log("OnSelectEntered: " + xrInteractable.transform.name);
        SetObjectInteracted(xrInteractable.transform.name);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        var xrInteractable = args.interactableObject;
        // Debug.Log("OnSelectExited: " + xrInteractable.transform.name);
    }

    private void OnActivated(ActivateEventArgs args)
    {
        var interactable = args.interactableObject;
        Debug.Log($"OnActivated: {interactable.transform.name}");
        SetObjectInteracted(interactable.transform.name);
    }

    private void OnDeactivated(DeactivateEventArgs args)
    {
        var interactable = args.interactableObject;
        // Debug.Log($"OnDeactivated: {interactable.transform.name}");
    }

    private void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log($"Pointer entered UI: {eventData.pointerEnter.name}");
    }

    private void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log($"Pointer exited UI: {eventData.pointerEnter.name}");
    }

    // private void OnPointerClick(PointerEventData eventData)
    // {
    //     var button = eventData.pointerEnter.GetComponentInParent<Button>();
    //     var toggle = eventData.pointerEnter.GetComponentInParent<Toggle>();
    //     var slider = eventData.pointerEnter.GetComponentInParent<Slider>();
    //     var dropdown = eventData.pointerEnter.GetComponentInParent<Dropdown>();
    //     var tmp_dropdown = eventData.pointerEnter.GetComponentInParent<TMP_Dropdown>();
    //     if (button != null)
    //     {
    //         // Debug.Log("Button clicked: " + button.gameObject.name);
    //         SetObjectInteracted(button.gameObject.name);
    //     }
    //     else if (toggle != null)
    //     {
    //         // Debug.Log("Toggle clicked: " + toggle.gameObject.name);
    //         SetObjectInteracted(toggle.gameObject.name);
    //     }
    //     else if (slider != null)
    //     {
    //         // Debug.Log("Slider clicked: " + slider.gameObject.name);
    //         SetObjectInteracted(slider.gameObject.name);
    //     }
    //     else if (dropdown != null)
    //     {
    //         // Debug.Log("Dropdown clicked: " + dropdown.gameObject.name);
    //         SetObjectInteracted(dropdown.gameObject.name);
    //     }
    //     else if (tmp_dropdown != null)
    //     {
    //         // Debug.Log("TMP_Dropdown clicked: " + tmp_dropdown.gameObject.name);
    //         SetObjectInteracted(tmp_dropdown.gameObject.name);
    //     }
    //     else
    //     {
    //         // Debug.Log("Uncategorised UI clicked: " + eventData.pointerEnter.name);
    //         SetObjectInteracted(eventData.pointerEnter.name);
    //     }
    // }

    /// <summary>
    /// Transition to a new state with a delay
    /// </summary>
    private IEnumerator TransitionToState(ExplorationState newState)
    {
        // Wait for the specified delay
        yield return new WaitForSeconds(stateTransitionDelay);
        // Reset the action flags when transitioning to a new state
        if (newState != ExplorationState.ThreeDInteraction)
        {
            current3DInteractionPattern = ""; // Clear the interaction pattern when leaving 3D state
            isGripHeld = false; // Ensure grip is released when leaving state
            gripActionCount = 0; // Reset grip action count
            combinedActionCount = 0; // Reset combined action count
        }
        if (newState != ExplorationState.TwoDInteraction)
        {
            triggerActionCount = 0; // Reset trigger action count when leaving 2D interaction state
        }
        currentExplorationState = newState;
    }
}
