using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Microsoft;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.Toolkit.Input;
using TMPro;
using Unity.Barracuda;

public class GestureDetector : MonoBehaviour
{
    // Select Hand
    Handedness handedness = Handedness.Right;

    // Joints Marker
    public GameObject jointsMarker;
    GameObject[] cubeJoints = new GameObject[27];
    public bool renderJoints = true;

    // Joints tracking FPS
    public int FPS = 60;
    float period, timeInterval;

    // Time Window
    List<Vector3[]> windowData = new List<Vector3[]>();
    public int maxWindowCapacity = 11;
    float[] prediction = new float[2];
    bool insertDataToWindow = false;
    Vector3[] currJoints, prevJoints, jointsDiff;

    // Onxx model 
    [SerializeField]
    private NNModel onnxModel;
    private Model runtimeModel;
    private IWorker worker;
    private int modelInputSize;

    // Panel to show prediction
    [SerializeField]
    private TextMeshProUGUI predictionDisplayPanel;
    public float predictionDisplayDuration = 2f;
    float predictionDisplayTimer = 0;

    // Predictions
    public float predictionThreshold = 0.6f;
    bool isPredictionPaused = false; // after a positive prediction we pause the prediction process to prevent multiple detections of the same gesture
    public float predictionPauseDuration = 0.5f;
    float predictionPauseTimer = 0f;

    // Joints
    private int numOfJoints = 26;

    // Event to notify other components
    public delegate void GesturePerformed(string gestureName);
    public static event GesturePerformed OnGesturePerformed;

    // Controll the gesture detection process (start/stop)
     private bool isDetectionEnabled = true;

    void Start()
    {
        if (renderJoints)
        {
            for (int i = 0; i < cubeJoints.Length; i++)
            {
                cubeJoints[i] = Instantiate(jointsMarker, this.transform);
            }
        }

        period = 1 / FPS;

        runtimeModel = ModelLoader.Load(onnxModel);
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.ComputePrecompiled, runtimeModel);
        modelInputSize = maxWindowCapacity * 3 * numOfJoints;
    }

    void Update()
    {
        // Hide the joint markers on update
        if (renderJoints)
        {
            for (int i = 0; i < cubeJoints.Length; i++)
            {
                cubeJoints[i].GetComponent<Renderer>().enabled = false;
            }
        }

        // Clear the prediction panel 
        if (predictionDisplayTimer > 0.0f)
        {
            predictionDisplayTimer -= Time.unscaledDeltaTime;
            if (predictionDisplayTimer < 0.0f)
            {
                predictionDisplayPanel.text = "";
                predictionDisplayTimer = 0;
            }
        }

        // Restart the gesture prediction
        if (predictionPauseTimer > 0.0f)
        {
            predictionPauseTimer -= Time.unscaledDeltaTime;
            if (predictionPauseTimer < 0.0f)
            {
                isPredictionPaused = false;
                predictionPauseTimer = 0.0f;
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
            if (FpsController() && !isPredictionPaused && isDetectionEnabled)
            {
                ProcessFrame();
            }
            return;
        }
        else
        {
            windowData.Clear();
            insertDataToWindow = false;
            return;
        }
    }

    private void ProcessFrame()
    {
        // Loop through all joints
        Vector3[] jointsPos = new Vector3[numOfJoints];
        foreach (TrackedHandJoint joint in System.Enum.GetValues(typeof(TrackedHandJoint)))
        {
            if (HandJointUtils.TryGetJointPose(joint, handedness, out MixedRealityPose jointPose))
            {
                if ((int)joint != 0)
                {
                    jointsPos[(int)joint - 1] = jointPose.Position;
                }
            }
        }

        if (insertDataToWindow == false)
        {
            prevJoints = jointsPos;
            insertDataToWindow = true;
            return;
        }
        else
        {
            currJoints = jointsPos;
            jointsDiff = JointsPositionsDifference(currJoints, prevJoints);
            prevJoints = currJoints;
            windowData.Insert(0, jointsDiff);
            if (windowData.Count == maxWindowCapacity)
            {

                prediction = Predict(FlattenWindowData());
                if (prediction[1] > predictionThreshold)
                {
                    NotifySubscribers("NotOkGesture");
                    // Debug.Log("NotOk:" + prediction[1]);
                    predictionDisplayPanel.text = "NOT OK GESTURE!";
                    predictionDisplayTimer = predictionDisplayDuration;
                    windowData.Clear();
                    insertDataToWindow = false;
                    isPredictionPaused = true;
                    predictionPauseTimer = predictionPauseDuration;
                }
                else
                {
                    windowData.RemoveAt(windowData.Count - 1);
                }
            }
        }
    }

    private float[] Predict(float[] input)
    {
        Tensor inputTensor = new Tensor(1, modelInputSize, input);
        Tensor outputTensor = null;
        float[] prediction = new float[2];

        try
        {
            // Execute inference
            worker.Execute(inputTensor);
            outputTensor = worker.PeekOutput();

            // The network outputs two values: The first one is the probability of the gesture to be a random gesture and the second one is the probability to be a 'notOk' gesture.
            for (int i = 0; i < 2; i++)
            {
                prediction[i] = outputTensor[i];
            }
            // Debug.Log(outputTensor.length);
        }
        finally
        {
            // Dispose of tensors
            inputTensor.Dispose();
            outputTensor?.Dispose();
        }

        return prediction;
    }

    private float[] FlattenWindowData()
    {
        float[] flattened = new float[modelInputSize];
        for (int i = 0; i < maxWindowCapacity; i++)
        {
            for (int j = 0; j < numOfJoints; j++)
            {
                flattened[i * numOfJoints + j * 3] = windowData[i][j].x;
                flattened[i * numOfJoints + j * 3 + 1] = windowData[i][j].y;
                flattened[i * numOfJoints + j * 3 + 2] = windowData[i][j].z;
            }
        }
        return flattened;
    }

    private Vector3[] JointsPositionsDifference(Vector3[] a, Vector3[] b)
    {
        Vector3[] c = new Vector3[numOfJoints];
        for (int i = 0; i < numOfJoints; i++)
        {
            c[i] = a[i] - b[i];
            // Debug.Log(c[i]);
        }
        return c;
    }

    private void RenderJoints()
    {
        for (int i = 0; i < cubeJoints.Length; i++)
        {
            cubeJoints[i].GetComponent<Renderer>().enabled = false;
        }
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
                }
            }
        }
    }

    private bool FpsController()
    {
        timeInterval += Time.unscaledDeltaTime;
        if (timeInterval < period)
        {
            return false;
        }
        else
        {
            timeInterval = timeInterval - period;
            return true;
        }
    }

    private void NotifySubscribers(string gestureName)
    {
        // Invoke the event to notify listeners
        OnGesturePerformed?.Invoke(gestureName);
        // Debug.Log($"Gesture Detected: {gestureName}");
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
