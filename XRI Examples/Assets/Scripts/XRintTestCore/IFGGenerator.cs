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
    [MenuItem("Tools/Generate IFG")]
    public static void GenerateIFG()
    {
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        // Rename duplicate GameObjects before processing
        RenameDuplicateGameObjects(rootObjects);
        
        List<object> results = new List<object>();
        List<XRGrabInteractable> allGrabInteractables = new List<XRGrabInteractable>();
        List<XRSocketInteractor> allSocketInteractors = new List<XRSocketInteractor>();

        // Pass 1: Collect all relevant components
        foreach (GameObject rootObj in rootObjects)
        {
            CollectComponents(rootObj, allGrabInteractables, allSocketInteractors);
        }

        // Pass 2: Generate events for XRGrabInteractable (Non-socket interactions)
        foreach (var grab in allGrabInteractables)
        {
            var interactions = new List<string[]>();
            
            // Basic grab interaction
            interactions.Add(new string[] { "grab", "" });

            // Trigger interaction (if activated event has listeners)
            var activatedEvent = grab.activated;
            bool triggerInteraction = activatedEvent.GetPersistentEventCount() > 0;
            if (triggerInteraction)
            {
                interactions.Add(new string[] { "trigger", "grab" });
            }

            var nonSocketInteraction = new
            {
                interactable = grab.name,
                interaction = interactions
            };
            results.Add(nonSocketInteraction);
        }

        // Pass 3: Generate events for XRSocketInteractor
        foreach (var socket in allSocketInteractors)
        {
            int socketMask = socket.interactionLayers.value;
            foreach (var grab in allGrabInteractables)
            {
                int grabMask = grab.interactionLayers.value;
                // Check if they share any interaction layer
                if ((socketMask & grabMask) != 0)
                {
                    var socketInteraction = new
                    {
                        interactor = socket.name,
                        interactable = grab.name,
                        interaction = "socket"
                    };
                    results.Add(socketInteraction);
                }
            }
        }

        string resultJson = JsonConvert.SerializeObject(results, Formatting.Indented);
        string path = Path.Combine(Application.dataPath, "Scripts/IFG.json");
        File.WriteAllText(path, resultJson);
        Debug.Log($"Interaction results exported to {path}");
    }

    private static void CollectComponents(GameObject obj, List<XRGrabInteractable> grabs, List<XRSocketInteractor> sockets)
    {
        if (!obj.activeInHierarchy) return;

        var grab = obj.GetComponent<XRGrabInteractable>();
        if (grab != null && grab.enabled)
        {
            grabs.Add(grab);
        }

        var socket = obj.GetComponent<XRSocketInteractor>();
        if (socket != null && socket.enabled)
        {
            sockets.Add(socket);
        }

        foreach (Transform child in obj.transform)
        {
            CollectComponents(child.gameObject, grabs, sockets);
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
        if (!obj.activeInHierarchy) return;

        list.Add(obj);
        foreach (Transform child in obj.transform)
        {
            CollectAllGameObjects(child.gameObject, list);
        }
    }
}
