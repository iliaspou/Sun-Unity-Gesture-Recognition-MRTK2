using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

using Microsoft;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.Toolkit.Input;

// using Microsoft.MixedReality.Toolkit;
// using Microsoft.MixedReality.Toolkit.WindowsMixedReality;
// using UnityEngine.XR.Management;

using Microsoft.MixedReality.Toolkit;
// using Windows.Perception.Spatial; 

public class HandJointsCollector : MonoBehaviour
{
    // Select Hand
    Handedness handedness = Handedness.Right;

    // Joints Marker
    public GameObject jointsMarker;
    GameObject[] cubeJoints = new GameObject[27];

    // Save Joints
    public string outputFolder;
    string outputFilePath;

    // Fps
    public int FPS = 60;

    // Gesture
    public string gesture;

    public bool renderJoints = true;
    public bool saveJoints = true;

    /* Control data collection rate */
    // public bool controlDataCollectionRate = false;
    // float period, timeInterval;

    void Start()
    {
        Application.targetFrameRate = FPS;
        if (renderJoints)
        {
            for (int i = 0; i < cubeJoints.Length; i++)
            {
                cubeJoints[i] = Instantiate(jointsMarker, this.transform);
            }
        }

        if (saveJoints)
        {
            CreateOutputFolder();
            outputFilePath = CreateOutputFile();
        }

        /* Control data collection rate */
        // if (controlDataCollectionRate)
        // {
        //     period = 1/(float)FPS;
        //     Debug.Log("Period: " + period);
        // }
    }

    void Update()
    {
        // Debug.Log("Time: " + Time.unscaledDeltaTime);
        if (renderJoints)
        {
            for (int i = 0; i < cubeJoints.Length; i++)
            {
                cubeJoints[i].GetComponent<Renderer>().enabled = false;
            }
        }

        // Check if hand exist
        if (HandJointUtils.TryGetJointPose(TrackedHandJoint.Palm, handedness, out MixedRealityPose palmPose))
        {
            // Debug.Log($"Hand: {handedness}, Palm Position: {palmPose.Position}, Palm Rotation: {palmPose.Rotation}");
            if (renderJoints)
            {
                RenderJoints();
            }

            if (saveJoints)
            {
                SaveJoints();

                /* Control data collection rate */
                // if (!controlDataCollectionRate)
                // {
                    
                //     SaveJoints();
                // }
                // else 
                // {
                //     if (fpsController())
                //     {
                //         SaveJoints();
                //     }
                // }
            }
        }
    }

    private void SaveJoints()
    {
        string jointsStr = "";
        // Loop through all joints
        foreach (TrackedHandJoint joint in System.Enum.GetValues(typeof(TrackedHandJoint)))
        {
            if ((int)joint != 0)
            {
                if (HandJointUtils.TryGetJointPose(joint, handedness, out MixedRealityPose jointPose))
                {
                    Vector3 position = jointPose.Position;
                    Quaternion rotation = jointPose.Rotation;
                    // Debug.Log(position);
                    // Debug.Log(rotation);
                    jointsStr += jointPose.Position.ToString("F4") + " " + jointPose.Rotation.ToString("F4") + ",";
                }
            }
        }
        string msg = (Time.time * 1000).ToString() + " | " + jointsStr + "\n";
        File.AppendAllText(outputFilePath, msg);
        Debug.Log("Joints saved");
    }

    private void RenderJoints()
    {
        // for (int i = 0; i < cubeJoints.Length; i++)
        // {
        //     cubeJoints[i].GetComponent<Renderer>().enabled = false;
        // }

        // Loop through all joints
        foreach (TrackedHandJoint joint in System.Enum.GetValues(typeof(TrackedHandJoint)))
        {

            if (HandJointUtils.TryGetJointPose(joint, handedness, out MixedRealityPose jointPose))
            {
                if ((int)joint != 0)
                {
                    Vector3 position = jointPose.Position;
                    Quaternion rotation = jointPose.Rotation;
                    // Debug.Log($"Joint: {joint}, Position: {position}, Rotation: {rotation}");

                    cubeJoints[(int)joint].GetComponent<Renderer>().enabled = true;
                    cubeJoints[(int)joint].transform.position = jointPose.Position;
                    cubeJoints[(int)joint].transform.rotation = jointPose.Rotation;

                    // Debug.Log(transform.TransformPoint(jointPose.Position));
                }
            }
        }
    }

    /* Control data collection rate */
    // private bool fpsController()
    // {
    //     timeInterval += Time.unscaledDeltaTime;
    //     if (timeInterval < period)
    //     {
    //         Debug.Log("False");
    //         return false;
    //     }
    //     else
    //     {
    //         Debug.Log("True");
    //         timeInterval = timeInterval - period;
    //         return true;
    //     }
    // }

    private void CreateOutputFolder()
    {
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
            Debug.Log("Folder " + outputFolder + " created successfully.");
        }
    }

    private string CreateOutputFile()
    {
        int gestureFileCount = 0;
        try
        {
            // Get the all the files and return them as an array
            string[] subfiles = GetSubfiles(outputFolder);

            // Print the subfiles
            Debug.Log("Subfiles in: " + outputFolder);
            foreach (string file in subfiles)
            {
                if (file.Contains("right_hand_60fps_" + gesture))
                {
                    gestureFileCount++;
                }
            }
            Debug.Log(gestureFileCount);
        }
        catch (Exception ex)
        {
            Debug.Log("Error: " + ex.Message);
        }
        string filePath = outputFolder + "right_hand_" + FPS + "fps_" + gesture + "_" + (gestureFileCount + 1).ToString() + ".txt";
        Debug.Log(filePath);
        return filePath;
    }

    static string[] GetSubfiles(string folderPath)
    {
        if (Directory.Exists(folderPath))
        {
            // Get all files within the specified folder path
            return Directory.GetFiles(folderPath);

        }
        else
        {
            throw new DirectoryNotFoundException($"The directory '{folderPath}' does not exist.");
        }
    }
}
