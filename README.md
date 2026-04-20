# EchoesOfEternia

EchoesOfEternia - це Unity-проєкт із базовою сценою, керуванням персонажем, взаємодією з платформою та голосовими командами для заклять.

## Що є в проєкті
У сцені додано три основні об'єкти: персонаж, ground і spell visualizer. Персонаж рухається по сцені, ground працює як опора, а spell visualizer показує результат голосової команди через зміну кольору.

Голосова частина обробляє команди ignis, mentiri, echo, tise. Для стабільнішої роботи додано alias, fuzzy matching і fallback логіку, а також інструменти діагностики мікрофона.

## Основні скрипти
- [PlayerMovement](Assets/Scripts/Player/Movement/PlayerMovement.cs)
- [SimpleBlock](Assets/Scripts/Platforms/SimplePlatform/SimpleBlock.cs)
- [VoiceRecognition](Assets/Scripts/Player/Voice/VoiceRecognition.cs)
- [PhraseColorVisualizer](Assets/Scripts/Player/Voice/PhraseColorVisualizer.cs)
- [VoiceInputDebugPanel](Assets/Scripts/Player/Voice/VoiceInputDebugPanel.cs)
- [VoiceRecognitionEditor](Assets/Scripts/Player/Voice/Editor/VoiceRecognitionEditor.cs)

## Changelog
Історія змін винесена в окремий файл: [CHANGELOG.md](CHANGELOG.md)