using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class XROffsetGrabbable : UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable
{
    class SavedTransform
    {
        public Vector3 OriginalPosition;
        public Quaternion OriginalRotation;
    }

    Dictionary<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor, SavedTransform> m_SavedTransforms = new Dictionary<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor, SavedTransform>();
    Rigidbody m_Rb;

    protected override void Awake()
    {
        base.Awake();
        m_Rb = GetComponent<Rigidbody>();
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        var interactor = args.interactorObject;
        if (interactor is UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor directInteractor)
        {
            var savedTransform = new SavedTransform
            {
                OriginalPosition = directInteractor.attachTransform.localPosition,
                OriginalRotation = directInteractor.attachTransform.localRotation
            };

            m_SavedTransforms[interactor] = savedTransform;

            bool haveAttach = attachTransform != null;
            directInteractor.attachTransform.position = haveAttach ? attachTransform.position : m_Rb.worldCenterOfMass;
            directInteractor.attachTransform.rotation = haveAttach ? attachTransform.rotation : m_Rb.rotation;
        }

        base.OnSelectEntered(args);
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        var interactor = args.interactorObject;
        if (interactor is UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor directInteractor && m_SavedTransforms.TryGetValue(interactor, out var savedTransform))
        {
            directInteractor.attachTransform.localPosition = savedTransform.OriginalPosition;
            directInteractor.attachTransform.localRotation = savedTransform.OriginalRotation;
            m_SavedTransforms.Remove(interactor);
        }

        base.OnSelectExited(args);
    }

    public override bool IsSelectableBy(UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor interactor)
    {
        if (!base.IsSelectableBy(interactor))
            return false;

        if (interactor is UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor baseInteractor)
        {
            int interactorLayerMask = 1 << baseInteractor.gameObject.layer;
            return (interactionLayers.value & interactorLayerMask) != 0;
        }
        return false;
    }
}
