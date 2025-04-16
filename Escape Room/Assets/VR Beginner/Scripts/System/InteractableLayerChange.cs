using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class InteractableLayerChange : MonoBehaviour
{
    [SerializeField] UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable TargetInteractable;
    [SerializeField] InteractionLayerMask NewInteractionLayers;

    public void ChangeLayerDynamic(UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable)
    {
        interactable.interactionLayers = NewInteractionLayers;
    }

    public void ChangeLayer()
    {
        if (TargetInteractable != null)
            TargetInteractable.interactionLayers = NewInteractionLayers;
    }
}
