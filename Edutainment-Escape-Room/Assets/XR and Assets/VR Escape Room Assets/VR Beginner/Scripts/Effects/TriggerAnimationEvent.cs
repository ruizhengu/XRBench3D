using System.Collections;
using System.Collections.Generic;
using System.Data;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor))]
public class TriggerAnimationEvent : MonoBehaviour
{
    public string TriggerName;

    int m_TriggerID;

    void Start()
    {
        m_TriggerID = Animator.StringToHash(TriggerName);
        var interactor = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>();
        interactor.selectEntered.AddListener(TriggerAnim);
    }

    public void TriggerAnim(SelectEnterEventArgs args)
    {
        var interactable = args.interactableObject;
        var interactableGameObject = interactable.transform.gameObject;
        var animator = interactableGameObject.GetComponentInChildren<Animator>();

        if (animator != null)
        {
            animator.SetTrigger(TriggerName);
        }

        // Cast to concrete type to modify interaction layers
        var baseInteractable = interactable as UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable;
        if (baseInteractable != null)
        {
            int handsLayer = LayerMask.NameToLayer("Hands");
            if (handsLayer != -1)
            {
                baseInteractable.interactionLayers = (InteractionLayerMask)((int)baseInteractable.interactionLayers & ~(1 << handsLayer));
            }
        }
    }
}
