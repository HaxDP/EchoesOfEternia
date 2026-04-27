using System;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PhraseColorVisualizer : MonoBehaviour
{
	private static readonly string[] SilentaAliases =
	{
		"silenta"
	};

	private static readonly string[] ReveraAliases =
	{
		"revera"
	};

	[Header("Target")]
	[SerializeField] private SpriteRenderer targetRenderer;
	[SerializeField] private VoiceRecognition voiceRecognition;

	[Header("Phrase Colors")]
	[SerializeField] private Color ignisColor = new Color(1f, 0.5f, 0f, 1f);
	[SerializeField] private Color mentiriColor = new Color(1f, 0f, 0f, 1f);
	[SerializeField] private Color reveraColor = new Color(0.5f, 0.9f, 1f, 1f);
	[SerializeField] private Color silentaColor = new Color(0.85f, 0.85f, 0.85f, 1f);
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
			case "revera":
				SetColor(reveraColor);
				break;
			case "silenta":
				SetColor(silentaColor);
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
		if (IsAlias(phrase, SilentaAliases))
		{
			return "silenta";
		}

		if (IsAlias(phrase, ReveraAliases))
		{
			return "revera";
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