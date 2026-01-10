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

  private class InteractionGroup
  {
    public string interactor;
    public string interactable;
    public object interaction;
  }

  public class InteractionInfo
  {
      public string Type;
      public string Condition;
      public string TargetInteractor;

      public InteractionInfo(string type, string condition, string targetInteractor = null)
      {
          Type = type;
          Condition = condition;
          TargetInteractor = targetInteractor;
      }

      public override string ToString()
      {
          if (TargetInteractor != null)
              return $"{Type} -> {TargetInteractor}";
          return Type;
      }
  }

  /// <summary>
  /// Get the interaction events from the interaction_results.json file
  /// </summary>
  public static List<InteractableObject> GetInteractableObjects()
  {
    string jsonPath = Path.Combine(Application.dataPath, "Scripts/IFG.json");
    if (!File.Exists(jsonPath))
    {
        Debug.LogError($"IFG.json not found at {jsonPath}");
        return new List<InteractableObject>();
    }

    using (StreamReader r = new StreamReader(jsonPath))
    {
      string json = r.ReadToEnd();
      var groupedEvents = JsonConvert.DeserializeObject<List<InteractionGroup>>(json);
      var interactableDict = new Dictionary<string, InteractableObject>();

      if (groupedEvents != null)
      {
        foreach (var group in groupedEvents)
        {
          var interactableGO = GameObject.Find(group.interactable);
          if (interactableGO == null) continue;

          if (!interactableDict.TryGetValue(group.interactable, out var obj))
          {
            obj = new InteractableObject(group.interactable, interactableGO, false, new List<InteractionInfo>());
            interactableDict[group.interactable] = obj;
          }

          if (group.interaction != null)
          {
            if (group.interaction is string interactionStr && interactionStr == "socket")
            {
                bool exists = obj.Interactions.Any(i => i.Type == "socket" && i.TargetInteractor == group.interactor);
                if (!exists)
                {
                    obj.Interactions.Add(new InteractionInfo("socket", "grab", group.interactor));
                }
            }
            else if (group.interaction is Newtonsoft.Json.Linq.JArray interactionList)
            {
                foreach (var item in interactionList)
                {
                    if (item is Newtonsoft.Json.Linq.JArray interaction && interaction.Count >= 2)
                    {
                        string type = interaction[0].ToString();
                        string condition = interaction[1].ToString();
                        
                        // Add interaction if not present (simple check based on type and target)
                        bool exists = obj.Interactions.Any(i => i.Type == type && i.TargetInteractor == group.interactor);
                        if (!exists)
                        {
                            obj.Interactions.Add(new InteractionInfo(type, condition, group.interactor));
                        }

                        if (type == "trigger")
                        {
                            obj.IsTrigger = true;
                        }
                    }
                }
            }
          }
        }
      }
      return interactableDict.Values.ToList();
    }
  }

  public static int GetInteractableEventsCount(List<InteractableObject> interactableObjects)
  {
    int eventCount = 0;
    foreach (var obj in interactableObjects)
    {
      if (obj.Interactions != null)
      {
        eventCount += obj.Interactions.Count;
      }
    }
    return eventCount;
  }

  private static void LogInteractables(List<InteractableObject> interactables)
  {
    foreach (var interactable in interactables)
    {
      Debug.Log($"Interactable: {interactable.Name} <{string.Join(", ", interactable.Interactions)}> ({interactable.Interactable.name})");
    }
  }

  public static int CountInteracted(List<InteractableObject> interactableObjects, bool detailedLog = false)
  {
    int count = 0;
    foreach (var obj in interactableObjects)
    {
        // Simple logic: if object interacted, count all non-socket interactions as done?
        // Or strictly count based on what happened.
        // For now, let's keep it simple: count total interactions vs executed.
        // This function seems to rely on flags like Grabbed/Triggered.
        
      if (obj.Interacted)
      {
        count++; // Base interaction
        if (obj.IsTrigger) count++;
        // TODO: Count socket interactions?
      }
      else if (obj.Grabbed)
      {
        count++;
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

  public class InteractableObject
  {
    public GameObject Interactable { get; set; }
    public string Name { get; set; }
    public List<InteractionInfo> Interactions { get; set; }
    public bool Intersected { get; set; }
    public bool IsTrigger { get; set; }
    public bool Triggered { get; set; }
    public bool Grabbed { get; set; }
    public bool Visited { get; set; }
    public bool InteractionAttempted { get; set; }
    public bool Interacted { get; set; }


    public InteractableObject(string name, GameObject go, bool isTrigger, List<InteractionInfo> interactions)
    {
      this.Name = name;
      this.Interactable = go;
      this.IsTrigger = isTrigger;
      this.Visited = false;
      this.Interacted = false;
      this.InteractionAttempted = false;
      this.Intersected = false;
      this.Interactions = interactions;
    }
  }
}
