using System.Collections;
using Meta.XR.Samples;
using UnityEngine;
using UnityEngine.UI;

namespace PassthroughCameraSamples.CameraViewer
{
    [MetaCodeSample("PassthroughCameraApiSamples-CameraViewer")]
    public class CameraViewerManager : MonoBehaviour
    {
        // Create a field to attach the reference to the WebCamTextureManager prefab
        [SerializeField] private WebCamTextureManager m_webCamTextureManager;
        [SerializeField] private Text m_debugText;
        [SerializeField] private RawImage m_image;

        // Reference to your other script that needs the Texture2D
        [SerializeField] private PoseDetection m_poseDetectionScript;

        [SerializeField] private float m_textureUpdateInterval = 1f;
        private float m_timeSinceLastUpdate = 0f;

        private IEnumerator Start()
        {
            while (m_webCamTextureManager.WebCamTexture == null)
            {
                yield return null;
            }
            m_debugText.text += "\nWebCamTexture Object ready and playing.";
            // Set WebCamTexture GPU texture to the RawImage Ui element
            m_image.texture = m_webCamTextureManager.WebCamTexture;
        }

        private void Update()
        {
            m_debugText.text = PassthroughCameraPermissions.HasCameraPermission == true ? "Permission granted." : "No permission granted.";

            // Check if we have a valid WebCamTexture and should update the Texture2D
            if (m_webCamTextureManager.WebCamTexture != null && m_poseDetectionScript != null)
            {
                m_timeSinceLastUpdate += Time.deltaTime;
                if (m_timeSinceLastUpdate >= m_textureUpdateInterval)
                {
                    m_timeSinceLastUpdate = 0f;
                    UpdateTextureForOtherScript();
                }
            }
        }

        private void UpdateTextureForOtherScript()
        {
            WebCamTexture webCamTexture = m_webCamTextureManager.WebCamTexture;

            if (webCamTexture != null)
            {
                Texture2D texture2D = new Texture2D(webCamTexture.width, webCamTexture.height);
                texture2D.SetPixels(webCamTexture.GetPixels());
                texture2D.Apply();

                Debug.Log($"Sending Texture2D to PoseDetection: {texture2D.width}x{texture2D.height}");
                m_poseDetectionScript.SetTexture(texture2D);
            }
            else
            {
                Debug.LogWarning("WebCamTexture is null.");
            }
        }
    }
}