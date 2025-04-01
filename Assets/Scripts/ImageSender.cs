using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using PassthroughCameraSamples;

public class ImageSender : MonoBehaviour
{
    [Header("Server Settings")]
    [SerializeField] private string serverUrl = "http://10.0.55.172:5000/api/frame";
    [Tooltip("Your Python server's IP address")]
    [SerializeField] private string serverIP = "10.0.55.172";
    [SerializeField] private int serverPort = 5000;
    
    [Header("Camera Settings")]
    [SerializeField] private WebCamTextureManager webCamTextureManager;
    [SerializeField] private float sendInterval = 0.1f; // 10 frames per second
    [SerializeField] private int imageQuality = 75; // JPEG compression quality (0-100)
    [SerializeField] private int maxImageSize = 640; // Maximum dimension for resizing
    
    [Header("Debug Settings")]
    [SerializeField] private bool showNetworkStatus = true;
    [SerializeField] private bool logSuccessMessages = false;
    
    private float timeSinceLastSend = 0f;
    private bool isServerConnected = false;
    private string statusMessage = "";
    private int framesSent = 0;
    private Texture2D readTexture;
    private bool isProcessing = false;
    
    [Serializable]
    private class FrameData
    {
        public string frame;
    }
    
    private void Start()
    {
        // Update the server URL with the provided IP and port
        serverUrl = $"http://{serverIP}:{serverPort}/api/frame";
        Debug.Log($"ImageSender initialized. Sending images to: {serverUrl}");
        
        // Test connection to the server
        StartCoroutine(TestServerConnection());
    }
    
    private IEnumerator TestServerConnection()
    {
        using (UnityWebRequest request = UnityWebRequest.Get($"http://{serverIP}:{serverPort}/api/ping"))
        {
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                isServerConnected = true;
                Debug.Log($"Successfully connected to Python server at {serverIP}:{serverPort}");
                statusMessage = "Server connected";
            }
            else
            {
                isServerConnected = false;
                Debug.LogWarning($"Could not connect to Python server at {serverIP}:{serverPort}. Error: {request.error}");
                statusMessage = "Server disconnected";
            }
        }
    }
    
    private void Update()
    {
        timeSinceLastSend += Time.deltaTime;
        
        if (timeSinceLastSend >= sendInterval && webCamTextureManager.WebCamTexture != null && isServerConnected && !isProcessing)
        {
            timeSinceLastSend = 0f;
            StartCoroutine(CaptureAndSendFrame());
        }
    }
    
    private IEnumerator CaptureAndSendFrame()
    {
        isProcessing = true;
        
        // Get the current WebCamTexture
        WebCamTexture webCamTexture = webCamTextureManager.WebCamTexture;
        
        // Create texture if it doesn't exist or if size changed
        if (readTexture == null || readTexture.width != webCamTexture.width || readTexture.height != webCamTexture.height)
        {
            if (readTexture != null)
                Destroy(readTexture);
                
            readTexture = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGB24, false);
        }
        
        // Read pixels from WebCamTexture
        readTexture.SetPixels(webCamTexture.GetPixels());
        readTexture.Apply();
        
        // Resize if needed
        Texture2D processedTexture = readTexture;
        if (maxImageSize > 0 && (readTexture.width > maxImageSize || readTexture.height > maxImageSize))
        {
            processedTexture = ResizeTexture(readTexture, maxImageSize);
        }
        
        // Convert to JPEG
        byte[] jpgBytes = processedTexture.EncodeToJPG(imageQuality);
        
        // Convert to Base64
        string base64Image = Convert.ToBase64String(jpgBytes);
        
        // Create payload
        FrameData frameData = new FrameData { frame = base64Image };
        string jsonData = JsonUtility.ToJson(frameData);
        
        // Send to server
        yield return StartCoroutine(SendImageToServer(jsonData));
        
        // Clean up if we created a separate texture for resizing
        if (processedTexture != readTexture)
        {
            Destroy(processedTexture);
        }
        
        isProcessing = false;
    }
    
    private Texture2D ResizeTexture(Texture2D source, int maxSize)
    {
        int width = source.width;
        int height = source.height;
        float aspectRatio = (float)width / height;
        
        if (width > height)
        {
            width = maxSize;
            height = Mathf.RoundToInt(width / aspectRatio);
        }
        else
        {
            height = maxSize;
            width = Mathf.RoundToInt(height * aspectRatio);
        }
        
        // Create a new texture with the target size
        Texture2D result = new Texture2D(width, height, source.format, false);
        
        // Scale using bilinear filtering
        float scaleX = 1.0f / ((float)source.width / width);
        float scaleY = 1.0f / ((float)source.height / height);
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color color = source.GetPixelBilinear(x * scaleX, y * scaleY);
                result.SetPixel(x, y, color);
            }
        }
        
        result.Apply();
        return result;
    }
    
    private IEnumerator SendImageToServer(string jsonData)
    {
        using (UnityWebRequest request = new UnityWebRequest(serverUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 5; // 5 second timeout
            
            yield return request.SendWebRequest();
            
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error sending image: {request.error}");
                isServerConnected = false;
                statusMessage = $"Send error: {request.error}";
                
                // Test connection again
                StartCoroutine(TestServerConnection());
            }
            else
            {
                framesSent++;
                if (logSuccessMessages)
                {
                    Debug.Log($"Image sent successfully (frame {framesSent})");
                }
                statusMessage = $"Frames sent: {framesSent}";
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
            
            // Display server status in bottom-left corner
            GUI.Label(new Rect(10, Screen.height - 60, 500, 50), 
                isServerConnected ? 
                    $"Python server: {serverIP}:{serverPort} | {statusMessage}" : 
                    $"Server disconnected: {serverIP}:{serverPort}", 
                networkStyle);
        }
    }
    
    private void OnDestroy()
    {
        if (readTexture != null)
        {
            Destroy(readTexture);
        }
    }
}
