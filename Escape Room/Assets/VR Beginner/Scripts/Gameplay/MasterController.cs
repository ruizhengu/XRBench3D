using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using CommonUsages = UnityEngine.XR.CommonUsages;
using InputDevice = UnityEngine.XR.InputDevice;
using Unity.XR.CoreUtils;

public class MasterController : MonoBehaviour
{
    static MasterController s_Instance;
    public static MasterController Instance => s_Instance;

    public XROrigin Rig => m_Rig;

    [Header("Setup")]
    public bool DisableSetupForDebug = false;
    public Transform StartingPosition;
    public GameObject TeleporterParent;

    [Header("Reference")]
    public UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor RightTeleportInteractor;
    public UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor LeftTeleportInteractor;
    public UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor RightDirectInteractor;
    public UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor LeftDirectInteractor;
    public MagicTractorBeam RightTractorBeam;
    public MagicTractorBeam LeftTractorBeam;

    XROrigin m_Rig;
    InputDevice m_LeftInputDevice;
    InputDevice m_RightInputDevice;
    UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual m_RightLineVisual;
    UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual m_LeftLineVisual;
    HandPrefab m_RightHandPrefab;
    HandPrefab m_LeftHandPrefab;
    XRReleaseController m_RightController;
    XRReleaseController m_LeftController;
    bool m_PreviousRightClicked;
    bool m_PreviousLeftClicked;
    bool m_LastFrameRightEnable;
    bool m_LastFrameLeftEnable;
    InteractionLayerMask m_OriginalRightMask;
    InteractionLayerMask m_OriginalLeftMask;

    void Awake()
    {
        s_Instance = this;
        m_Rig = GetComponent<XROrigin>();
    }

    void Start()
    {
        m_RightLineVisual = RightTeleportInteractor.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>();
        m_LeftLineVisual = LeftTeleportInteractor.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>();
        m_RightLineVisual.enabled = m_LeftLineVisual.enabled = false;

        m_RightController = RightTeleportInteractor.GetComponent<XRReleaseController>();
        m_LeftController = LeftTeleportInteractor.GetComponent<XRReleaseController>();

        m_OriginalRightMask = RightTeleportInteractor.interactionLayers;
        m_OriginalLeftMask = LeftTeleportInteractor.interactionLayers;

        if (!DisableSetupForDebug)
        {
            m_Rig.transform.SetPositionAndRotation(StartingPosition.position, StartingPosition.rotation);
            TeleporterParent?.SetActive(false);
        }

        UpdateInputDevices();
        SetupTrackingOrigin();
    }

    void UpdateInputDevices()
    {
        var devices = new List<InputDevice>();

        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Left,
            devices
        );
        if (devices.Count > 0) m_LeftInputDevice = devices[0];

        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Right,
            devices
        );
        if (devices.Count > 0) m_RightInputDevice = devices[0];
    }

    void SetupTrackingOrigin()
    {
        // Updated XRInputSubsystem access
        List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);

        foreach (var subsystem in subsystems)
        {
            if (subsystem.running && subsystem.GetTrackingOriginMode() != TrackingOriginModeFlags.Floor)
            {
                m_Rig.CameraYOffset = 1.8f;
                break;
            }
        }
    }

    void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            Application.Quit();

        RightTeleportUpdate();
        LeftTeleportUpdate();
    }

    void RightTeleportUpdate()
    {
        if (!m_RightInputDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axisInput))
            return;

        m_RightLineVisual.enabled = axisInput.y > 0.5f;
        // Fixed InteractionLayerMask initialization
        RightTeleportInteractor.interactionLayers = m_LastFrameRightEnable ?
            m_OriginalRightMask :
            new InteractionLayerMask { value = 0 };

        HandleTeleportAction(axisInput.y, ref m_PreviousRightClicked, m_RightController);
        HandleTractorBeam(axisInput.y, RightTractorBeam);
        UpdateHandAnimation(ref m_RightHandPrefab, RightDirectInteractor, m_PreviousRightClicked);

        m_LastFrameRightEnable = m_RightLineVisual.enabled;
    }

    void LeftTeleportUpdate()
    {
        if (!m_LeftInputDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axisInput))
            return;

        m_LeftLineVisual.enabled = axisInput.y > 0.5f;
        // Fixed InteractionLayerMask initialization
        LeftTeleportInteractor.interactionLayers = m_LastFrameLeftEnable ?
            m_OriginalLeftMask :
            new InteractionLayerMask { value = 0 };
        HandleTeleportAction(axisInput.y, ref m_PreviousLeftClicked, m_LeftController);
        HandleTractorBeam(axisInput.y, LeftTractorBeam);
        UpdateHandAnimation(ref m_LeftHandPrefab, LeftDirectInteractor, m_PreviousLeftClicked);

        m_LastFrameLeftEnable = m_LeftLineVisual.enabled;
    }

    void HandleTeleportAction(float axisY, ref bool previousClicked, XRReleaseController controller)
    {
        if (axisY <= 0.5f && previousClicked)
        {
            controller.Select();
        }
        previousClicked = axisY > 0.5f;
    }

    void HandleTractorBeam(float axisY, MagicTractorBeam tractorBeam)
    {
        if (axisY <= -0.5f && !tractorBeam.IsTracting)
        {
            tractorBeam.StartTracting();
        }
        else if (tractorBeam.IsTracting)
        {
            tractorBeam.StopTracting();
        }
    }

    void UpdateHandAnimation(ref HandPrefab handPrefab, UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor interactor, bool pointing)
    {
        if (handPrefab == null)
        {
            handPrefab = interactor.GetComponentInChildren<HandPrefab>();
        }
        handPrefab?.Animator.SetBool("Pointing", pointing);
    }

    void OnEnable() => InputDevices.deviceConnected += RegisterDevices;
    void OnDisable() => InputDevices.deviceConnected -= RegisterDevices;

    void RegisterDevices(InputDevice connectedDevice)
    {
        if (!connectedDevice.isValid) return;

        var characteristics = connectedDevice.characteristics;
        if ((characteristics & InputDeviceCharacteristics.HeldInHand) == 0) return;

        if ((characteristics & InputDeviceCharacteristics.Left) != 0)
            m_LeftInputDevice = connectedDevice;
        else if ((characteristics & InputDeviceCharacteristics.Right) != 0)
            m_RightInputDevice = connectedDevice;
    }
}
