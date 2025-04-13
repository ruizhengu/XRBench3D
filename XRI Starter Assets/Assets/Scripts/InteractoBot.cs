using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public enum XRDeviceState { HMD, LeftController, RightController };

public class InteractoBot : MonoBehaviour
{
    public SceneExplore explorer;
    public InteractableIdentification interactableIdentification;
    public ControllerAction controllerAction;
    public Dictionary<GameObject, InteractableIdentification.InteractableInfo> interactables = new Dictionary<GameObject, InteractableIdentification.InteractableInfo>();
    public XRDeviceState deviceState = XRDeviceState.HMD;
    void Awake()
    {
        explorer = new SceneExplore(transform);
        interactableIdentification = new InteractableIdentification();
        controllerAction = new ControllerAction("left");
    }

    void Start()
    {
        interactables = interactableIdentification.GetInteractables();
        ResigterListener();
    }

    void Update()
    {
        Time.timeScale = 3.0f;
        // transform.position = explorer.RandomExploration();
        // SetSelectValue(1.0f);
        // SetActivateValue(1.0f);
        if (Keyboard.current.oKey.wasPressedThisFrame)
        {
            Utils.StartSelect();
        }
        if (Keyboard.current.pKey.wasPressedThisFrame)
        {
            StartCoroutine(ActivateAndRelease(0.5f));
        }
        // transform.position = explorer.RandomExploration();
        GameObject targetInteractable = GetCloestInteractable();
        if (targetInteractable && !controllerAction.GetMovementCompleted())
        {
            // var (updatePos, updateRot) = explorer.GreedyExploration(targetInteractable);
            var (updatePos, updateRot) = explorer.EasyExploration(targetInteractable);
            transform.SetPositionAndRotation(updatePos, updateRot);
            Vector3 targetPos = explorer.GameObjectOffset(targetInteractable);
            // if (transform.position == targetPos)
            if (Vector3.Distance(transform.position, targetPos) < 0.5f)
            {
                Debug.Log("Visited: " + targetInteractable.name);
                interactables[targetInteractable].SetVisited(true);
                SwitchDeviceState(XRDeviceState.LeftController);
                // StartCoroutine(controllerAction.ControllerMovement("left", targetPos));
                controllerAction.ControllerMovement("left", targetPos);
            }
            controllerAction.ControllerMovement("left", targetPos);
            // StartCoroutine(MoveAndRotate(targetInteractable, 0.5f));
        }
        else
        {
            // Debug.Log("All interactables are interacted. Test stop.");
        }
    }

    public void SwitchDeviceState(XRDeviceState state)
    {
        if (deviceState != state)
        {
            if (state == XRDeviceState.HMD)
            {
                Utils.SwitchDeviceStateHMD();
                deviceState = XRDeviceState.HMD;

            }
            else if (state == XRDeviceState.LeftController)
            {
                Utils.SwitchDeviceStateLeftController();
                deviceState = XRDeviceState.LeftController;
            }
            else if (state == XRDeviceState.RightController)
            {
                Utils.SwitchDeviceStateRightController();
                deviceState = XRDeviceState.RightController;
            }
            Debug.Log("Swtich device state to: " + deviceState);
        }
    }

    IEnumerator ActivateAndRelease(float duration)
    {
        var device = InputSystem.GetDevice<Mouse>();
        InputSystem.QueueStateEvent(device, new MouseState().WithButton(MouseButton.Left));
        yield return new WaitForSeconds(duration);
        InputSystem.QueueStateEvent(device, new MouseState().WithButton(MouseButton.Left, false));
    }

    // public void SetSelectValue(float value)
    // {
    //     var device = InputSystem.GetDevice<Keyboard>();
    //     InputSystem.QueueStateEvent(device, new KeyboardState(Key.G));
    // }

    // public void SetActivateValue(float value)
    // {
    //     var device = InputSystem.GetDevice<Mouse>();
    //     InputSystem.QueueStateEvent(device, new MouseState().WithButton(MouseButton.Left));
    // }

    public GameObject GetCloestInteractable()
    {
        GameObject closest = null;
        float minDistance = Mathf.Infinity;
        foreach (KeyValuePair<GameObject, InteractableIdentification.InteractableInfo> entry in interactables) // test with the first interactable
        {
            var interactableInfo = entry.Value;
            if (!interactableInfo.GetVisited())
            {
                GameObject obj = interactableInfo.GetObject();
                float distance = Vector3.Distance(transform.position, obj.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = obj;
                }
            }
        }
        return closest;
    }

    void GetPlayerTransform()
    {
        GameObject mainCamera = GameObject.FindWithTag("MainCamera");
        // Debug.Log("mainCamera: (" + mainCamera.transform.position + ") (" + mainCamera.transform.rotation + ")");
        GameObject leftController = GameObject.FindWithTag("LeftController");
        if (leftController)
        {
            // Debug.Log("leftController: (" + leftController.transform.position + ") (" + leftController.transform.rotation + ")");
        }
        else
        {
            // Debug.Log("leftController not found");
        }
        GameObject rightController = GameObject.FindWithTag("RightController");
        if (rightController)
        {
            // Debug.Log("rightController: (" + rightController.transform.position + ") (" + rightController.transform.rotation + ")");
        }
        else
        {
            // Debug.Log("rightController not found");
        }
    }

    void ResigterListener()
    {
        foreach (KeyValuePair<GameObject, InteractableIdentification.InteractableInfo> entry in interactables)
        {
            var grabInteractable = entry.Key.GetComponent<XRGrabInteractable>();
            var interactableType = entry.Value.GetObjectType();
            if (grabInteractable != null && interactableType == "3d")
            {
                grabInteractable.selectEntered.AddListener(OnSelectEntered);
                grabInteractable.selectExited.AddListener(OnSelectExited);
            }
            var baseInteractable = entry.Key.GetComponent<XRBaseInteractable>();
            if (baseInteractable != null && interactableType == "3d")
            {
                baseInteractable.activated.AddListener(OnActivated);
                baseInteractable.deactivated.AddListener(OnDeactivated);
            }
        }
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        var xrInteractable = args.interactableObject as UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;
        Debug.Log("OnSelectEntered: " + xrInteractable.gameObject.name);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        var xrInteractable = args.interactableObject as UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;
        Debug.Log("OnSelectExited: " + xrInteractable.gameObject.name);
    }

    private void OnActivated(ActivateEventArgs args)
    {
        var xrInteractable = args.interactableObject as UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable;
        Debug.Log("OnActivated: " + xrInteractable.gameObject.name);
    }

    private void OnDeactivated(DeactivateEventArgs args)
    {
        var xrInteractable = args.interactableObject as UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable;
        Debug.Log("OnDeactivated: " + xrInteractable.gameObject.name);
    }

    public class ControllerAction
    {
        private GameObject controller;
        private float controllerMovementStep = 1f;
        private bool movementCompleted;
        private bool interactionCompleted;
        // private string controllerType;

        public ControllerAction(string controllerType)
        {
            if (controllerType == "left")
            {
                controller = GameObject.FindWithTag("LeftController");
            }
            else if (controllerType == "right")
            {
                controller = GameObject.FindWithTag("RightController");
            }
            else
            {
                Debug.LogError("Please create the controller with a valid type");
            }
        }

        public void GetControllerInstance(string controllerType)
        {
            if (controllerType == "left")
            {
                controller = GameObject.FindWithTag("LeftController");
            }
            else if (controllerType == "right")
            {
                controller = GameObject.FindWithTag("RightController");
            }
            else
            {
                Debug.LogError("Please create the controller with a valid type");
            }
        }
        public void ControllerMovement(string controllerType, Vector3 targetPos)
        {
            GetControllerInstance(controllerType);
            Debug.Log("ControllerMovement--Controller Pose" + controller.transform.position);
            Debug.Log("ControllerMovement--Target Pose" + targetPos);
            controller.transform.position = Vector3.MoveTowards(
                controller.transform.position,
                targetPos,
                controllerMovementStep * Time.deltaTime
            );
            // yield return new WaitForSeconds(0.0001f);
        }

        public void SetMovementCompleted(bool flag)
        {
            movementCompleted = flag;
        }

        public bool GetMovementCompleted()
        {
            return movementCompleted;
        }

    }
}
