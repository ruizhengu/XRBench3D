using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using System;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class RandomBaseline : MonoBehaviour
{
    public int interactionCount = 0;
    public List<Utils.InteractableObject> interactableObjects;
    private float gameSpeed = 2.0f; // May alter gameSpeed to speed up the test execution process
    private float actionInterval = 0.5f; // Time between random actions
    private float lastActionTime = 0f;
    private bool isExecutingAction = false;
    private bool isCameraMode = true; // Track whether we're in camera or controller mode
    private float cameraMoveSpeed = 0.1f; // Speed of camera movement
    private float cameraRotateSpeed = 5f; // Speed of camera rotation
    private float timeBudget = 600f; // 10 minutes time budget in seconds
    private float startTime; // Time when the program started
    private bool isTimeBudgetExceeded = false; // Flag to track if time budget is exceeded
    private float reportInterval = 30f; // Report interval in seconds
    private float reportTimer = 0f; // Timer for report interval
    private float minuteCount = 0.5f;

    // Action types that can be randomly selected
    private enum ActionType
    {
        MoveForward,
        MoveBackward,
        MoveLeft,
        MoveRight,
        MoveUp,
        MoveDown,
        RotateLeft,
        RotateRight,
        GripAction,
        TriggerAction,
        SwitchMode
    }

    void Start()
    {
        // Place the object at a random x and z position (y unchanged)
        Vector3 pos = transform.position;
        float randomX = UnityEngine.Random.Range(-2.5f, 2.5f);
        float randomZ = UnityEngine.Random.Range(-2.5f, 2.5f);
        transform.position = new Vector3(randomX, pos.y, randomZ);

        Time.timeScale = gameSpeed;
        startTime = Time.time;
        Debug.Log($"Starting random baseline with {timeBudget} seconds time budget");
        interactableObjects = Utils.GetInteractableObjects();
        interactionCount = Utils.GetInteractableEventsCount(interactableObjects);
        RegisterListeners();
    }


    void FixedUpdate()
    {
        reportTimer += Time.deltaTime;
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
        if (!isTimeBudgetExceeded && Time.time - lastActionTime >= actionInterval && !isExecutingAction)
        {
            StartCoroutine(ExecuteRandomAction());
        }
    }

    void RegisterListeners()
    {
        foreach (var obj in interactableObjects)
        {
            var baseInteractable = obj.Interactable.GetComponent<XRBaseInteractable>();
            if (baseInteractable != null)
            {
                baseInteractable.selectEntered.AddListener(OnSelectEntered);
                baseInteractable.activated.AddListener(OnActivated);
            }
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
        // Debug.Log($"OnSelectEntered: {xrInteractable.transform.name}");
        SetObjectGrabbed(xrInteractable.transform.name);
    }

    private void OnActivated(ActivateEventArgs args)
    {
        var interactable = args.interactableObject;
        // Debug.Log($"OnActivated: {interactable.transform.name}");
        SetObjectTriggered(interactable.transform.name);
    }

    private IEnumerator ExecuteRandomAction()
    {
        if (isTimeBudgetExceeded) yield break;

        isExecutingAction = true;
        lastActionTime = Time.time;

        // Get a random action type
        ActionType randomAction = (ActionType)UnityEngine.Random.Range(0, System.Enum.GetValues(typeof(ActionType)).Length);

        // Execute the selected action
        if (isCameraMode)
        {
            yield return ExecuteCameraAction(randomAction);
        }
        else
        {
            yield return ExecuteControllerAction(randomAction);
        }

        isExecutingAction = false;
    }

    private IEnumerator ExecuteCameraAction(ActionType action)
    {
        if (isTimeBudgetExceeded) yield break;

        switch (action)
        {
            case ActionType.MoveForward:
                transform.Translate(Vector3.forward * cameraMoveSpeed);
                break;
            case ActionType.MoveBackward:
                transform.Translate(Vector3.back * cameraMoveSpeed);
                break;
            case ActionType.MoveLeft:
                transform.Translate(Vector3.left * cameraMoveSpeed);
                break;
            case ActionType.MoveRight:
                transform.Translate(Vector3.right * cameraMoveSpeed);
                break;
            case ActionType.MoveUp:
                transform.Translate(Vector3.up * cameraMoveSpeed);
                break;
            case ActionType.MoveDown:
                transform.Translate(Vector3.down * cameraMoveSpeed);
                break;
            case ActionType.RotateLeft:
                transform.Rotate(Vector3.up, -cameraRotateSpeed);
                break;
            case ActionType.RotateRight:
                transform.Rotate(Vector3.up, cameraRotateSpeed);
                break;
            case ActionType.SwitchMode:
                Key switchControllerKey = Key.RightBracket;
                yield return ExecuteKeyWithDuration(switchControllerKey, 0.1f);
                isCameraMode = false;
                break;
        }
        yield return new WaitForSeconds(0.1f);
    }

    private IEnumerator ExecuteControllerAction(ActionType action)
    {
        if (isTimeBudgetExceeded) yield break;
        float randomDuration = UnityEngine.Random.Range(0.1f, 0.5f);
        switch (action)
        {
            case ActionType.MoveForward:
                yield return ExecuteKeyWithDuration(Key.W, randomDuration);
                break;
            case ActionType.MoveBackward:
                yield return ExecuteKeyWithDuration(Key.S, randomDuration);
                break;
            case ActionType.MoveLeft:
                yield return ExecuteKeyWithDuration(Key.A, randomDuration);
                break;
            case ActionType.MoveRight:
                yield return ExecuteKeyWithDuration(Key.D, randomDuration);
                break;
            case ActionType.MoveUp:
                yield return ExecuteKeyWithDuration(Key.E, randomDuration);
                break;
            case ActionType.MoveDown:
                yield return ExecuteKeyWithDuration(Key.Q, randomDuration);
                break;
            case ActionType.GripAction:
                yield return ExecuteKeyWithDuration(Key.G, randomDuration);
                break;
            case ActionType.TriggerAction:
                yield return ExecuteKeyWithDuration(Key.T, randomDuration);
                break;
            case ActionType.SwitchMode:
                isCameraMode = true;
                Key switchCameraKey = Key.Tab;
                yield return ExecuteKeyWithDuration(switchCameraKey, 0.1f);
                break;
        }
    }

    private IEnumerator ExecuteKeyWithDuration(Key key, float duration)
    {
        if (isTimeBudgetExceeded) yield break;

        var keyboard = InputSystem.GetDevice<Keyboard>();
        if (keyboard == null) yield break;

        // Press the key
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));

        // Wait for the specified duration
        yield return new WaitForSeconds(duration);

        // Release the key
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
    }
}
