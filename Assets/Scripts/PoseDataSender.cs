using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

[Serializable]
public class PoseData
{
    public List<KeypointData> keypoints = new List<KeypointData>();
}

[Serializable]
public class KeypointData
{
    public int index;
    public float x;
    public float y;
    public float z;
    public bool active;

    public KeypointData(int idx, Vector3 position, bool isActive)
    {
        index = idx;
        x = position.x;
        y = position.y;
        z = position.z;
        active = isActive;
    }
}

public class PoseDataSender : MonoBehaviour
{
    [Header("Server Settings")]
    [SerializeField] private string serverUrl = "http://10.0.55.172:3000/pose-data";
    [Tooltip("Your laptop's IP address on the local network")]
    [SerializeField] private string serverIP = "10.0.55.172";
    [SerializeField] private int serverPort = 3000;
    
    [Header("Input Settings")]
    [SerializeField] private bool useButtonTrigger = true;
    [Tooltip("Only applies when button trigger is disabled")]
    [SerializeField] private float sendInterval = 0.1f;
    [SerializeField] private bool showButtonPrompt = true;
    [Tooltip("Cooldown between button presses (seconds)")]
    [SerializeField] private float buttonCooldown = 0.5f;
    
    [Header("Data Settings")]
    [SerializeField] private PosePreview posePreview;
    [SerializeField] private bool sendOnlyActiveKeypoints = true;
    [SerializeField] private bool logSuccessMessages = false;
    
    [Header("Network Status")]
    [SerializeField] private bool showNetworkStatus = true;
    [SerializeField] private int maxRetryCount = 3;
    
    private float timeSinceLastSend = 0f;
    private float timeSinceLastButtonPress = 0f;
    private PoseData poseData = new PoseData();
    private int failedRequestCount = 0;
    private bool isServerConnected = false;
    private bool dataSentThisFrame = false;
    private string statusMessage = "";
    
    private void Start()
    {
        // Update server URL using the IP and port provided
        serverUrl = $"http://{serverIP}:{serverPort}/pose-data";
        Debug.Log($"PoseDataSender initialized. Sending data to: {serverUrl}");
        
        // Test connection to server
        StartCoroutine(TestServerConnection());
    }
    
    private IEnumerator TestServerConnection()
    {
        using (UnityWebRequest request = UnityWebRequest.Get($"http://{serverIP}:{serverPort}/"))
        {
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                isServerConnected = true;
                Debug.Log($"Successfully connected to server at {serverIP}:{serverPort}");
            }
            else
            {
                isServerConnected = false;
                Debug.LogWarning($"Could not connect to server at {serverIP}:{serverPort}. Error: {request.error}. Make sure the server is running and IP is correct.");
            }
        }
    }
    
    private void Update()
    {
        timeSinceLastSend += Time.deltaTime;
        timeSinceLastButtonPress += Time.deltaTime;

        if (useButtonTrigger)
        {
            // Change button mapping to use a more reliable VR controller button
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger) && timeSinceLastButtonPress >= buttonCooldown)
            {
                Debug.Log("Primary Index Trigger pressed");
                if (posePreview != null && posePreview.gameObject.activeSelf)
                {
                    timeSinceLastButtonPress = 0f;
                    CollectAndSendPoseData();
                }
            }
        }
        else if (timeSinceLastSend >= sendInterval && posePreview != null && posePreview.gameObject.activeSelf)
        {
            timeSinceLastSend = 0f;
            CollectAndSendPoseData();
        }
    }
    
    private void CollectAndSendPoseData()
    {
        Debug.Log("CollectAndSendPoseData called");
        poseData.keypoints.Clear();
        
        for (int i = 0; i < posePreview.keypoints.Length; i++)
        {
            var keypoint = posePreview.keypoints[i];
            if (keypoint != null)
            {
                if (!sendOnlyActiveKeypoints || keypoint.IsActive)
                {
                    poseData.keypoints.Add(new KeypointData(i, keypoint.Position, keypoint.IsActive));
                }
            }
        }
        
        StartCoroutine(SendPoseDataToServer());
    }
    
    private IEnumerator SendPoseDataToServer()
    {
        string jsonData = JsonUtility.ToJson(poseData);
        
        using (UnityWebRequest request = new UnityWebRequest(serverUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 5; // Set timeout to 5 seconds
            
            yield return request.SendWebRequest();
            
            if (request.result != UnityWebRequest.Result.Success)
            {
                failedRequestCount++;
                statusMessage = $"Error: {request.error}";
                Debug.LogError($"Error sending pose data: {request.error}. Failed requests: {failedRequestCount}");
                
                if (failedRequestCount >= maxRetryCount && isServerConnected)
                {
                    Debug.LogWarning("Multiple request failures. Retesting server connection...");
                    isServerConnected = false;
                    StartCoroutine(TestServerConnection());
                }
            }
            else
            {
                if (logSuccessMessages)
                {
                    Debug.Log("Pose data sent successfully");
                }
                
                statusMessage = "Pose data sent ✓";
                failedRequestCount = 0;
                isServerConnected = true;
            }
        }
    }
    
    private void OnGUI()
    {
        if (showNetworkStatus)
        {
            GUIStyle networkStyle = new GUIStyle();
            networkStyle.fontSize = 24;
            networkStyle.normal.textColor = isServerConnected ? Color.green : Color.red;
            
            // Display server connection status in top-left corner
            GUI.Label(new Rect(10, 10, 500, 50), 
                isServerConnected ? 
                    $"Server connected: {serverIP}:{serverPort}" : 
                    $"Server disconnected: {serverIP}:{serverPort}", 
                networkStyle);
            
            // Display button prompt if enabled
            if (showButtonPrompt && useButtonTrigger)
            {
                GUIStyle promptStyle = new GUIStyle();
                promptStyle.fontSize = 24;
                promptStyle.normal.textColor = Color.white;
                promptStyle.alignment = TextAnchor.MiddleCenter;
                
                GUI.Label(new Rect(Screen.width/2 - 200, Screen.height - 100, 400, 50), 
                    "Press 'A' button to send pose data", 
                    promptStyle);
            }
            
            // Display status message
            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUIStyle statusStyle = new GUIStyle();
                statusStyle.fontSize = 24;
                statusStyle.normal.textColor = statusMessage.Contains("Error") ? Color.red : 
                                             statusMessage.Contains("✓") ? Color.green : Color.yellow;
                statusStyle.alignment = TextAnchor.MiddleCenter;
                
                GUI.Label(new Rect(Screen.width/2 - 200, Screen.height - 150, 400, 50), 
                    statusMessage, 
                    statusStyle);
            }
        }
    }
}
