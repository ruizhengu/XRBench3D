using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public interface IInteractionStrategy
{
    IEnumerator Execute(XRIntTest context, XRInputManager input, Utils.InteractableObject target, Utils.InteractionInfo info);
}

public class GrabInteractionStrategy : IInteractionStrategy
{
    private const int MaxRetries = 3;

    public IEnumerator Execute(XRIntTest context, XRInputManager input, Utils.InteractableObject target, Utils.InteractionInfo info)
    {
        int retries = 0;
        bool success = false;

        while (retries < MaxRetries && !success)
        {
            // 1. Perform Grab (Tap G)
            yield return PerformTap(input, Key.G);
            yield return new WaitForSeconds(0.5f);

            // 2. Verify Grab
            if (!context.HasCurrentInteractionGrabbed)
            {
                Debug.LogWarning($"Grab failed for {target.Name}. Retrying ({retries + 1}/{MaxRetries})...");
                retries++;
                continue; // Retry loop
            }

            // 3. Release (Tap G again)
            yield return PerformTap(input, Key.G);
            
            // 4. Verify Release
            // Ideally check context.IsTargetCurrentlyGrabbed is false
            // But for now we assume release works or we just proceed.
            // The original code had logic to retry release, we can add that if needed.
            
            success = true;
        }

        if (!success)
        {
            Debug.LogError($"Grab interaction failed after {MaxRetries} retries for {target.Name}.");
        }
        
        info.Attempted = true;
    }

    private IEnumerator PerformTap(XRInputManager input, Key key)
    {
        yield return input.ExecuteKeyWithDuration(key, 0.1f);
    }
}

public class TriggerInteractionStrategy : IInteractionStrategy
{
    private const int MaxRetries = 3;

    public IEnumerator Execute(XRIntTest context, XRInputManager input, Utils.InteractableObject target, Utils.InteractionInfo info)
    {
        int retries = 0;
        bool success = false;

        while (retries < MaxRetries && !success)
        {
            // 1. Hold Grab
            input.StartHoldingGrab();
            yield return new WaitForSeconds(0.5f);

            if (!context.HasCurrentInteractionGrabbed)
            {
                Debug.LogWarning($"Grab (for Trigger) failed for {target.Name}. Retrying ({retries + 1}/{MaxRetries})...");
                input.StopHoldingGrab();
                retries++;
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            // 2. Press Trigger (T) while holding Grab
            yield return input.ExecuteKeysWithDuration(new Key[] { Key.T }, 0.1f);
            yield return new WaitForSeconds(0.5f);

            if (!context.HasCurrentInteractionTriggered)
            {
                Debug.LogWarning($"Trigger failed for {target.Name}. Retrying sequence...");
                input.StopHoldingGrab();
                retries++;
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            // 3. Success - Release Grab
            input.StopHoldingGrab();
            success = true;
        }

        if (!success)
        {
             Debug.LogError($"Trigger interaction failed after {MaxRetries} retries for {target.Name}.");
             input.StopHoldingGrab(); // Ensure release
        }

        info.Attempted = true;
    }
}

public class SocketInteractionStrategy : IInteractionStrategy
{
    public IEnumerator Execute(XRIntTest context, XRInputManager input, Utils.InteractableObject target, Utils.InteractionInfo info)
    {
        // 1. Find Target Socket
        if (string.IsNullOrEmpty(info.TargetInteractor))
        {
            Debug.LogError("Socket interaction missing TargetInteractor name.");
            info.Attempted = true;
            yield break;
        }

        GameObject targetSocket = GameObject.Find(info.TargetInteractor);
        if (targetSocket == null)
        {
            Debug.LogError($"Target socket '{info.TargetInteractor}' not found.");
            info.Attempted = true;
            yield break;
        }

        // 2. Grab Object
        input.StartHoldingGrab();
        yield return new WaitForSeconds(0.5f);
        
        // Note: We might want to verify grab here too, but for socket flow let's proceed.
        
        // 3. Move Controller to Socket
        // We delegate back to context to move the controller to a new target
        yield return context.MoveControllerTo(targetSocket);
        
        // 4. Release Object (Snap)
        yield return new WaitForSeconds(1.0f); // Wait for stabilization
        input.StopHoldingGrab();
        yield return new WaitForSeconds(0.5f); // Wait for snap event

        info.Attempted = true;
    }
}

public class InteractionStrategyFactory
{
    public static IInteractionStrategy GetStrategy(string type)
    {
        switch (type)
        {
            case "grab": return new GrabInteractionStrategy();
            case "trigger": return new TriggerInteractionStrategy();
            case "socket": return new SocketInteractionStrategy();
            default: return null;
        }
    }
}
