# Kitchen verification — 2026-10-13

Unity **6000.5.6f1**, XR Interaction Toolkit **3.5.1**, OpenXR **1.17.1**, Input System **1.20.0**. This record establishes build and automated regression results, not hardware acceptance or a rubric score.

| Check | Result | Evidence |
| --- | --- | --- |
| Editor scene generation and compilation | Passed | `Logs/Kitchen-EditorValidation.log`, `KITCHEN_SCENE_BUILD_PASS` |
| Food rule boundaries | Passed | Same log, `KITCHEN_FOOD_RULES_PASS`; exact chicken 20/40 and steak 8/18 boundaries, energy effects, cumulative wash |
| Real Play Mode regression | **125 assertions passed**, editor teardown verified | `Logs/kitchen-play-checks.json`, `Logs/Kitchen-PlayChecks.log` |
| Physical pan and steak | Passed | Dynamic bodies fall and settle; after 2.011 seconds steak had 1.750 cooking seconds; both velocities zero |
| Desktop input path | Passed with in-process virtual devices | E opens door, wheel adjusts knob, left click grabs/claims, F consumes; no OS keyboard/mouse takeover |
| Windows build | Succeeded, **0 errors** | `Logs/Kitchen-WindowsBuild.log`; `Builds/Windows/Kitchen.exe` plus adjacent runtime files |
| Windows player startup | Remained running after 15 seconds; no gameplay exception observed | `Logs/Kitchen-PC-Smoke.log`; test process closed after observation; not a manual full recipe run |
| Quest APK | Succeeded, **0 errors**, 47,584,966 bytes | `Logs/Kitchen-QuestBuild.log`; `Builds/Quest/Kitchen.apk` |
| APK inspection | ARM64, minimum API 29, target API 36; VR category and head tracking declared | Android build-tools `aapt` inspection of the final APK |
| Quest 2 / Quest 3 hardware | **Pending** | No device was connected in `adb devices -l` |

APK SHA-256: `160a8106e2847efe33b778b51284a62b5564ba919a5dc3a732e329253ec07b83`.

The Play Mode assertions cover all meat energy states and duplicate consumption, clean/dirty salad and ingredient accounting, refrigerator replenishment without duplicate or overlapping food, timer expiry/open-door pause, heat rates and pan requirements, foundation/occupancy rejection, actual trash trigger disposal, shared inventory ownership, and settlement after KitchenSession destruction.

The editor emitted a known local `UnityEditor.Search.SearchDatabase` startup `ArgumentOutOfRangeException`. The harness records its exact engine-only stack separately under `editorDiagnostics`. Other errors, exceptions, or assertions still fail the run, including errors while exiting Play Mode. The final run reports `editorTeardownVerified: true`.

The Android toolchain rejects the original Chinese project path. The successful APK was built from an ordinary ASCII temporary copy containing the same Assets, Packages, and ProjectSettings, without copying Library or Temp. The APK and log were copied back to this checkout. `Tools/Build-Quest.ps1` reproduces this workflow. Build outputs and logs are local, ignored files, not source assets to merge into other rooms.

Still needed: manually complete all recipes in the Windows player; on both Quest models complete the loop with only the left controller and only the right controller; verify text readability, reach, knife contact, comfort and frame rate; record contribution video and genuine teammate PR review/merge evidence. See `Contribution-Checklist.md`.
