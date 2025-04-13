// using UnityEngine;

// public class ControllerAction
// {
//     // private GameObject leftController;
//     private GameObject controller;
//     private float controllerMovementStep = 0.1f;
//     private bool movementCompleted;
//     private bool interactionCompleted;
//     // private string controllerType;

//     public ControllerAction(string controllerType)
//     {
//         if (controllerType == "left")
//         {
//             controller = GameObject.FindWithTag("LeftController");
//         }
//         else if (controllerType == "right")
//         {
//             controller = GameObject.FindWithTag("RightController");
//         }
//         else
//         {
//             Debug.LogError("Please create the controller with a valid type");
//         }
//     }
//     IEnumerator ControllerMovement(Vector3 targetPos)
//     {
//         Debug.Log("ControllerMovement: " + targetPos);
//         controller.transform.position = Vector3.MoveTowards(
//             controller.transform.position,
//             targetPos,
//             controllerMovementStep * Time.deltaTime
//         );
//     }

//     public void ControllerGrip()
//     {

//     }

// }
