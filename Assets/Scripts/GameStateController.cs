using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GameStateController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Text poseText;
    [SerializeField] private Text countdownText;

    [Header("VR References")]
    [SerializeField] private Transform headsetTransform;
    [SerializeField] private Transform leftHandTransform;
    [SerializeField] private Transform rightHandTransform;

    [Header("Game Settings")]
    [SerializeField] private string[] suryaNamaskarPoses = {
        "Pranamasana", "Hasta Uttanasana", "Padahastasana", "Ashwa Sanchalanasana",
        "Dandasana", "Ashtanga Namaskara", "Bhujangasana", "Adho Mukha Svanasana",
        "Ashwa Sanchalanasana", "Padahastasana", "Hasta Uttanasana", "Pranamasana"
    };
    [SerializeField] private float poseDuration = 5f;
    [SerializeField] private float relaxDuration = 2f;

    private int currentPoseIndex = 0;
    private float timer = 0f;
    private bool isRelaxState = false;

    public string CurrentGameState { get; private set; } = "Relax"; // Public property for ImageSender
    public QuestValues CurrentQuestValues { get; private set; } = new QuestValues();

    private void Start()
    {
        StartCoroutine(CycleThroughPoses());
    }

    private IEnumerator CycleThroughPoses()
    {
        while (true)
        {
            if (isRelaxState)
            {
                CurrentGameState = "Relax";
                poseText.text = "Relax";
                timer = relaxDuration;
            }
            else
            {
                CurrentGameState = suryaNamaskarPoses[currentPoseIndex];
                poseText.text = $"Pose: {CurrentGameState}";
                timer = poseDuration;
            }

            while (timer > 0)
            {
                countdownText.text = $"Time: {timer:F1}s";
                timer -= Time.deltaTime;

                // Update VR headset and hand data
                UpdateQuestValues();

                yield return null;
            }

            if (!isRelaxState)
            {
                currentPoseIndex = (currentPoseIndex + 1) % suryaNamaskarPoses.Length;
            }

            isRelaxState = !isRelaxState;
        }
    }

    private void UpdateQuestValues()
    {
        CurrentQuestValues.headsetPosition = headsetTransform.position;
        CurrentQuestValues.headsetRotation = headsetTransform.rotation;
        CurrentQuestValues.leftHandPosition = leftHandTransform.position;
        CurrentQuestValues.leftHandRotation = leftHandTransform.rotation;
        CurrentQuestValues.rightHandPosition = rightHandTransform.position;
        CurrentQuestValues.rightHandRotation = rightHandTransform.rotation;
    }
}

[System.Serializable]
public class QuestValues
{
    public Vector3 headsetPosition;
    public Quaternion headsetRotation;
    public Vector3 leftHandPosition;
    public Quaternion leftHandRotation;
    public Vector3 rightHandPosition;
    public Quaternion rightHandRotation;
}
