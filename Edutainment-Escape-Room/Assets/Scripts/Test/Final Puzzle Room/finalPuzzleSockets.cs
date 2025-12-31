using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
public class finalPuzzleSockets : MonoBehaviour
{
    [SerializeField] private finalPuzzle linkedPuzzleManager;

    private UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor socket;

    [SerializeField] private Transform secondPartCorrect;

    private void Awake() => socket = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();

    private void OnEnable()
    {
        if (socket == null)
        {
            socket = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
        }
        if (socket != null)
        {
            socket.selectEntered.AddListener(ObjectSnapped);
            socket.selectExited.AddListener(ObjectRemoved);
        }
    }

    private void OnDisable()
    {
        socket.selectEntered.RemoveListener(ObjectSnapped);
        socket.selectExited.RemoveListener(ObjectRemoved);
    }

    private void ObjectSnapped(SelectEnterEventArgs arg0)
    {
        linkedPuzzleManager.incrementFirstPart();
        if (arg0.interactableObject.transform.name == secondPartCorrect.name)
        {
            linkedPuzzleManager.incrementSecondPart();
        }
    }
    private void ObjectRemoved(SelectExitEventArgs arg0)
    {
        linkedPuzzleManager.decrementFirstPart();
        if (arg0.interactableObject.transform.name == secondPartCorrect.name)
        {
            linkedPuzzleManager.decrementSecondPart();
        }
    }
}
