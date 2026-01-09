using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Newtonsoft.Json;
using System.Linq;

public class IFGGenerator : EditorWindow
{

    private static List<string> interactionScripts = new List<string> { 
        "XRGrabInteractable", "XRSocketInteractor"};

    [MenuItem("Tools/Generate IFG")]
    public static void GenerateIFG()
    {
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        // Rename duplicate GameObjects before processing
        RenameDuplicateGameObjects(rootObjects);
        List<Utils.InteractionEvent> results = new List<Utils.InteractionEvent>();

        foreach (GameObject rootObj in rootObjects)
        {
            ProcessGameObject(rootObj, results);
        }

        string resultJson = JsonConvert.SerializeObject(results, Formatting.Indented);
        string path = Path.Combine(Application.dataPath, "Scripts/IFG.json");
        File.WriteAllText(path, resultJson);
        Debug.Log($"Interaction results exported to {path}");
    }

    private static void ProcessGameObject(GameObject obj, List<Utils.InteractionEvent> results)
    {
        foreach (var script in interactionScripts)
        {
            var component = obj.GetComponent(script);
            if (component != null)
            {
                var result = new Utils.InteractionEvent
                {
                    interactor = "XR Origin (XR Rig)",
                    condition = new List<string>(),
                    interactable = obj.name,
                    interaction_type = "grab"
                };
                results.Add(result);
                if (script == "XRGrabInteractable")
                {
                    var grabInteractable = component as XRGrabInteractable;
                    var activatedEvent = grabInteractable.activated;
                    bool triggerInteraction = activatedEvent.GetPersistentEventCount() > 0;
                    if (triggerInteraction)
                    {
                        var triggerResult = new Utils.InteractionEvent
                        {
                            interactor = "XR Origin (XR Rig)",
                            condition = new List<string> { "grab" },
                            interactable = obj.name,
                            interaction_type = "trigger"
                        };
                        results.Add(triggerResult);
                    }
                } else if (script == "XRSocketInteractor") {
                    var socketInteractor = component as XRSocketInteractor;
                    var layerMask = socketInteractor.interactionLayers.value;
                    Debug.Log($"Socket Interactor {socketInteractor.name} interaction layers: {layerMask}");
                }
                break;
            }
        }

        foreach (Transform child in obj.transform)
        {
            ProcessGameObject(child.gameObject, results);
        }
    }



    // Add this method to rename duplicate GameObjects
    private static void RenameDuplicateGameObjects(GameObject[] rootObjects)
    {
        Dictionary<string, int> nameCounts = new Dictionary<string, int>();
        List<GameObject> allObjects = new List<GameObject>();
        foreach (GameObject root in rootObjects)
        {
            CollectAllGameObjects(root, allObjects);
        }
        // Group by name
        var grouped = allObjects.GroupBy(obj => obj.name);
        foreach (var group in grouped)
        {
            if (group.Count() > 1)
            {
                int index = 1;
                foreach (var obj in group)
                {
                    obj.name = $"{group.Key} {index}";
                    index++;
                }
            }
        }
    }

    // Helper to collect all GameObjects in the hierarchy
    private static void CollectAllGameObjects(GameObject obj, List<GameObject> list)
    {
        list.Add(obj);
        foreach (Transform child in obj.transform)
        {
            CollectAllGameObjects(child.gameObject, list);
        }
    }
}
