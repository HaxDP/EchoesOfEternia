#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VoiceRecognition))]
public class VoiceRecognitionEditor : Editor
{
	private SerializedProperty useSystemDefaultMicrophoneProperty;
	private SerializedProperty preferredMicrophoneDeviceProperty;

	private void OnEnable()
	{
		useSystemDefaultMicrophoneProperty = serializedObject.FindProperty("useSystemDefaultMicrophone");
		preferredMicrophoneDeviceProperty = serializedObject.FindProperty("preferredMicrophoneDevice");
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		DrawDefaultInspector();
		EditorGUILayout.Space(8f);
		DrawMicrophonePicker();

		serializedObject.ApplyModifiedProperties();
	}

	private void DrawMicrophonePicker()
	{
		EditorGUILayout.LabelField("Microphone Picker", EditorStyles.boldLabel);

		var devices = Microphone.devices;
		if (devices == null)
		{
			devices = new string[0];
		}

		var options = new string[devices.Length + 1];
		options[0] = "<System Default>";
		for (var i = 0; i < devices.Length; i++)
		{
			options[i + 1] = devices[i];
		}

		var selectedIndex = GetSelectedIndex(devices);
		var newIndex = EditorGUILayout.Popup("Input Device", selectedIndex, options);
		if (newIndex != selectedIndex)
		{
			if (newIndex <= 0)
			{
				useSystemDefaultMicrophoneProperty.boolValue = true;
				preferredMicrophoneDeviceProperty.stringValue = string.Empty;
			}
			else
			{
				useSystemDefaultMicrophoneProperty.boolValue = false;
				preferredMicrophoneDeviceProperty.stringValue = devices[newIndex - 1];
			}
		}

		if (devices.Length == 0)
		{
			EditorGUILayout.HelpBox("No microphone devices detected by Unity.", MessageType.Warning);
		}
		else
		{
			EditorGUILayout.HelpBox("Pick one device here so voice input does not rely on whichever device appears first.", MessageType.Info);
		}

		EditorGUI.BeginDisabledGroup(!Application.isPlaying);
		if (GUILayout.Button("Apply Mic Selection Now"))
		{
			serializedObject.ApplyModifiedProperties();
			var voiceRecognition = target as VoiceRecognition;
			if (voiceRecognition != null)
			{
				voiceRecognition.ApplyMicrophoneSelectionNow();
				EditorUtility.SetDirty(voiceRecognition);
			}
		}
		EditorGUI.EndDisabledGroup();
	}

	private int GetSelectedIndex(string[] devices)
	{
		if (useSystemDefaultMicrophoneProperty.boolValue)
		{
			return 0;
		}

		var preferred = preferredMicrophoneDeviceProperty.stringValue;
		if (string.IsNullOrWhiteSpace(preferred) || devices == null || devices.Length == 0)
		{
			return 0;
		}

		for (var i = 0; i < devices.Length; i++)
		{
			if (string.Equals(devices[i], preferred, System.StringComparison.OrdinalIgnoreCase))
			{
				return i + 1;
			}
		}

		for (var i = 0; i < devices.Length; i++)
		{
			if (devices[i].IndexOf(preferred, System.StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return i + 1;
			}
		}

		return 0;
	}
}
#endif
