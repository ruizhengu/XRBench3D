using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor))]
public class TriggerAnimationEvent : MonoBehaviour
{
    public string TriggerName;
    public InteractionLayerMask excludedLayers;

    int m_TriggerID;

    void Start()
    {
        m_TriggerID = Animator.StringToHash(TriggerName);
        GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>().selectEntered.AddListener(TriggerAnim);
    }

    public void TriggerAnim(SelectEnterEventArgs args)
    {
        var interactable = args.interactableObject as MonoBehaviour;
        if (interactable == null) return;

        var animator = interactable.GetComponentInChildren<Animator>();
        animator?.SetTrigger(m_TriggerID);

        // Update interaction layers using new system
        if (interactable.TryGetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>(out var xrInteractable))
        {
            // xrInteractable.interactionLayers = xrInteractable.interactionLayers.Exclude(excludedLayers);
            xrInteractable.interactionLayers &= ~(1 << LayerMask.NameToLayer("Hands"));

        }
    }
}
