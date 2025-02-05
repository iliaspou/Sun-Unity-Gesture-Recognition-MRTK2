using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Microsoft;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.Toolkit.Input;
using TMPro;
using Unity.Barracuda;

public class NextGestureDetection : MonoBehaviour
{
    // Select Hand
    Handedness handedness = Handedness.Right;

    // Joints Marker
    public GameObject jointsMarker;
    GameObject[] cubeJoints = new GameObject[27];
    public bool renderJoints = true;

    // Joints tracking FPS
    public int FPS = 60;

    // Hand joint data
    Vector3[] jointsPos;
    Quaternion[] jointsRot;

    // Onxx model 
    [SerializeField]
    private NNModel onnxModel;
    private Model runtimeModel;
    private IWorker worker;
    private int modelInputSize;
    private int modelOutputSize;

    // Panel to show prediction
    [SerializeField]
    private TextMeshProUGUI predictionDisplayPanel;

    // Predictions
    public float predictionThreshold = 0.5f;

    // Joints
    private int numOfJoints = 26;

    // Event to notify other components
    public delegate void GesturePerformed(string gestureName);
    public static event GesturePerformed OnGesturePerformed;

    // Controll the gesture detection process (start/stop)
    private bool isDetectionEnabled = true;


    // Two Fingers dynamic gesture detection
    bool dynamicTwoFingersSpeedCheck_1 = false;
    bool dynamicTwoFingersSpeedCheck_2 = false;
    float dynamicTwoFingersSpeed;
    int dynamicTwoFingersSpotsCounter = 0; // In how many frames did we find the static-two-fingers gesture during the current dynamic-two-fingers gesture search
    int dynamicTwoFingersSearchCounter = 0; // In how many frames did we find the static-two-fingers gesture during the current dynamic-two-fingers gesture search
    float twoFingersMeanPos_Prev = -1f;
    float twoFingersMeanPos_Curr = -1f;
    bool dynamicTwoFingersSearchEnabled = false;


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

        runtimeModel = ModelLoader.Load(onnxModel);
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.ComputePrecompiled, runtimeModel);
        modelInputSize = numOfJoints - 1 + 4 * numOfJoints;
        modelOutputSize = 4;
        jointsPos = new Vector3[numOfJoints];
        jointsRot = new Quaternion[numOfJoints];
    }

    void Update()
    {
        int prediction;
        // Hide the joint markers on update
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
            LoadHandJointData();

            if (renderJoints)
            {
                RenderJoints();
            }
            if (isDetectionEnabled)
            {
                prediction = SearchForStaticGestures();
                // if (prediction == 3)
                // {
                //     SearchForTwoFingersDynamicGesture();
                // }
            }
            return;
        }
        else
        {
            predictionDisplayPanel.text = "";
            return;
        }
    }

    private void LoadHandJointData()
    {
        // Loop through all joints
        foreach (TrackedHandJoint joint in System.Enum.GetValues(typeof(TrackedHandJoint)))
        {
            if ((int)joint != 0)
            {
                if (HandJointUtils.TryGetJointPose(joint, handedness, out MixedRealityPose jointPose))
                {
                    jointsPos[(int)joint - 1] = jointPose.Position;
                    jointsRot[(int)joint - 1] = jointPose.Rotation;
                }
            }
        }
    }

    // private bool SearchForTwoFingersDynamicGesture()
    // {
    //     if (twoFingersMeanPos_Prev == -1f)
    //     {
    //         return false;
    //     }
    // }

    private void StopTwoFingersDynamicSearch()
    {

    }

    private int SearchForStaticGestures()
    {

        float[] distances;
        float[] flattened;
        float[] prediction;


        distances = CalculateDistancesFromRoot(jointsPos);
        flattened = FlattenData(distances, jointsRot);
        prediction = Predict(flattened);

        float predMax = prediction[0];
        int predMaxIdx = 0;
        string predictionStr = "[ " + prediction[0].ToString();

        for (int i = 1; i < 4; i++)
        {
            predictionStr += ", " + prediction[i].ToString();
        }
        predictionStr += " ]";
        Debug.Log(predictionStr);

        for (int i = 1; i < modelOutputSize; i++)
        {
            if (prediction[i] > predMax)
            {
                predMax = prediction[i];
                predMaxIdx = i;
            }
        }

        if (predMax > predictionThreshold)
        {
            if (predMaxIdx == 0)
            {
                predictionDisplayPanel.text = "";
            }
            if (predMaxIdx == 1)
            {
                Debug.Log("Thumbs Up, " + predMax);
                predictionDisplayPanel.text = "Thumbs Up!";
            }
            if (predMaxIdx == 2)
            {
                Debug.Log("Thumbs Down, " + predMax);
                predictionDisplayPanel.text = "Thumbs Down!";
            }
            if (predMaxIdx == 3)
            {
                Debug.Log("Two Fingers, " + predMax);
                predictionDisplayPanel.text = "Two Fingers!";
            }
            return predMaxIdx;
        }
        else
        {
            predictionDisplayPanel.text = "";
            return 0;
        }
    }


    private float[] CalculateDistancesFromRoot(Vector3[] jointsPos)
    {
        // Create an array to store the distances
        float[] distances = new float[jointsPos.Length - 1];

        // Get the first point
        Vector3 firstPoint = jointsPos[0];

        // Calculate the distance of each point to the first point
        for (int i = 1; i < jointsPos.Length; i++)
        {
            distances[i - 1] = Vector3.Distance(firstPoint, jointsPos[i]);
        }

        return distances;
    }


    private float[] FlattenData(float[] distances, Quaternion[] jointsRot)
    {
        float[] flattened = new float[modelInputSize];
        for (int i = 0; i < distances.Length; i++)
        {
            flattened[i] = distances[i];
        }
        int offset = 25;

        for (int i = 0; i < jointsRot.Length; i++)
        {
            flattened[i * 4 + 0 + offset] = jointsRot[i].x;
            flattened[i * 4 + 1 + offset] = jointsRot[i].y;
            flattened[i * 4 + 2 + offset] = jointsRot[i].z;
            flattened[i * 4 + 3 + offset] = jointsRot[i].w;
        }

        return flattened;
    }


    private float[] Predict(float[] input)
    {
        Tensor inputTensor = new Tensor(1, modelInputSize, input);
        Tensor outputTensor = null;
        float[] prediction = new float[modelOutputSize];

        try
        {
            // Execute inference
            worker.Execute(inputTensor);
            outputTensor = worker.PeekOutput();

            // The network outputs 4 values
            for (int i = 0; i < modelOutputSize; i++)
            {
                prediction[i] = outputTensor[i];
            }
        }
        finally
        {
            // Dispose of tensors
            inputTensor.Dispose();
            outputTensor?.Dispose();
        }

        return prediction;
    }


    private void RenderJoints()
    {
        // Loop through all joints
        foreach (TrackedHandJoint joint in System.Enum.GetValues(typeof(TrackedHandJoint)))
        {

            if (HandJointUtils.TryGetJointPose(joint, handedness, out MixedRealityPose jointPose))
            {
                if ((int)joint != 0)
                {
                    cubeJoints[(int)joint].GetComponent<Renderer>().enabled = true;
                    cubeJoints[(int)joint].transform.position = jointPose.Position;
                    cubeJoints[(int)joint].transform.rotation = jointPose.Rotation;
                }
            }
        }
    }

    private void NotifySubscribers(string gestureName)
    {
        // Invoke the event to notify listeners
        OnGesturePerformed?.Invoke(gestureName);
    }

    public void StartRecognition()
    {
        isDetectionEnabled = true;
        Debug.Log("Gesture detection started.");
    }

    public void StopRecognition()
    {
        isDetectionEnabled = false;
        Debug.Log("Gesture detection stopped.");
    }

    void OnDestroy()
    {
        // Dispose of worker to release resources
        worker?.Dispose();
    }
}
