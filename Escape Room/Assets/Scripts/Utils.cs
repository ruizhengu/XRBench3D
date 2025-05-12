using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SimpleJSON;
using Newtonsoft.Json;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Linq;


[Serializable]
public class InteractionEvent
{
  public string interactor;
  public List<string> condition;
  public string interactable;
  public string type;
  public string event_type;

  public InteractionEvent(string interactor, List<string> condition, string interactable, string type, string event_type)
  {
    this.interactor = interactor;
    this.condition = condition;
    this.interactable = interactable;
    this.type = type;
    this.event_type = event_type;
  }
}

public static class Utils
{

  /// <summary>
  /// Find simulator devices (i.e., controllers and HMD)
  /// </summary>
  public static void FindSimulatedDevices()
  {
    InputDevice simulatedControllerDevice = null;
    var devices = InputSystem.devices;
    foreach (var device in devices)
    {
      if (device.name == "XRSimulatedController")
      {
        simulatedControllerDevice = device;
        break;
      }
      // TODO: could check what does "XRSimulatedController1" do
    }
    if (simulatedControllerDevice == null)
    {
      Debug.LogWarning("Couldn't find simulated left controller device. Movement won't work.");
    }
  }


  /// <summary>
  /// Get the interaction distance considering resolution
  /// </summary>
  /// <returns></returns>
  public static float GetInteractionDistance()
  {
    float interactionDistance = 0.5f; // The distance for transiting from movement to interaction
    float dpiScale = Screen.dpi / 96f; // Normalize to 96DPI base
    float adjustedInteractionDistance = interactionDistance * dpiScale; // Ajusted distance based on the screen size
    return adjustedInteractionDistance;
  }

  /// <summary>
  /// Get the distance between user and target considering resolution (ignore y axis)
  /// </summary>
  /// <param name="currentPos"></param>
  /// <param name="targetPos"></param>
  /// <returns></returns>
  public static float GetUserViewportDistance(Vector3 currentPos, Vector3 targetPos)
  {
    Vector3 currentViewport = Camera.main.WorldToViewportPoint(currentPos);
    Vector3 targetViewport = Camera.main.WorldToViewportPoint(targetPos);
    float distanceToTarget = Vector3.Distance(currentPos, targetPos);
    float viewportDistance = Vector2.Distance(
        new Vector2(currentViewport.x, currentViewport.z),
        new Vector2(targetViewport.x, targetViewport.z)
    );
    return viewportDistance;
  }


  /// <summary>
  /// Get the direction from controller to target considering resolution
  /// </summary>
  /// <param name="currentPos"></param>
  /// <param name="targetPos"></param>
  /// <returns></returns>
  public static Vector3 GetControllerWorldDirection(Vector3 currentPos, Vector3 targetPos)
  {
    // Convert to viewport space for resolution independence
    Vector3 controllerCurrentViewport = Camera.main.WorldToViewportPoint(currentPos);
    Vector3 controllerTargetViewport = Camera.main.WorldToViewportPoint(targetPos);
    Vector3 viewportDirection = controllerTargetViewport - controllerCurrentViewport;
    Vector3 worldDirection = Camera.main.ViewportToWorldPoint(controllerCurrentViewport + viewportDirection.normalized * Time.deltaTime) - currentPos;
    return worldDirection;
  }

  public static Dictionary<GameObject, InteractableObject> GetInteractables()
  {
    Dictionary<GameObject, InteractableObject> interactables = new Dictionary<GameObject, InteractableObject>();
    GameObject[] gos = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
    foreach (GameObject go in gos)
    {
      EventTrigger trigger = go.GetComponent<EventTrigger>();
      UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable xrInteractable =
        go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
      // if (trigger != null && !interactables.ContainsKey(go))
      // {
      //   interactables[go] = new InteractableObject(null, go, "2d", new List<string>());
      // }
      if (xrInteractable != null && !interactables.ContainsKey(go))
      {
        interactables[go] = new InteractableObject(null, go, "3d", new List<string>());
      }
    }
    foreach (var interactable in interactables)
    {
      Debug.Log("Interactable: " + interactable.Key.name + " " + interactable.Value.GetName());
    }
    Debug.Log("Interactables: " + interactables.Count);
    return interactables;
  }

  /// <summary>
  /// Get the interaction events from the interaction_results.json file
  /// </summary>
  public static List<InteractionEvent> GetInteractionEvents()
  {
    // string jsonPath = Path.Combine(Application.dataPath, "Scripts/interaction_results.json");
    // string jsonPath = Path.Combine(Application.dataPath, "Scripts/interaction_results_PrototypeScene.json");
    string jsonPath = Path.Combine(Application.dataPath, "Scripts/interaction_results_EscapeRoom.json");
    using (StreamReader r = new StreamReader(jsonPath))
    {
      string json = r.ReadToEnd();
      List<InteractionEvent> interactionEvents = JsonConvert.DeserializeObject<List<InteractionEvent>>(json);
      return interactionEvents;
    }
  }

  public static void ExecuteKey(Key key)
  {
    Debug.Log("Executing key: " + key);
    var keyboard = InputSystem.GetDevice<Keyboard>();
    if (keyboard == null) return;
    InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
  }

  public static void ExecuteKeyImmediate(Key key)
  {
    var keyboard = InputSystem.GetDevice<Keyboard>();
    if (keyboard == null) return;

    // Press and release immediately
    InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
  }

  public static List<InteractableObject> GetInteractableObjects()
  {
    var interactionEvents = GetInteractionEvents();
    var interactableObjects = new List<InteractableObject>();
    var interactableEventTypes = CollectEventTypes(interactionEvents);
    var processedInteractables = new HashSet<string>();

    foreach (var interactionEvent in interactionEvents)
    {
      if (processedInteractables.Contains(interactionEvent.interactable)) continue;

      var interactable = FindInteractableObject(interactionEvent.interactable);
      if (interactable == null) continue;

      ProcessInteractable(interactable, interactionEvent, interactableEventTypes, interactionEvents, interactableObjects, processedInteractables);
    }
    LogInteractables(interactableObjects);
    return interactableObjects;
  }

  private static Dictionary<string, HashSet<string>> CollectEventTypes(List<InteractionEvent> events)
  {
    var eventTypes = new Dictionary<string, HashSet<string>>();
    foreach (var evt in events)
    {
      if (!eventTypes.ContainsKey(evt.interactable))
      {
        eventTypes[evt.interactable] = new HashSet<string>();
      }
      eventTypes[evt.interactable].Add(evt.event_type);
    }
    return eventTypes;
  }

  private static void ProcessInteractable(
    GameObject interactable,
    InteractionEvent currentEvent,
    Dictionary<string, HashSet<string>> eventTypes,
    List<InteractionEvent> allEvents,
    List<InteractableObject> interactableObjects,
    HashSet<string> processedInteractables)
  {
    var type = currentEvent.type;
    var interactableEvents = eventTypes[currentEvent.interactable];

    if (ShouldCombineEvents(currentEvent.interactable, interactableEvents, allEvents))
    {
      if (TryAddInteractable(interactable, type, currentEvent.interactable, interactableObjects, new List<string> { "select", "activate" }))
      {
        processedInteractables.Add(currentEvent.interactable);
        return;
      }
    }
    else
    {
      if (TryAddInteractable(interactable, type, currentEvent.interactable, interactableObjects, new List<string> { currentEvent.event_type }))
      {
        processedInteractables.Add(currentEvent.interactable);
        return;
      }
    }

    AddChildInteractables(interactable, type, currentEvent.interactable, interactableObjects, interactableEvents);
    processedInteractables.Add(currentEvent.interactable);
  }

  private static bool ShouldCombineEvents(string interactableName, HashSet<string> eventTypes, List<InteractionEvent> allEvents)
  {
    bool hasSelect = eventTypes.Contains("select");
    bool hasActivate = eventTypes.Contains("activate");
    bool hasSelectCondition = allEvents.Any(evt =>
      evt.interactable == interactableName &&
      evt.condition != null &&
      evt.condition.Contains("select"));

    return hasSelect && hasActivate && hasSelectCondition;
  }

  private static void LogInteractables(List<InteractableObject> interactables)
  {
    foreach (var interactable in interactables)
    {
      Debug.Log($"Interactable: {interactable.GetName()} <{string.Join(", ", interactable.GetEvents())}> ({interactable.GetObject().name})");
    }
  }

  private static GameObject FindInteractableObject(string name)
  {
    var interactable = GameObject.Find(name);
    if (interactable != null) return interactable;

    return GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None)
        .FirstOrDefault(obj => obj.name.Contains(name));
  }

  private static bool TryAddInteractable(GameObject obj, string type, string name, List<InteractableObject> interactables, List<string> eventTypes)
  {
    if (type == "2d" && obj.GetComponent<EventTrigger>() != null)
    {
      interactables.Add(new InteractableObject(name, obj, "2d", eventTypes));
      return true;
    }
    if (type == "3d" && obj.GetComponent<XRBaseInteractable>() != null)
    {
      interactables.Add(new InteractableObject(name, obj, "3d", eventTypes));
      return true;
    }
    return false;
  }

  private static void AddChildInteractables(GameObject parent, string type, string name, List<InteractableObject> interactables, HashSet<string> eventTypes)
  {
    if (type == "2d")
    {
      foreach (var trigger in parent.GetComponentsInChildren<EventTrigger>())
      {
        interactables.Add(new InteractableObject(name, trigger.gameObject, "2d", new List<string>(eventTypes)));
      }
    }
    else if (type == "3d")
    {
      foreach (var interactable in parent.GetComponentsInChildren<XRBaseInteractable>())
      {
        interactables.Add(new InteractableObject(name, interactable.gameObject, "3d", new List<string>(eventTypes)));
      }
    }
  }

  public static int CountInteracted(List<InteractableObject> interactableObjects)
  {
    int count = 0;
    foreach (var obj in interactableObjects)
    {
      if (obj.GetInteracted())
      {
        count++;
      }
      else if (!obj.GetInteracted() && obj.GetIntersected())
      {
        Debug.Log("Could be a bug: " + obj.GetName());
      }
      else
      {
        Debug.Log("Not Interacted Interactable: " + obj.GetName());
      }
    }
    return count;
  }

  public static bool GetIntersected(GameObject target, GameObject controller)
  {
    Collider[] interactableColliders = target.GetComponentsInChildren<Collider>();
    Collider controllerCollider = controller.GetComponent<Collider>();
    if (interactableColliders.Length > 0 && controllerCollider != null)
    {
      Bounds combinedBounds = interactableColliders[0].bounds;
      for (int i = 1; i < interactableColliders.Length; i++)
      {
        combinedBounds.Encapsulate(interactableColliders[i].bounds);
      }
      return combinedBounds.Intersects(controllerCollider.bounds);
    }
    return false;
  }
}
