```c#
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

public class ButtonInteractableFIXED : UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable
{
    [System.Serializable]
    public class ButtonPressedEvent : UnityEvent { }
    [System.Serializable]
    public class ButtonReleasedEvent : UnityEvent { }

    public Vector3 Axis = new Vector3(0, -1, 0);
    public float MaxDistance;
    public float ReturnSpeed = 10.0f;

    public AudioClip ButtonPressAudioClip;
    public AudioClip ButtonReleaseAudioClip;

    public ButtonPressedEvent OnButtonPressed;
    public ButtonReleasedEvent OnButtonReleased;

    Vector3 m_StartPosition;
    Rigidbody m_Rigidbody;
    Collider m_Collider;
    bool m_Pressed = false;
    bool m_IsInteracting = false;

    protected override void Awake()
    {
        base.Awake();
        m_Rigidbody = GetComponent<Rigidbody>();
        m_Collider = GetComponentInChildren<Collider>();
        m_StartPosition = transform.position;
    }

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        base.ProcessInteractable(updatePhase);

        if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Fixed) return;

        Vector3 worldAxis = transform.TransformDirection(Axis);
        Vector3 end = transform.position + worldAxis * MaxDistance;

        float m_CurrentDistance = (transform.position - m_StartPosition).magnitude;
        float move = 0.0f;

        if (isSelected)
        {
            // When interacting, push the button down
            move = MaxDistance - m_CurrentDistance;
            m_IsInteracting = true;
        }
        else if (m_IsInteracting || m_CurrentDistance > 0)
        {
            // Return the button when not interacting
            move = -ReturnSpeed * Time.deltaTime;
            m_IsInteracting = false;
        }

        float newDistance = Mathf.Clamp(m_CurrentDistance + move, 0, MaxDistance);
        m_Rigidbody.MovePosition(m_StartPosition + worldAxis * newDistance);

        // Handle button press and release events
        if (!m_Pressed && Mathf.Approximately(newDistance, MaxDistance))
        {
            m_Pressed = true;
            SFXPlayer.Instance.PlaySFX(ButtonPressAudioClip, transform.position, new SFXPlayer.PlayParameters()
            {
                Pitch = Random.Range(0.9f, 1.1f),
                SourceID = -1,
                Volume = 1.0f
            }, 0.0f);
            OnButtonPressed.Invoke();
        }
        else if (m_Pressed && !Mathf.Approximately(newDistance, MaxDistance))
        {
            m_Pressed = false;
            SFXPlayer.Instance.PlaySFX(ButtonReleaseAudioClip, transform.position, new SFXPlayer.PlayParameters()
            {
                Pitch = Random.Range(0.9f, 1.1f),
                SourceID = -1,
                Volume = 1.0f
            }, 0.0f);
            OnButtonReleased.Invoke();
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Handles.DrawLine(transform.position, transform.position + transform.TransformDirection(Axis).normalized * MaxDistance);
    }
#endif
}

```

