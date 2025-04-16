using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor))]
public class ActivateRotatingInteractor : MonoBehaviour
{
    public DialInteractable DialToActivate;

    UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor m_Interactor;

    void Start()
    {
        m_Interactor = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>();
        m_Interactor.selectEntered.AddListener(Activated);
    }

    void Activated(SelectEnterEventArgs args)
    {
        var interactableObject = args.interactableObject as MonoBehaviour;
        if (interactableObject == null) return;

        // Get the Rigidbody from the interactable
        DialToActivate.RotatingRigidbody = interactableObject.GetComponentInChildren<Rigidbody>();
        DialToActivate.gameObject.SetActive(true);

        // Update interaction layers using new system
        if (interactableObject.TryGetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>(out var interactable))
        {
            interactable.interactionLayers = 0;
        }
    }
}
