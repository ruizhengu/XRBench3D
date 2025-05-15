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

  /// <summary>
  /// Get the interaction events from the interaction_results.json file
  /// </summary>
  public static List<InteractionEvent> ParseInteractionGraph()
  {
    string jsonPath = Path.Combine(Application.dataPath, "Scripts/scene_graph.json");
    using (StreamReader r = new StreamReader(jsonPath))
    {
      string json = r.ReadToEnd();
      List<InteractionEvent> interactionEvents = JsonConvert.DeserializeObject<List<InteractionEvent>>(json);
      return interactionEvents;
    }
  }

  public static List<InteractableObject> GetInteractableObjects()
  {
    var interactionEvents = ParseInteractionGraph();
    var interactableDict = new Dictionary<string, InteractableObject>();

    foreach (var interactionEvent in interactionEvents)
    {
      var interactable = GameObject.Find(interactionEvent.interactable);
      if (interactable == null) continue;

      if (!interactableDict.TryGetValue(interactionEvent.interactable, out var obj))
      {
        // Create new InteractableObject with the first interaction type
        var events = new List<string> { interactionEvent.interaction_type };
        bool isTrigger = interactionEvent.interaction_type == "trigger";
        obj = new InteractableObject(interactionEvent.interactable, interactable, isTrigger, events);
        interactableDict[interactionEvent.interactable] = obj;
      }
      else
      {
        // Add new interaction type if not already present
        if (!obj.Events.Contains(interactionEvent.interaction_type))
        {
          obj.Events.Add(interactionEvent.interaction_type);
        }
        // If any interaction type is trigger, set IsTrigger to true
        if (interactionEvent.interaction_type == "trigger")
        {
          obj.IsTrigger = true;
        }
      }
    }
    var interactableObjects = interactableDict.Values.ToList();
    // LogInteractables(interactableObjects);
    return interactableObjects;
  }

  public static int GetInteractableEventsCount(List<InteractableObject> interactableObjects)
  {
    int eventCount = 0;
    foreach (var obj in interactableObjects)
    {
      if (obj.Events != null)
      {
        eventCount += obj.Events.Count;
      }
    }
    return eventCount;
  }

  private static void LogInteractables(List<InteractableObject> interactables)
  {
    foreach (var interactable in interactables)
    {
      Debug.Log($"Interactable: {interactable.Name} <{string.Join(", ", interactable.Events)}> ({interactable.Interactable.name})");
    }
  }

  public static int CountInteracted(List<InteractableObject> interactableObjects, bool detailedLog = false)
  {
    int count = 0;
    foreach (var obj in interactableObjects)
    {
      if (obj.Interacted)
      {
        count++;
        if (obj.IsTrigger)
        {
          count++;
        }
      }
      else if (!obj.Interacted && obj.Grabbed)
      {
        count++;
      }
      if (detailedLog)
      {
        if (!obj.Interacted && obj.Intersected)
        {
          Debug.Log("Could be a bug: " + obj.Name);
        }
        if (!obj.Interacted)
        {
          Debug.Log("Not Interacted Interactable: " + obj.Name);
        }
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

  public class InteractionEvent
  {
    public string interactor;
    public List<string> condition;
    public string interactable;
    public string interaction_type;

    public InteractionEvent() { }

    public InteractionEvent(string interactor, List<string> condition, string interactable, string interaction_type)
    {
      this.interactor = interactor;
      this.condition = condition;
      this.interactable = interactable;
      this.interaction_type = interaction_type;
    }
  }

  public class InteractableObject
  {
    public GameObject Interactable { get; set; }
    public string Name { get; set; }
    public List<string> Events { get; set; }
    public bool Intersected { get; set; }
    public bool IsTrigger { get; set; }
    public bool Triggered { get; set; }
    public bool Grabbed { get; set; }
    public bool Visited { get; set; }
    public bool InteractionAttempted { get; set; }
    public bool Interacted { get; set; }


    public InteractableObject(string name, GameObject go, bool isTrigger, List<string> events)
    {
      this.Name = name;
      this.Interactable = go;
      this.IsTrigger = isTrigger;
      this.Visited = false;
      this.Interacted = false;
      this.InteractionAttempted = false;
      this.Intersected = false;
      this.Events = events;
    }
  }
}
