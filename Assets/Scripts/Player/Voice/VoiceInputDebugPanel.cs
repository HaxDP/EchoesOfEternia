using System;
using UnityEngine;
using UnityEngine.Windows.Speech;

public class VoiceInputDebugPanel : MonoBehaviour
{
	[SerializeField] private VoiceRecognition voiceRecognition;
	[SerializeField] private KeyCode togglePanelKey = KeyCode.F8;
	[SerializeField] private bool showPanel = true;
	[SerializeField] private bool showDeviceList = true;
	[SerializeField] private Rect panelRect = new Rect(16f, 16f, 380f, 240f);
	[SerializeField, Range(0.01f, 0.5f)] private float meterSmoothing = 0.15f;

	private string lastRecognizedCommand = "(none)";
	private float lastRecognizedTime = -999f;
	private float smoothedMeterValue;
	private Texture2D solidTexture;

	private void Awake()
	{
		if (voiceRecognition == null)
		{
			voiceRecognition = FindFirstObjectByType<VoiceRecognition>();
		}

		CreateSolidTexture();
	}

	private void OnEnable()
	{
		if (voiceRecognition != null)
		{
			voiceRecognition.CommandRecognized += OnCommandRecognized;
		}
	}

	private void OnDisable()
	{
		if (voiceRecognition != null)
		{
			voiceRecognition.CommandRecognized -= OnCommandRecognized;
		}
	}

	private void OnDestroy()
	{
		if (solidTexture != null)
		{
			Destroy(solidTexture);
			solidTexture = null;
		}
	}

	private void Update()
	{
		if (Input.GetKeyDown(togglePanelKey))
		{
			showPanel = !showPanel;
		}

		if (voiceRecognition == null)
		{
			smoothedMeterValue = Mathf.Lerp(smoothedMeterValue, 0f, meterSmoothing);
			return;
		}

		var normalized = DecibelsToMeterValue(voiceRecognition.CurrentDecibels);
		smoothedMeterValue = Mathf.Lerp(smoothedMeterValue, normalized, meterSmoothing);
	}

	private void OnGUI()
	{
		if (!showPanel)
		{
			return;
		}

		GUI.Box(panelRect, "Voice Input Debug");

		if (voiceRecognition == null)
		{
			GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 32f, panelRect.width - 24f, 40f), "VoiceRecognition not found in scene.");
			GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 52f, panelRect.width - 24f, 40f), "Assign it in inspector or add VoiceRecognition object.");
			return;
		}

		var y = panelRect.y + 28f;
		var lineHeight = 18f;
		var left = panelRect.x + 12f;
		var width = panelRect.width - 24f;

		GUI.Label(new Rect(left, y, width, lineHeight), $"Keyword recognizer: {(voiceRecognition.IsKeywordRecognizerRunning ? "Running" : "Stopped")}");
		y += lineHeight;

		var dictationStatus = ToDictationStatusLabel(voiceRecognition.DictationRecognizerStatus);
		GUI.Label(new Rect(left, y, width, lineHeight), $"Dictation fallback: {dictationStatus}");
		y += lineHeight;

		GUI.Label(new Rect(left, y, width, lineHeight), $"Mic monitoring enabled: {(voiceRecognition.IsMicrophoneMonitoringEnabled ? "Yes" : "No")}");
		y += lineHeight;

		GUI.Label(new Rect(left, y, width, lineHeight), $"Mic capture active: {(voiceRecognition.IsMicrophoneCaptureActive ? "Yes" : "No")}");
		y += lineHeight;

		GUI.Label(new Rect(left, y, width, lineHeight), $"Active mic: {voiceRecognition.ActiveMicrophoneDeviceName}");
		y += lineHeight + 2f;

		var meterRect = new Rect(left, y, width, 16f);
		DrawMeter(meterRect, smoothedMeterValue);
		y += 20f;

		GUI.Label(new Rect(left, y, width, lineHeight), $"Input level: {voiceRecognition.CurrentDecibels:F1} dB");
		y += lineHeight;

		var sinceLast = lastRecognizedTime < 0f ? "-" : $"{Time.unscaledTime - lastRecognizedTime:F1}s ago";
		GUI.Label(new Rect(left, y, width, lineHeight), $"Last command: {lastRecognizedCommand} ({sinceLast})");
		y += lineHeight;

		if (showDeviceList)
		{
			var devices = voiceRecognition.GetAvailableMicrophoneDevices();
			var devicesText = devices.Length == 0 ? "(none)" : string.Join(", ", devices);
			GUI.Label(new Rect(left, y, width, lineHeight * 2f), $"Devices: {devicesText}");
		}
	}

	private void DrawMeter(Rect rect, float normalizedValue)
	{
		if (solidTexture == null)
		{
			CreateSolidTexture();
		}

		DrawFilledRect(rect, new Color(0.12f, 0.12f, 0.12f, 0.95f));

		var filledWidth = Mathf.Clamp01(normalizedValue) * rect.width;
		var fillRect = new Rect(rect.x, rect.y, filledWidth, rect.height);
		var fillColor = Color.Lerp(new Color(0.2f, 0.85f, 0.3f), new Color(0.95f, 0.2f, 0.2f), Mathf.Clamp01(normalizedValue));
		DrawFilledRect(fillRect, fillColor);
	}

	private void DrawFilledRect(Rect rect, Color color)
	{
		if (rect.width <= 0f || rect.height <= 0f)
		{
			return;
		}

		var previousColor = GUI.color;
		GUI.color = color;
		GUI.DrawTexture(rect, solidTexture);
		GUI.color = previousColor;
	}

	private float DecibelsToMeterValue(float decibels)
	{
		const float minDb = -70f;
		const float maxDb = -10f;
		return Mathf.InverseLerp(minDb, maxDb, decibels);
	}

	private void OnCommandRecognized(string command)
	{
		lastRecognizedCommand = string.IsNullOrWhiteSpace(command) ? "(empty)" : command;
		lastRecognizedTime = Time.unscaledTime;
	}

	private string ToDictationStatusLabel(SpeechSystemStatus status)
	{
		switch (status)
		{
			case SpeechSystemStatus.Running:
				return "Running";
			case SpeechSystemStatus.Failed:
				return "Failed";
			default:
				return "Stopped";
		}
	}

	private void CreateSolidTexture()
	{
		if (solidTexture != null)
		{
			return;
		}

		solidTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
		solidTexture.SetPixel(0, 0, Color.white);
		solidTexture.Apply();
	}
}
