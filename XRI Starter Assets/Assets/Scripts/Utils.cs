using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
public static class Utils
{
  public static void StartSelect()
  {
    var device = InputSystem.GetDevice<Keyboard>();
    InputSystem.QueueStateEvent(device, new KeyboardState(Key.G));
  }

  public static void ActivateOnce()
  {
    var device = InputSystem.GetDevice<Mouse>();
    InputSystem.QueueStateEvent(device, new MouseState().WithButton(MouseButton.Left));
    InputSystem.QueueStateEvent(device, new MouseState().WithButton(MouseButton.Left, false));
  }

  public static void SwitchDeviceStateHMD()
  {
    var device = InputSystem.GetDevice<Keyboard>();
    InputSystem.QueueStateEvent(device, new KeyboardState(Key.U));
  }

  public static void SwitchDeviceStateLeftController()
  {
    var device = InputSystem.GetDevice<Keyboard>();
    InputSystem.QueueStateEvent(device, new KeyboardState(Key.T));
  }

  public static void SwitchDeviceStateRightController()
  {
    var device = InputSystem.GetDevice<Keyboard>();
    InputSystem.QueueStateEvent(device, new KeyboardState(Key.Y));
  }
}
