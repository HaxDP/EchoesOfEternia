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

	[Header("Fallback Recognition")]
	[SerializeField] private bool useDictationFallback = true;
	[SerializeField] private ConfidenceLevel dictationMinConfidence = ConfidenceLevel.Low;
	[SerializeField, Min(0.1f)] private float duplicateCommandCooldownSeconds = 0.35f;

	[Header("Silenta Quiet Check")]
	[SerializeField] private bool requireQuietVoiceForSilenta = true;
	[SerializeField] private bool strictSilentaQuietCheck;
	[SerializeField, Range(-80f, -5f)] private float silentaMaxDecibels = -32f;
	[SerializeField] private int loudnessSampleWindow = 1024;
	[SerializeField] private int microphoneFrequency = 16000;
	[SerializeField] private bool logSilentaVolumeChecks;
	[SerializeField] private bool calibrateSilentaThresholdOnStart = true;
	[SerializeField, Min(0.5f)] private float calibrationDurationSeconds = 2f;
	[SerializeField, Range(1f, 20f)] private float calibrationHeadroomDecibels = 8f;
	[SerializeField, Min(0.25f)] private float microphoneWarmupTimeoutSeconds = 2f;

	[Header("Microphone Selection")]
	[SerializeField] private bool useSystemDefaultMicrophone = true;
	[SerializeField] private string preferredMicrophoneDevice = string.Empty;
	[SerializeField, Min(0.25f)] private float microphoneDeviceRefreshIntervalSeconds = 1f;
	[SerializeField] private bool restartSpeechRecognizersOnDeviceChange = true;

	[Header("Events")]
	[SerializeField] private VoiceCommandEvent onCommandRecognized;

	public event Action<string> CommandRecognized;

	public float CurrentDecibels => currentDecibels;
	public bool IsMicrophoneMonitoringEnabled => requireQuietVoiceForSilenta;
	public bool IsMicrophoneCaptureActive => microphoneClip != null && Microphone.IsRecording(microphoneDevice);
	public string ActiveMicrophoneDeviceName => string.IsNullOrEmpty(microphoneDevice) ? "<system default>" : microphoneDevice;
	public bool IsKeywordRecognizerRunning => keywordRecognizer != null && keywordRecognizer.IsRunning;
	public SpeechSystemStatus DictationRecognizerStatus => dictationRecognizer == null ? SpeechSystemStatus.Stopped : dictationRecognizer.Status;

	private readonly Dictionary<string, Action> commands = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);
	private static readonly string[] SilentaAliases =
	{
		"silenta"
	};

	private static readonly string[] ReveraAliases =
	{
		"revera"
	};

	private KeywordRecognizer keywordRecognizer;
	private DictationRecognizer dictationRecognizer;
	private AudioClip microphoneClip;
	private string microphoneDevice;
	private float currentDecibels = -80f;
	private Coroutine calibrationCoroutine;
	private string lastCanonicalCommand = string.Empty;
	private float lastCommandTime = -10f;
	private string microphoneDevicesSignature = string.Empty;
	private float nextMicrophoneDeviceRefreshTime;

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
		StopDictationFallback();
		StopMicrophoneCapture();
	}

	private void Update()
	{
		UpdateCurrentDecibels();
		RefreshMicrophoneCaptureIfDeviceListChanged();
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
		StartDictationFallback();
		StartMicrophoneCapture();
		Debug.Log("VoiceRecognition: KeywordRecognizer and DictationRecognizer use the OS default microphone input device.");

		if (calibrateSilentaThresholdOnStart)
		{
			StartSilentaQuietCalibration();
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

		StopDictationFallback();

		if (calibrationCoroutine != null)
		{
			StopCoroutine(calibrationCoroutine);
			calibrationCoroutine = null;
		}

		StopMicrophoneCapture();
	}

	public void StartSilentaQuietCalibration()
	{
		if (!requireQuietVoiceForSilenta)
		{
			return;
		}

		if (calibrationCoroutine != null)
		{
			StopCoroutine(calibrationCoroutine);
		}

		calibrationCoroutine = StartCoroutine(CalibrateSilentaQuietThresholdRoutine());
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
		TryResolveCommand(recognizedText, out mappedKeyword, out var action);

		if (action == null)
		{
			Debug.Log($"VoiceRecognition: Recognized phrase not mapped: {args.text}");
			return;
		}

		ExecuteResolvedCommand(mappedKeyword, action, args.text, "keyword");
	}

	private void OnDictationResult(string text, ConfidenceLevel confidence)
	{
		if (confidence < dictationMinConfidence)
		{
			return;
		}

		var recognizedText = NormalizeKeyword(text);
		if (string.IsNullOrEmpty(recognizedText))
		{
			return;
		}

		TryResolveCommand(recognizedText, out var mappedKeyword, out var action);
		if (action == null)
		{
			return;
		}

		ExecuteResolvedCommand(mappedKeyword, action, text, "dictation");
	}

	private void ExecuteResolvedCommand(string mappedKeyword, Action action, string rawText, string source)
	{
		if (requireQuietVoiceForSilenta && strictSilentaQuietCheck && IsSilentaAlias(mappedKeyword) && !IsQuietEnoughForSilenta())
		{
			Debug.Log($"VoiceRecognition: Ignored silenta because voice was too loud. dB: {currentDecibels:F1}, max: {silentaMaxDecibels:F1}, phrase: {rawText}");
			return;
		}

		var canonicalCommand = GetCanonicalCommand(mappedKeyword);
		var now = Time.unscaledTime;
		if (string.Equals(canonicalCommand, lastCanonicalCommand, StringComparison.OrdinalIgnoreCase) && now - lastCommandTime < duplicateCommandCooldownSeconds)
		{
			return;
		}

		action.Invoke();
		onCommandRecognized?.Invoke(canonicalCommand);
		CommandRecognized?.Invoke(canonicalCommand);

		lastCanonicalCommand = canonicalCommand;
		lastCommandTime = now;
		Debug.Log($"Recognized command: {canonicalCommand} (source: {source}, raw: {rawText})");
	}

	private void StartDictationFallback()
	{
		if (!useDictationFallback)
		{
			return;
		}

		if (dictationRecognizer == null)
		{
			dictationRecognizer = new DictationRecognizer();
			dictationRecognizer.DictationResult += OnDictationResult;
			dictationRecognizer.DictationError += OnDictationError;
		}

		if (dictationRecognizer.Status == SpeechSystemStatus.Stopped)
		{
			try
			{
				dictationRecognizer.Start();
			}
			catch (Exception exception)
			{
				Debug.LogWarning($"VoiceRecognition: Dictation fallback failed to start: {exception.Message}");
			}
		}
	}

	private void StopDictationFallback()
	{
		if (dictationRecognizer == null)
		{
			return;
		}

		dictationRecognizer.DictationResult -= OnDictationResult;
		dictationRecognizer.DictationError -= OnDictationError;

		if (dictationRecognizer.Status == SpeechSystemStatus.Running)
		{
			dictationRecognizer.Stop();
		}

		dictationRecognizer.Dispose();
		dictationRecognizer = null;
	}

	private void OnDictationError(string error, int hresult)
	{
		Debug.LogWarning($"VoiceRecognition: Dictation fallback error: {error} ({hresult})");
	}

	private void TryResolveCommand(string recognizedText, out string mappedKeyword, out Action action)
	{
		mappedKeyword = recognizedText;
		action = null;

		if (commands.TryGetValue(recognizedText, out action))
		{
			return;
		}

		if (TryResolveFromFuzzy(recognizedText, out mappedKeyword, out action))
		{
			return;
		}

		if (TryResolveFromTokens(recognizedText, out mappedKeyword, out action))
		{
			return;
		}

		if (LooksLikeRevera(recognizedText) && commands.TryGetValue("revera", out action))
		{
			mappedKeyword = "revera";
			Debug.Log($"VoiceRecognition: Revera fallback for phrase '{recognizedText}'");
		}
	}

	private bool TryResolveFromFuzzy(string recognizedText, out string mappedKeyword, out Action action)
	{
		mappedKeyword = recognizedText;
		action = null;

		if (!useFuzzyMatching)
		{
			return false;
		}

		if (TryFindClosestKeyword(recognizedText, out var closestKeyword, out var similarity))
		{
			mappedKeyword = closestKeyword;
			action = commands[closestKeyword];
			Debug.Log($"VoiceRecognition: Fuzzy match '{recognizedText}' -> '{closestKeyword}' ({similarity:P0})");
			return true;
		}

		return false;
	}

	private bool TryResolveFromTokens(string recognizedText, out string mappedKeyword, out Action action)
	{
		mappedKeyword = recognizedText;
		action = null;

		var tokens = recognizedText.Split(new[] { ' ', '\t', ',', '.', ';', ':', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
		for (var i = 0; i < tokens.Length; i++)
		{
			var token = tokens[i];
			if (commands.TryGetValue(token, out action))
			{
				mappedKeyword = token;
				Debug.Log($"VoiceRecognition: Token match '{recognizedText}' -> '{token}'");
				return true;
			}

			if (TryResolveFromFuzzy(token, out mappedKeyword, out action))
			{
				Debug.Log($"VoiceRecognition: Token fuzzy match '{recognizedText}' -> '{mappedKeyword}'");
				return true;
			}
		}

		return false;
	}

	private bool LooksLikeRevera(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}

		return text.Contains("revera");
	}

	private string NormalizeKeyword(string input)
	{
		return input == null ? string.Empty : input.Trim().ToLowerInvariant();
	}

	private bool IsSilentaAlias(string command)
	{
		for (var i = 0; i < SilentaAliases.Length; i++)
		{
			if (string.Equals(command, SilentaAliases[i], StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	private bool IsReveraAlias(string command)
	{
		for (var i = 0; i < ReveraAliases.Length; i++)
		{
			if (string.Equals(command, ReveraAliases[i], StringComparison.OrdinalIgnoreCase))
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

		if (IsSilentaAlias(command))
		{
			return "silenta";
		}

		if (IsReveraAlias(command))
		{
			return "revera";
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

	private bool IsQuietEnoughForSilenta()
	{
		if (!IsMicrophoneAvailable())
		{
			return false;
		}

		var isQuiet = currentDecibels <= silentaMaxDecibels;

		if (logSilentaVolumeChecks)
		{
			Debug.Log($"VoiceRecognition: Silenta quiet check dB={currentDecibels:F1}, threshold={silentaMaxDecibels:F1}, pass={isQuiet}");
		}

		return isQuiet;
	}

	private IEnumerator CalibrateSilentaQuietThresholdRoutine()
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

		Debug.Log("VoiceRecognition: Calibrating silenta quiet threshold. Please stay silent.");

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
			silentaMaxDecibels = Mathf.Clamp(ambientDecibels + calibrationHeadroomDecibels, -80f, -5f);
			Debug.Log($"VoiceRecognition: Calibration complete. Ambient dB: {ambientDecibels:F1}, new silenta threshold: {silentaMaxDecibels:F1}");
		}
		else
		{
			Debug.LogWarning("VoiceRecognition: Calibration failed, no microphone samples were collected.");
		}

		calibrationCoroutine = null;
	}

	private void StartMicrophoneCapture()
	{
		if (!requireQuietVoiceForSilenta)
		{
			return;
		}

		if (microphoneClip != null)
		{
			return;
		}

		if (!IsMicrophoneAvailable())
		{
			Debug.LogWarning("VoiceRecognition: No microphone device found. Quiet check for silenta will be blocked.");
			return;
		}

		microphoneDevice = ResolveMicrophoneDeviceForCapture();
		microphoneClip = Microphone.Start(microphoneDevice, true, 1, microphoneFrequency);
		microphoneDevicesSignature = BuildMicrophoneDeviceSignature();

		if (microphoneClip == null)
		{
			var selectedDevice = string.IsNullOrEmpty(microphoneDevice) ? "<system default>" : microphoneDevice;
			Debug.LogWarning($"VoiceRecognition: Failed to start microphone capture with device '{selectedDevice}'.");
			return;
		}

		var activeDevice = string.IsNullOrEmpty(microphoneDevice) ? "<system default>" : microphoneDevice;
		Debug.Log($"VoiceRecognition: Microphone capture started with device '{activeDevice}'.");
	}

	private void StopMicrophoneCapture()
	{
		if (microphoneClip == null)
		{
			return;
		}

		if (Microphone.IsRecording(microphoneDevice))
		{
			Microphone.End(microphoneDevice);
		}

		microphoneClip = null;
		microphoneDevice = null;
		currentDecibels = -80f;
		microphoneDevicesSignature = string.Empty;
		nextMicrophoneDeviceRefreshTime = 0f;
	}

	private void UpdateCurrentDecibels()
	{
		TryGetCurrentDecibels(out _);
	}

	private bool TryGetCurrentDecibels(out float decibels)
	{
		decibels = -80f;

		if (!requireQuietVoiceForSilenta)
		{
			return false;
		}

		if (microphoneClip == null || !Microphone.IsRecording(microphoneDevice))
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

	public void ApplyMicrophoneSelectionNow()
	{
		if (!requireQuietVoiceForSilenta)
		{
			return;
		}

		if (restartSpeechRecognizersOnDeviceChange && keywordRecognizer != null && keywordRecognizer.IsRunning)
		{
			Debug.Log("VoiceRecognition: Applying microphone selection by restarting speech recognizers.");
			RestartRecognition();
			return;
		}

		StopMicrophoneCapture();
		StartMicrophoneCapture();
	}

	public string[] GetAvailableMicrophoneDevices()
	{
		if (Microphone.devices == null || Microphone.devices.Length == 0)
		{
			return Array.Empty<string>();
		}

		var devicesCopy = new string[Microphone.devices.Length];
		Array.Copy(Microphone.devices, devicesCopy, Microphone.devices.Length);
		return devicesCopy;
	}

	private string ResolveMicrophoneDeviceForCapture()
	{
		if (useSystemDefaultMicrophone)
		{
			return null;
		}

		if (!string.IsNullOrWhiteSpace(preferredMicrophoneDevice))
		{
			for (var i = 0; i < Microphone.devices.Length; i++)
			{
				var device = Microphone.devices[i];
				if (string.Equals(device, preferredMicrophoneDevice, StringComparison.OrdinalIgnoreCase))
				{
					return device;
				}
			}

			for (var i = 0; i < Microphone.devices.Length; i++)
			{
				var device = Microphone.devices[i];
				if (device.IndexOf(preferredMicrophoneDevice, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return device;
				}
			}

			Debug.LogWarning($"VoiceRecognition: Preferred microphone '{preferredMicrophoneDevice}' not found. Falling back to first device.");
		}

		return Microphone.devices[0];
	}

	private string BuildMicrophoneDeviceSignature()
	{
		if (!IsMicrophoneAvailable())
		{
			return string.Empty;
		}

		return string.Join("|", Microphone.devices);
	}

	private void RefreshMicrophoneCaptureIfDeviceListChanged()
	{
		if (!requireQuietVoiceForSilenta || microphoneClip == null)
		{
			return;
		}

		if (Time.unscaledTime < nextMicrophoneDeviceRefreshTime)
		{
			return;
		}

		nextMicrophoneDeviceRefreshTime = Time.unscaledTime + microphoneDeviceRefreshIntervalSeconds;
		var currentSignature = BuildMicrophoneDeviceSignature();
		if (string.Equals(currentSignature, microphoneDevicesSignature, StringComparison.Ordinal))
		{
			return;
		}

		microphoneDevicesSignature = currentSignature;
		Debug.Log("VoiceRecognition: Microphone device list changed. Restarting capture.");

		if (restartSpeechRecognizersOnDeviceChange && keywordRecognizer != null && keywordRecognizer.IsRunning)
		{
			Debug.Log("VoiceRecognition: Restarting speech recognizers to apply current OS default microphone.");
			RestartRecognition();
			return;
		}

		StopMicrophoneCapture();
		StartMicrophoneCapture();
	}

	private void BuildCommandMaps()
	{
		commands.Clear();

		AddCommand("start", "start");
		AddCommand("stop", "stop");
		AddCommand("default", "default");
		AddCommand("ignis", "ignis");
		AddCommand("mentiri", "mentiri");
		//AddCommand("silenta", "silenta");
		//AddCommand("revera", "revera");

		for (var i = 0; i < SilentaAliases.Length; i++)
		{
			AddCommand(SilentaAliases[i], "silenta");
		}

		for (var i = 0; i < ReveraAliases.Length; i++)
		{
			AddCommand(ReveraAliases[i], "revera");
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