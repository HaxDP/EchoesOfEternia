using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Windows.Speech;

public class VoiceRecognition : MonoBehaviour
{
	[Serializable]
	public class VoiceCommandEvent : UnityEvent<string>
	{
	}

	[Header("Settings")]
	[SerializeField] private ConfidenceLevel confidenceLevel = ConfidenceLevel.Medium;
	[SerializeField] private bool startOnAwake = true;
	[SerializeField] private bool useFuzzyMatching = true;
	[SerializeField, Range(0.5f, 1f)] private float fuzzySimilarityThreshold = 0.6f;

	[Header("Tise Quiet Check")]
	[SerializeField] private bool requireQuietVoiceForTise = true;
	[SerializeField, Range(-80f, -5f)] private float tiseMaxDecibels = -32f;
	[SerializeField] private int loudnessSampleWindow = 1024;
	[SerializeField] private int microphoneFrequency = 16000;
	[SerializeField] private bool logTiseVolumeChecks;
	[SerializeField] private bool calibrateTiseThresholdOnStart = true;
	[SerializeField, Min(0.5f)] private float calibrationDurationSeconds = 2f;
	[SerializeField, Range(1f, 20f)] private float calibrationHeadroomDecibels = 8f;
	[SerializeField, Min(0.25f)] private float microphoneWarmupTimeoutSeconds = 2f;

	[Header("Events")]
	[SerializeField] private VoiceCommandEvent onCommandRecognized;

	public event Action<string> CommandRecognized;

	private readonly Dictionary<string, Action> commands = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);
	private static readonly string[] TiseAliases =
	{
		"tise", "tais", "tyce", "ties", "taise", "taiz", "taisz", "tease", "teez", "tays", "тайс", "тайз"
	};

	private static readonly string[] EchoAliases =
	{
		"echo", "akho", "aho", "eko", "ecco", "ekko", "eho", "ekho", "yeho", "yekho", "jeho", "ехо", "эхо"
	};

	private KeywordRecognizer keywordRecognizer;
	private AudioClip microphoneClip;
	private string microphoneDevice;
	private float currentDecibels = -80f;
	private Coroutine calibrationCoroutine;

	private void Awake()
	{
		BuildCommandMaps();

		if (startOnAwake)
		{
			StartRecognition();
		}
	}

	private void OnDestroy()
	{
		StopRecognition();
		StopMicrophoneCapture();
	}

	private void Update()
	{
		UpdateCurrentDecibels();
	}

	public void StartRecognition()
	{
		if (keywordRecognizer != null && keywordRecognizer.IsRunning)
		{
			return;
		}

		var keywords = GetActiveKeywords();

		if (keywords.Count == 0)
		{
			Debug.LogWarning("VoiceRecognition: No commands configured.");
			return;
		}

		var recognizerConfidence = confidenceLevel;
		keywordRecognizer = new KeywordRecognizer(keywords.ToArray(), recognizerConfidence);
		keywordRecognizer.OnPhraseRecognized += OnPhraseRecognized;
		keywordRecognizer.Start();
		StartMicrophoneCapture();

		if (calibrateTiseThresholdOnStart)
		{
			StartTiseQuietCalibration();
		}

		Debug.Log($"VoiceRecognition started. Commands: {keywords.Count}, recognizerConfidence: {recognizerConfidence}");
	}

	public void StopRecognition()
	{
		if (keywordRecognizer == null)
		{
			return;
		}

		keywordRecognizer.OnPhraseRecognized -= OnPhraseRecognized;

		if (keywordRecognizer.IsRunning)
		{
			keywordRecognizer.Stop();
		}

		keywordRecognizer.Dispose();
		keywordRecognizer = null;

		if (calibrationCoroutine != null)
		{
			StopCoroutine(calibrationCoroutine);
			calibrationCoroutine = null;
		}

		StopMicrophoneCapture();
	}

	public void StartTiseQuietCalibration()
	{
		if (!requireQuietVoiceForTise)
		{
			return;
		}

		if (calibrationCoroutine != null)
		{
			StopCoroutine(calibrationCoroutine);
		}

		calibrationCoroutine = StartCoroutine(CalibrateTiseQuietThresholdRoutine());
	}

	private void RestartRecognition()
	{
		StopRecognition();
		StartRecognition();
	}

	private List<string> GetActiveKeywords()
	{
		return new List<string>(commands.Keys);
	}

	private void OnPhraseRecognized(PhraseRecognizedEventArgs args)
	{
		var recognizedText = NormalizeKeyword(args.text);
		var mappedKeyword = recognizedText;
		if (!commands.TryGetValue(mappedKeyword, out var action) && useFuzzyMatching)
		{
			if (TryFindClosestKeyword(recognizedText, out var closestKeyword, out var similarity))
			{
				mappedKeyword = closestKeyword;
				action = commands[closestKeyword];
				Debug.Log($"VoiceRecognition: Fuzzy match '{recognizedText}' -> '{closestKeyword}' ({similarity:P0})");
			}
		}

		if (action == null)
		{
			Debug.Log($"VoiceRecognition: Recognized phrase not mapped: {args.text}");
			return;
		}

		if (requireQuietVoiceForTise && IsTiseAlias(mappedKeyword) && !IsQuietEnoughForTise())
		{
			Debug.Log($"VoiceRecognition: Ignored tise because voice was too loud. dB: {currentDecibels:F1}, max: {tiseMaxDecibels:F1}, phrase: {args.text}");
			return;
		}

		action.Invoke();
		var canonicalCommand = GetCanonicalCommand(mappedKeyword);
		onCommandRecognized?.Invoke(canonicalCommand);
		CommandRecognized?.Invoke(canonicalCommand);
		Debug.Log($"Recognized command: {canonicalCommand} (raw: {args.text})");
	}

	private string NormalizeKeyword(string input)
	{
		return input == null ? string.Empty : input.Trim().ToLowerInvariant();
	}

	private bool IsTiseAlias(string command)
	{
		for (var i = 0; i < TiseAliases.Length; i++)
		{
			if (string.Equals(command, TiseAliases[i], StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	private bool IsEchoAlias(string command)
	{
		for (var i = 0; i < EchoAliases.Length; i++)
		{
			if (string.Equals(command, EchoAliases[i], StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	private string GetCanonicalCommand(string command)
	{
		if (string.IsNullOrEmpty(command))
		{
			return string.Empty;
		}

		if (IsTiseAlias(command))
		{
			return "tise";
		}

		if (IsEchoAlias(command))
		{
			return "echo";
		}

		return command;
	}

	private bool TryFindClosestKeyword(string recognizedText, out string closestKeyword, out float similarity)
	{
		closestKeyword = null;
		similarity = 0f;

		if (string.IsNullOrEmpty(recognizedText))
		{
			return false;
		}

		foreach (var keyword in commands.Keys)
		{
			var currentSimilarity = CalculateSimilarity(recognizedText, keyword);
			if (currentSimilarity > similarity)
			{
				similarity = currentSimilarity;
				closestKeyword = keyword;
			}
		}

		return closestKeyword != null && similarity >= fuzzySimilarityThreshold;
	}

	private float CalculateSimilarity(string source, string target)
	{
		if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
		{
			return 0f;
		}

		if (source == target)
		{
			return 1f;
		}

		var distance = CalculateLevenshteinDistance(source, target);
		var maxLength = Mathf.Max(source.Length, target.Length);
		return maxLength == 0 ? 1f : 1f - (float)distance / maxLength;
	}

	private int CalculateLevenshteinDistance(string source, string target)
	{
		var sourceLength = source.Length;
		var targetLength = target.Length;
		var matrix = new int[sourceLength + 1, targetLength + 1];

		for (var i = 0; i <= sourceLength; i++)
		{
			matrix[i, 0] = i;
		}

		for (var j = 0; j <= targetLength; j++)
		{
			matrix[0, j] = j;
		}

		for (var i = 1; i <= sourceLength; i++)
		{
			for (var j = 1; j <= targetLength; j++)
			{
				var substitutionCost = source[i - 1] == target[j - 1] ? 0 : 1;
				matrix[i, j] = Mathf.Min(
					Mathf.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
					matrix[i - 1, j - 1] + substitutionCost);
			}
		}

		return matrix[sourceLength, targetLength];
	}

	private bool IsQuietEnoughForTise()
	{
		if (!IsMicrophoneAvailable())
		{
			return false;
		}

		var isQuiet = currentDecibels <= tiseMaxDecibels;

		if (logTiseVolumeChecks)
		{
			Debug.Log($"VoiceRecognition: Tise quiet check dB={currentDecibels:F1}, threshold={tiseMaxDecibels:F1}, pass={isQuiet}");
		}

		return isQuiet;
	}

	private IEnumerator CalibrateTiseQuietThresholdRoutine()
	{
		StartMicrophoneCapture();

		if (microphoneClip == null)
		{
			Debug.LogWarning("VoiceRecognition: Calibration failed, microphone capture did not start.");
			calibrationCoroutine = null;
			yield break;
		}

		var warmupTimer = 0f;

		while (warmupTimer < microphoneWarmupTimeoutSeconds)
		{
			if (TryGetCurrentDecibels(out _))
			{
				break;
			}

			warmupTimer += Time.unscaledDeltaTime;
			yield return null;
		}

		if (!TryGetCurrentDecibels(out _))
		{
			Debug.LogWarning("VoiceRecognition: Calibration failed, microphone produced no readable samples.");
			calibrationCoroutine = null;
			yield break;
		}

		var elapsed = 0f;
		var totalDecibels = 0f;
		var sampleCount = 0;

		Debug.Log("VoiceRecognition: Calibrating tise quiet threshold. Please stay silent.");

		while (elapsed < calibrationDurationSeconds)
		{
			if (TryGetCurrentDecibels(out var decibels))
			{
				totalDecibels += decibels;
				sampleCount++;
			}

			elapsed += Time.unscaledDeltaTime;
			yield return null;
		}

		if (sampleCount > 0)
		{
			var ambientDecibels = totalDecibels / sampleCount;
			tiseMaxDecibels = Mathf.Clamp(ambientDecibels + calibrationHeadroomDecibels, -80f, -5f);
			Debug.Log($"VoiceRecognition: Calibration complete. Ambient dB: {ambientDecibels:F1}, new tise threshold: {tiseMaxDecibels:F1}");
		}
		else
		{
			Debug.LogWarning("VoiceRecognition: Calibration failed, no microphone samples were collected.");
		}

		calibrationCoroutine = null;
	}

	private void StartMicrophoneCapture()
	{
		if (!requireQuietVoiceForTise)
		{
			return;
		}

		if (microphoneClip != null)
		{
			return;
		}

		if (!IsMicrophoneAvailable())
		{
			Debug.LogWarning("VoiceRecognition: No microphone device found. Quiet check for tise will be blocked.");
			return;
		}

		microphoneDevice = Microphone.devices[0];
		microphoneClip = Microphone.Start(microphoneDevice, true, 1, microphoneFrequency);
	}

	private void StopMicrophoneCapture()
	{
		if (string.IsNullOrEmpty(microphoneDevice))
		{
			microphoneClip = null;
			return;
		}

		if (Microphone.IsRecording(microphoneDevice))
		{
			Microphone.End(microphoneDevice);
		}

		microphoneClip = null;
		microphoneDevice = null;
		currentDecibels = -80f;
	}

	private void UpdateCurrentDecibels()
	{
		TryGetCurrentDecibels(out _);
	}

	private bool TryGetCurrentDecibels(out float decibels)
	{
		decibels = -80f;

		if (!requireQuietVoiceForTise)
		{
			return false;
		}

		if (microphoneClip == null || string.IsNullOrEmpty(microphoneDevice) || !Microphone.IsRecording(microphoneDevice))
		{
			return false;
		}

		var micPosition = Microphone.GetPosition(microphoneDevice);

		if (micPosition < loudnessSampleWindow)
		{
			return false;
		}

		var readStart = micPosition - loudnessSampleWindow;
		var samples = new float[loudnessSampleWindow];
		microphoneClip.GetData(samples, readStart);

		decibels = CalculateDecibels(samples);
		currentDecibels = decibels;
		return true;
	}

	private float CalculateDecibels(float[] samples)
	{
		if (samples == null || samples.Length == 0)
		{
			return -80f;
		}

		var sumSquares = 0f;

		for (var i = 0; i < samples.Length; i++)
		{
			var sample = samples[i];
			sumSquares += sample * sample;
		}

		var rms = Mathf.Sqrt(sumSquares / samples.Length);
		return 20f * Mathf.Log10(Mathf.Max(rms, 0.000001f));
	}

	private bool IsMicrophoneAvailable()
	{
		return Microphone.devices != null && Microphone.devices.Length > 0;
	}

	private void BuildCommandMaps()
	{
		commands.Clear();

		AddCommand("start", "start");
		AddCommand("stop", "stop");
		AddCommand("default", "default");
		AddCommand("ignis", "ignis");
		AddCommand("mentiri", "mentiri");

		for (var i = 0; i < TiseAliases.Length; i++)
		{
			AddCommand(TiseAliases[i], "tise");
		}

		for (var i = 0; i < EchoAliases.Length; i++)
		{
			AddCommand(EchoAliases[i], "echo");
		}
	}

	private void AddCommand(string keyword, string command)
	{
		commands[keyword] = () => HandleCommand(command);
	}

	private void HandleCommand(string command)
	{
		Debug.Log($"Voice command executed: {command}");
	}
}