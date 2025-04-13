using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;


public class InteractableIdentification
{
  private static Dictionary<GameObject, InteractableInfo> interactables = new();
  private static GameObject triggered;
  // private GameObject leftController;
  // private GameObject rightController;
  // private float controllerMovementStep = 0.1f;

  public InteractableIdentification()
  {
    GameObject[] gos = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
    foreach (GameObject go in gos)
    {
      // TODO seperate control and interacatable
      FetchControl(go);
      FetchInteractable(go);
    }
  }

  // public Dictionary<GameObject, ControlInfo> getControls()
  // {
  //   return controls;
  // }

  // public void MoveLeftController(Vector3 dest)
  // {
  //   leftController = GameObject.FindWithTag("LeftController");
  //   Vector3 pos = leftController.transform.position
  //   pos = Vector3.MoveTowards(
  //     pose,
  //     dest,
  //     controllerMovementStep * Time.deltaTime
  //   );
  // }

  public Dictionary<GameObject, InteractableInfo> GetInteractables()
  {
    return interactables;
  }


  // protected void IdentifyInteraction()
  // {
  //   GameObject[] gos = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
  //   foreach (GameObject go in gos)
  //   {
  //     FetchControl(go);
  //     FetchInteractable(go);
  //   }
  // }

  protected void FetchControl(GameObject go)
  {
    EventTrigger r = go.GetComponent<EventTrigger>();
    if (r != null && !interactables.ContainsKey(go))
    {
      // Debug.Log("Found Triggerable: " + go.name);
      interactables[go] = new InteractableInfo(go, "2d");

      // EventTrigger.Entry entry = new EventTrigger.Entry();
      // entry.eventID = EventTriggerType.PointerClick;
      // entry.callback.AddListener((eventData) => { UpdateTrigger(); });
      // r.triggers.Add(entry);
    }
  }

  protected void FetchInteractable(GameObject go)
  {
    UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable xrInteractable = go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
    if (xrInteractable != null && !interactables.ContainsKey(go))
    {
      // Debug.Log("Found Interactable: " + go.name);
      interactables[go] = new InteractableInfo(go, "3d");
    }
  }

  public static void UpdateTrigger()
  {
    // if (controls.ContainsKey(triggered))
    // {
    //   Debug.Log("Triggered Recorded:" + controls[triggered]);
    //   controls[triggered].SetTrigger();
    // }
  }

  // public class ControlInfo
  // {
  //   GameObject control;
  //   int triggered;
  //   public ControlInfo(GameObject obj)
  //   {
  //     this.control = obj;
  //     this.triggered = 0;
  //   }
  //   public GameObject getObject()
  //   {
  //     return this.control;
  //   }
  //   public int getTriggered()
  //   {
  //     return this.triggered;
  //   }
  //   public void SetTrigger()
  //   {
  //     this.triggered = this.triggered + 1;
  //   }
  // }

  public class InteractableInfo
  {
    // TODO add a ''visited'' property, distinguish ''interacted'' and ''visited''
    GameObject interactable;
    bool interacted;
    bool visited;
    String type;
    public InteractableInfo(GameObject go, String type)
    {
      this.interactable = go;
      this.type = type;
      this.interacted = false;
    }
    public GameObject GetObject()
    {
      return this.interactable;
    }

    public void SetInteracted(bool flag)
    {
      this.interacted = flag;
    }
    public bool GetInteracted()
    {
      return this.interacted;
    }

    public void SetVisited(bool flag)
    {
      this.visited = flag;
    }

    public bool GetVisited()
    {
      return this.visited;
    }

    public String GetObjectType()
    {
      return this.type;
    }
  }
}
