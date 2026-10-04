# Compiled localization resource audit

This .NET 8 tool uses PE metadata and resource bytes only. It does not load the
plugin into the CLR, resolve its dependencies, or execute game code.

```sh
dotnet run --project scripts/ResourceAudit -- --self-test
dotnet run --project scripts/ResourceAudit -- \
  src/DarkFogSynthesis/bin/Release/net472/DarkFogSynthesis.dll \
  src/DarkFogSynthesis/Localization --json-report artifacts/resource-audit.json
```

It requires exactly `DarkFogSynthesis.Localization.Strings.en-US.json` and
`DarkFogSynthesis.Localization.Strings.zh-CN.json` in the main DLL. Both must be
embedded (not linked), valid dictionaries with unique namespaced keys, nonempty
string values, the frozen technology/recipe/tab keys, equal language key sets,
and bytes identical to the checked-in localization sources. The existing source
validator separately checks the approved technology prose against the frozen plan.

It rejects `DarkFogSynthesis.resources.dll` satellites anywhere beneath the
runtime output directory. The plugin reads raw dictionaries directly; .NET
culture inference must be disabled with `WithCulture="false"` and explicit
`LogicalName` metadata in the runtime project.

The 15 self-tests construct metadata-only fixture DLLs, including the original
zero-main-resource failure, misnamed/missing/duplicate/linked resources, malformed
JSON, invalid values, changed source bytes, unequal locale keys, and satellite
leakage. Fixtures are generated outside the repository and deleted afterward.

`Build.ps1` runs the self-tests even in CoreOnly mode, cleans previous runtime
outputs, builds, resolves the effective paths using MSBuild's `-getProperty`
JSON output (SDK 8+), then runs this audit and PublicApiAudit before recording
runtime build provenance. Resource correctness is not in-game validation.
