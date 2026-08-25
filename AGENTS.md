# Repository Guidelines

## Project Structure & Module Organization

This repository is a Unity 2021.3.30f1c1 project. Runtime code is under `Assets/`, with application features grouped in `Script`, `PlayerSystem`, `LrcSystem`, and `SubtitlesSystem`. Models and prefabs live in `Character`; media is in `Audio`; visual effects and imported packages include `PostProcess`, `Toon`, `TextMesh Pro`, and `MagicaCloth2`. Scenes are stored in `Assets/Scenes` (the default build scene is `Assets/Scenes/Player.unity`). Keep Unity-generated `.meta` files beside every asset. Project-wide settings and package versions belong in `ProjectSettings/` and `Packages/`.

## Build, Test, and Development Commands

Install Git LFS before cloning or updating large assets:

```bash
git lfs install
git lfs pull
```

Open the repository with Unity Hub using editor version `2021.3.30f1c1`, then press **Play** to run the demo. Create a player build from **File > Build Settings**, keeping `Assets/Scenes/Player.unity` enabled. There is no checked-in scripted build or test runner command; do not commit `Library/`, `Temp/`, `Build/`, or other ignored generated directories.

## Coding Style & Naming Conventions

Use four spaces in C# and preserve the surrounding file's brace and `return` style. Classes and public members use PascalCase; existing private fields and helper methods commonly use lower camelCase (for example, `focusObj` and `setupFocusObject`). Name folders and Unity assets descriptively in PascalCase where that convention already exists. Make Inspector-facing changes in Unity and retain serialized references; move or rename assets inside the Editor so their `.meta` GUIDs remain intact.

## Testing Guidelines

The Unity Test Framework (`com.unity.test-framework`) is available, but no project tests are currently checked in. For behavior changes, use the Unity Test Runner (EditMode tests for pure logic, PlayMode tests for scene/runtime behavior) and manually exercise the affected scene. Place new tests in a clearly named `Assets/Tests/...` folder and use names such as `LrcLoaderTests` or `PlayerPlaybackTests`.

## Commit & Pull Request Guidelines

Recent commits use short, imperative summaries (for example, `添加抽帧` or `Update README.md`). Keep each commit focused and describe the user-visible change. Pull requests should explain the purpose, list affected scenes/assets, identify Unity version and validation performed, and include screenshots or a short video for visual or animation changes. Mention any required Git LFS assets and call out changes to `ProjectSettings` or package manifests.

## Asset & Configuration Notes

Do not commit credentials, local machine paths, or generated build output. Review large binary additions for Git LFS tracking, and verify that imported model, audio, font, and subtitle assets are redistributed only when their licenses permit it.
