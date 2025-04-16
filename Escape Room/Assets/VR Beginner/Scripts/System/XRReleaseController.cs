using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class XRReleaseController : XRController
{
    bool m_Selected;
    bool m_Active = false;

    protected void LateUpdate()
    {
        // Get or create controller state
        var state = currentControllerState ?? new XRControllerState();
        var selectState = state.selectInteractionState;

        if (m_Selected)
        {
            if (!m_Active)
            {
                selectState.activatedThisFrame = true;
                selectState.active = true;
                m_Active = true;
            }
        }
        else
        {
            if (m_Active)
            {
                selectState.deactivatedThisFrame = true;
                selectState.active = false;
                m_Active = false;
            }
        }

        // Update state and assign back
        state.selectInteractionState = selectState;
        currentControllerState = state;

        m_Selected = false;
    }

    public void Select()
    {
        m_Selected = true;
    }
}
