using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class XRInputManager : MonoBehaviour
{
    public enum ControllerState
    {
        None,
        LeftController,
        RightController,
        Both,
        HMD
    }

    [Header("Configuration")]
    public float ControllerMovementThreshold = 0.15f;
    public GameObject RightController;

    private ControllerState currentControllerState = ControllerState.None;
    private bool isSocketGrabActive = false;

    public void Initialize(GameObject rightController)
    {
        this.RightController = rightController;
    }

    /// <summary>
    /// Ensure we're in the desired controller manipulation state
    /// </summary>
    public IEnumerator SwitchControllerState(ControllerState targetState)
    {
        if (currentControllerState == targetState)
            yield break;

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
            yield return ExecuteKeyWithDuration(key, 0.1f);
            currentControllerState = targetState;
        }
    }

    /// <summary>
    /// Reset controller position by XR Interaction Simulator shortcut
    /// </summary>
    public IEnumerator ResetControllerPosition()
    {
        Key resetKey = Key.R;
        yield return ExecuteKeyWithDuration(resetKey, 0.1f);
    }

    public void StartHoldingGrab()
    {
        isSocketGrabActive = true;
        var keyboard = InputSystem.GetDevice<Keyboard>();
        if (keyboard != null)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.G));
        }
    }

    public void StopHoldingGrab()
    {
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
    public void MoveControllerInDirection(Vector3 direction)
    {
        if (RightController == null) return;

        // Move in the controller's local direction
        Vector3 controllerForward = RightController.transform.forward;
        Vector3 controllerRight = RightController.transform.right;
        Vector3 controllerUp = RightController.transform.up;

        float zAxis = Vector3.Dot(direction, controllerForward);
        float xAxis = Vector3.Dot(direction, controllerRight);
        float yAxis = Vector3.Dot(direction, controllerUp);

        EnqueueMovementKeys(xAxis, yAxis, zAxis);
    }

    private void EnqueueMovementKeys(float x, float y, float z)
    {
        float threshold = ControllerMovementThreshold;
        float absX = Mathf.Abs(x);
        float absY = Mathf.Abs(y);
        float absZ = Mathf.Abs(z);

        // Greedy policy: move to the direction with largest distance first
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

    public IEnumerator ExecuteKeyWithDuration(Key key, float duration)
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
    
    public IEnumerator ExecuteKeysWithDuration(Key[] keys, float duration)
    {
        var keyboard = InputSystem.GetDevice<Keyboard>();
        if (keyboard == null) yield break;

        List<Key> keysToPress = new List<Key>(keys);
        if (isSocketGrabActive && !keysToPress.Contains(Key.G)) keysToPress.Add(Key.G);

        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keysToPress.ToArray()));
        yield return new WaitForSeconds(duration);

        if (isSocketGrabActive)
        {
             InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.G));
        }
        else
        {
             InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        }
    }
}
