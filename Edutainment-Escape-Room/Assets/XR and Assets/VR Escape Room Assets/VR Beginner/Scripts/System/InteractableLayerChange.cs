using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;


public class InteractableLayerChange : MonoBehaviour
{
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable TargetInteractable;
    public LayerMask NewLayerMask;

    public void ChangeLayerDynamic(UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable)
    {
        interactable.interactionLayers = (InteractionLayerMask)NewLayerMask.value;
    }

    public void ChangeLayer()
    {
        TargetInteractable.interactionLayers = (InteractionLayerMask)NewLayerMask.value;
    }
}
