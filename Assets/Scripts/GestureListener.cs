using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GestureListener : MonoBehaviour
{
    void OnEnable()
    {
        GestureDetector.OnGesturePerformed += HandleGesture;
    }

    void OnDisable()
    {
        GestureDetector.OnGesturePerformed -= HandleGesture;
    }

    private void HandleGesture(string gestureName)
    {
        Debug.Log("Gesture detected: " + gestureName);
        if (gestureName == "NotOkGesture")
        {
            // Trigger your functionality here
        }
    }
}
