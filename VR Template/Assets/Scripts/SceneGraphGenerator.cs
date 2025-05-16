using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Newtonsoft.Json;
using System.Linq;

public class SceneGraphGenerator : EditorWindow
{
    [MenuItem("Tools/Generate Scene Graph")]
    public static void GenerateSceneGraph()
    {
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        // Rename duplicate GameObjects before processing
        RenameDuplicateGameObjects(rootObjects);
        List<Utils.InteractionEvent> results = new List<Utils.InteractionEvent>();

        foreach (GameObject rootObj in rootObjects)
        {
            ProcessGameObject(rootObj, results);
        }

        string json = JsonConvert.SerializeObject(results, Formatting.Indented);
        string path = Path.Combine(Application.dataPath, "Scripts/scene_graph.json");
        File.WriteAllText(path, json);
        Debug.Log($"Interaction results exported to {path}");
    }

    private static void ProcessGameObject(GameObject obj, List<Utils.InteractionEvent> results)
    {
        // Check grab interactions
        var grabInteractable = obj.GetComponent<XRGrabInteractable>();
        if (grabInteractable != null)
        {
            var result = new Utils.InteractionEvent
            {
                interactor = "XR Origin (XR Rig)",
                condition = new List<string>(),
                interactable = obj.name,
                interaction_type = "grab"
            };
            results.Add(result);
            // Check trigger interactions
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
        }

        foreach (Transform child in obj.transform)
        {
            ProcessGameObject(child.gameObject, results);
        }
    }

    private static List<string> GetComponentsList(GameObject obj)
    {
        Component[] components = obj.GetComponents<Component>();
        List<string> componentNames = new List<string>();

        foreach (Component component in components)
        {
            if (component != null)
            {
                componentNames.Add(component.GetType().Name);
            }
        }

        return componentNames;
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
