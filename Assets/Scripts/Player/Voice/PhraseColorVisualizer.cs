using System;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PhraseColorVisualizer : MonoBehaviour
{
	private static readonly string[] TiseAliases =
	{
		"tise", "tais", "tyce", "ties", "taise", "taiz", "taisz", "tease", "teez", "tays", "тайс", "тайз"
	};

	private static readonly string[] EchoAliases =
	{
		"echo", "eko", "ecco", "ekko", "eho", "ekho", "yeho", "yekho", "jeho", "akho", "aho", "ехо", "эхо"
	};

	[Header("Target")]
	[SerializeField] private SpriteRenderer targetRenderer;
	[SerializeField] private VoiceRecognition voiceRecognition;

	[Header("Phrase Colors")]
	[SerializeField] private Color ignisColor = new Color(1f, 0.5f, 0f, 1f);
	[SerializeField] private Color mentiriColor = new Color(1f, 0f, 0f, 1f);
	[SerializeField] private Color echoColor = new Color(0.5f, 0.9f, 1f, 1f);
	[SerializeField] private Color tiseColor = new Color(0.85f, 0.85f, 0.85f, 1f);
	[SerializeField] private Color defaultColor = Color.white;

	private void Awake()
	{
		if (targetRenderer == null)
		{
			targetRenderer = GetComponent<SpriteRenderer>();
		}

		if (voiceRecognition == null)
		{
			voiceRecognition = FindFirstObjectByType<VoiceRecognition>();
		}

		if (targetRenderer == null)
		{
			Debug.LogWarning("PhraseColorVisualizer: Target Renderer is not assigned.");
		}
	}

	private void Start()
	{
		SetColor(defaultColor);
	}

	private void OnEnable()
	{
		if (voiceRecognition != null)
		{
			voiceRecognition.CommandRecognized += OnPhraseRecognized;
		}
	}

	private void OnDisable()
	{
		if (voiceRecognition != null)
		{
			voiceRecognition.CommandRecognized -= OnPhraseRecognized;
		}
	}

	public void OnPhraseRecognized(string phrase)
	{
		if (string.IsNullOrWhiteSpace(phrase))
		{
			return;
		}

		var normalizedPhrase = Normalize(phrase);
		var command = ToCanonicalCommand(normalizedPhrase);
		Debug.Log($"PhraseColorVisualizer: Received phrase='{normalizedPhrase}', command='{command}'");

		switch (command)
		{
			case "ignis":
				SetColor(ignisColor);
				break;
			case "mentiri":
				SetColor(mentiriColor);
				break;
			case "echo":
				SetColor(echoColor);
				break;
			case "tise":
				SetColor(tiseColor);
				break;
			case "default":
				SetColor(defaultColor);
				break;
		}
	}

	public void ResetToDefaultColor()
	{
		SetColor(defaultColor);
	}

	private void SetColor(Color color)
	{
		if (targetRenderer != null)
		{
			targetRenderer.color = color;
		}
	}

	private string Normalize(string value)
	{
		return value.Trim().ToLowerInvariant();
	}

	private string ToCanonicalCommand(string phrase)
	{
		if (IsAlias(phrase, TiseAliases))
		{
			return "tise";
		}

		if (IsAlias(phrase, EchoAliases))
		{
			return "echo";
		}

		switch (phrase)
		{
			case "ignis":
				return "ignis";
			case "mentiri":
				return "mentiri";
			case "default":
				return "default";
			default:
				return phrase;
		}
	}

	private bool IsAlias(string value, string[] aliases)
	{
		for (var i = 0; i < aliases.Length; i++)
		{
			if (string.Equals(value, aliases[i], StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}
}