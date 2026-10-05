# Optional Unity UI geometry tests

**Not executed by the ordinary .NET/Harmony/Python CI.** These fixtures require an
installed, licensed Unity 2022.3 editor. They do not load DSP, BepInEx or the plugin.
The preparation script copies the **exact production `RectMaskClipCapture.cs`** into
an isolated test assembly alongside the NUnit fixture; it does not substitute Unity types.

```powershell
python scripts/prepare-unity-ui-tests.py C:/Temp/DFS-UiGeometry-Tests
& 'C:/Path/To/Unity/Editor/Unity.exe' -batchmode -projectPath C:/Temp/DFS-UiGeometry-Tests `
  -runTests -testPlatform EditMode -testResults C:/Temp/DFS-UiGeometry-results.xml `
  -logFile C:/Temp/DFS-UiGeometry.log
```

Use an external, previously nonexistent directory. The script only prepares files;
it does not install the editor, run tests, change the original checkout or touch saves.
Open the generated project in the editor's Test Runner to run interactively instead.
Tests cover root-canvas scale 1/1.5/2, independent mask-transform scaling, nonzero
padding on all four sides, nested masks, and an empty padded region. Retain the
actual XML/log before claiming an engine-side pass. Pure Python geometry fixtures
exercise only the receiving checker; reference compilation establishes API signatures,
not the behavior of native Unity transforms. DSP panel usage still requires game acceptance.
