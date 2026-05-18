using UnityEngine;
using UnityEngine.UI;

public class VoiceVolumeDisplay : MonoBehaviour
{
    [SerializeField] private VoiceRecognition voiceRecognition;
    [SerializeField] private Image volumeImage;

    [Header("Volume Sprites")]
    [SerializeField] private Sprite[] volumeLayers;

    [Header("Volume Settings")]
    [SerializeField] private float minDecibels = -70f;
    [SerializeField] private float maxDecibels = -10f;
    [SerializeField] private float smoothing = 8f;

    private float smoothedValue;

    private void Awake()
    {
        if (volumeImage == null)
        {
            volumeImage = GetComponent<Image>();
        }
    }

    private void Update()
    {
        if (voiceRecognition == null || volumeImage == null || volumeLayers.Length == 0)
        {
            return;
        }

        float rawValue = Mathf.InverseLerp(
            minDecibels,
            maxDecibels,
            voiceRecognition.CurrentDecibels
        );

        smoothedValue = Mathf.Lerp(
            smoothedValue,
            rawValue,
            Time.deltaTime * smoothing
        );

        int index = Mathf.RoundToInt(smoothedValue * (volumeLayers.Length - 1));
        index = Mathf.Clamp(index, 0, volumeLayers.Length - 1);

        volumeImage.sprite = volumeLayers[index];
    }
}