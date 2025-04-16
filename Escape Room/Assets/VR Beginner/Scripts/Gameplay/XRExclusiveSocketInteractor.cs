using UnityEngine;


public class XRExclusiveSocketInteractor : UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor
{
    public string AcceptedType;

    public override bool CanSelect(UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable interactable)
    {
        if (!base.CanSelect(interactable))
            return false;

        // Get the SocketTarget component from the interactable
        var socketTarget = (interactable as MonoBehaviour)?.GetComponent<SocketTarget>();
        return socketTarget != null && socketTarget.SocketType == AcceptedType;
    }

    public override bool CanHover(UnityEngine.XR.Interaction.Toolkit.Interactables.IXRHoverInteractable interactable)
    {
        if (!base.CanHover(interactable))
            return false;

        // Get the SocketTarget component from the interactable
        var socketTarget = (interactable as MonoBehaviour)?.GetComponent<SocketTarget>();
        return socketTarget != null && socketTarget.SocketType == AcceptedType;
    }
}
