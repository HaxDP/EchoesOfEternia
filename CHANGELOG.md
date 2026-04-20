# Changelog

## 2026-04-20
У цьому оновленні структура проєкту стала повнішою для роботи з ігровою логікою та голосовим керуванням. Додано нову сцену з трьома ключовими об'єктами: character, ground і spell visualizer. Це дало просту, але зручну основу для тесту руху, взаємодії та візуальної реакції на команди.

Також додано нові скрипти для платформи, руху гравця та голосових механік. Окремо реалізовано візуалізатор фраз, панель діагностики мікрофона і кастомний інспектор для вибору конкретного мікрофона в Unity Editor. Завдяки цьому простіше перевіряти, чи справді є вхідний звук, і швидше знаходити проблему з гарнітурою.

У цій версії додані такі файли:

- Сцена та базові службові файли Unity: [Assets/Scenes/SampleScene.unity](Assets/Scenes/SampleScene.unity), [Assets/Scenes/SampleScene.unity.meta](Assets/Scenes/SampleScene.unity.meta), [Assets/Scenes.meta](Assets/Scenes.meta).
- Скрипти геймплею: [Assets/Scripts/Platforms/SimplePlatform/SimpleBlock.cs](Assets/Scripts/Platforms/SimplePlatform/SimpleBlock.cs), [Assets/Scripts/Player/Movement/PlayerMovement.cs](Assets/Scripts/Player/Movement/PlayerMovement.cs).
- Скрипти голосу та візуалізації: [Assets/Scripts/Player/Voice/VoiceRecognition.cs](Assets/Scripts/Player/Voice/VoiceRecognition.cs), [Assets/Scripts/Player/Voice/PhraseColorVisualizer.cs](Assets/Scripts/Player/Voice/PhraseColorVisualizer.cs), [Assets/Scripts/Player/Voice/VoiceInputDebugPanel.cs](Assets/Scripts/Player/Voice/VoiceInputDebugPanel.cs), [Assets/Scripts/Player/Voice/Editor/VoiceRecognitionEditor.cs](Assets/Scripts/Player/Voice/Editor/VoiceRecognitionEditor.cs).
- Службові файли проєкту та налаштувань, які були додані разом із порожнім Unity-проєктом і подальшими оновленнями: [.gitignore](.gitignore), [Packages/manifest.json](Packages/manifest.json), [Packages/packages-lock.json](Packages/packages-lock.json), файли в [ProjectSettings](ProjectSettings/).

## 2026-04-06
Початкове створення Unity-проєкту з базовими налаштуваннями URP, Input System, стартовою сценою та стандартною структурою Assets/Packages/ProjectSettings.