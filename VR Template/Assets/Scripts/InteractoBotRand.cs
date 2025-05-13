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

public class InteractoBotRand : MonoBehaviour
{
    private float actionInterval = 1.0f; // Time between random actions
    private float lastActionTime = 0f;
    private bool isExecutingAction = false;
    private bool isCameraMode = true; // Track whether we're in camera or controller mode
    private float cameraMoveSpeed = 0.1f; // Speed of camera movement
    private float cameraRotateSpeed = 5f; // Speed of camera rotation
    [SerializeField]
    private float timeBudget = 300f; // 5 minutes time budget in seconds
    private float startTime; // Time when the program started
    private bool isTimeBudgetExceeded = false; // Flag to track if time budget is exceeded
    private Dictionary<GameObject, InteractableObject> interactables;
    private int totalInteractables = 0;
    private float reportInterval = 60f; // Report interval in seconds
    private float reportTimer = 0f; // Timer for report interval
    private int minuteCount = 1;

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
        startTime = Time.time;
        Debug.Log($"Starting XUIMonkey with {timeBudget} seconds time budget");
        interactables = Utils.GetInteractables();
        totalInteractables = interactables.Count;
        RegisterListeners();
    }

    void RegisterListeners()
    {
        foreach (var obj in interactables.Values)
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
        EventTrigger[] uiTriggers = FindObjectsByType<EventTrigger>(FindObjectsSortMode.None);
        foreach (EventTrigger trigger in uiTriggers)
        {
            EventTrigger.Entry pointerClickEntry = new EventTrigger.Entry();
            pointerClickEntry.eventID = EventTriggerType.PointerClick;
            pointerClickEntry.callback.AddListener((data) => { OnPointerClick((PointerEventData)data); });
            trigger.triggers.Add(pointerClickEntry);
        }
    }

    void Update()
    {
        reportTimer += Time.deltaTime;
        if (reportTimer >= reportInterval)
        {
            int currentInteracted = Utils.CountInteracted(interactables.Values.ToList());
            Debug.Log("Current Interacted " + minuteCount + "m: " + currentInteracted + " / " + totalInteractables + " (" + (float)currentInteracted / (float)totalInteractables * 100 + "%)");
            minuteCount++;
            reportTimer = 0f;
        }
        // Check if we've exceeded the time budget
        if (!isTimeBudgetExceeded && Time.time - startTime >= timeBudget)
        {
            isTimeBudgetExceeded = true;
            Debug.Log($"Time budget exceeded. Stopping script execution.");
            Debug.Log($"Interaction Results: {Utils.CountInteracted(interactables.Values.ToList())} / {totalInteractables} interactables triggered");
            this.enabled = false; // Disable this script
            return;
        }

        if (!isTimeBudgetExceeded && Time.time - lastActionTime >= actionInterval && !isExecutingAction)
        {
            StartCoroutine(ExecuteRandomAction());
        }
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        var xrInteractable = args.interactableObject;
        Debug.Log($"OnSelectEntered: {xrInteractable.transform.name}");
        IncrementTriggeredCount(xrInteractable.transform.name);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        var xrInteractable = args.interactableObject;
        // Debug.Log($"OnSelectExited: {xrInteractable.transform.name}");
    }

    private void OnActivated(ActivateEventArgs args)
    {
        var interactable = args.interactableObject;
        Debug.Log($"OnActivated: {interactable.transform.name}");
        IncrementTriggeredCount(interactable.transform.name);
    }

    private void OnDeactivated(DeactivateEventArgs args)
    {
        var interactable = args.interactableObject;
        // Debug.Log($"OnDeactivated: {interactable.transform.name}");
    }

    private void OnPointerClick(PointerEventData eventData)
    {
        var button = eventData.pointerEnter.GetComponentInParent<Button>();
        var toggle = eventData.pointerEnter.GetComponentInParent<Toggle>();
        var slider = eventData.pointerEnter.GetComponentInParent<Slider>();
        var dropdown = eventData.pointerEnter.GetComponentInParent<Dropdown>();
        var tmp_dropdown = eventData.pointerEnter.GetComponentInParent<TMP_Dropdown>();

        if (button != null)
        {
            IncrementTriggeredCount(button.gameObject.name);
        }
        else if (toggle != null)
        {
            IncrementTriggeredCount(toggle.gameObject.name);
        }
        else if (slider != null)
        {
            IncrementTriggeredCount(slider.gameObject.name);
        }
        else if (dropdown != null)
        {
            IncrementTriggeredCount(dropdown.gameObject.name);
        }
        else if (tmp_dropdown != null)
        {
            IncrementTriggeredCount(tmp_dropdown.gameObject.name);
        }
        else
        {
            IncrementTriggeredCount(eventData.pointerEnter.name);
        }
    }

    // private int CountInteracted()
    // {
    //     int count = 0;
    //     foreach (var obj in interactableObjects)
    //     {
    //         if (obj.GetInteracted())
    //         {
    //             count++;
    //         }
    //         else
    //         {
    //             Debug.Log("Not Interacted Interactable: " + obj.GetName());
    //         }
    //     }
    //     return count;
    // }

    private void IncrementTriggeredCount(string interactableName)
    {
        foreach (var obj in interactables.Values)
        {
            if (obj.GetObject().name == interactableName && !obj.GetInteracted())
            {
                obj.SetInteracted(true);
                Debug.Log("Interacted: " + obj.GetName() + " " + obj.GetObject().name);
                break;
            }
        }
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

        switch (action)
        {
            case ActionType.MoveForward:
                yield return ExecuteKeyWithDuration(Key.W, 0.1f);
                break;
            case ActionType.MoveBackward:
                yield return ExecuteKeyWithDuration(Key.S, 0.1f);
                break;
            case ActionType.MoveLeft:
                yield return ExecuteKeyWithDuration(Key.A, 0.1f);
                break;
            case ActionType.MoveRight:
                yield return ExecuteKeyWithDuration(Key.D, 0.1f);
                break;
            case ActionType.MoveUp:
                yield return ExecuteKeyWithDuration(Key.E, 0.1f);
                break;
            case ActionType.MoveDown:
                yield return ExecuteKeyWithDuration(Key.Q, 0.1f);
                break;
            case ActionType.GripAction:
                yield return ExecuteKeyWithDuration(Key.G, 0.1f);
                break;
            case ActionType.TriggerAction:
                yield return ExecuteKeyWithDuration(Key.T, 0.1f);
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
