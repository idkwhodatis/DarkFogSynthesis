# Original game API accessibility audit

Publicized reference assemblies can make a private game member appear public to the C# compiler. A successful compile against them alone is insufficient evidence that direct calls will work against the original installed game.

This tool reads compiled plugin IL and the supplied game's metadata with Mono.Cecil. It honors `BepInEx.AssemblyPublicizer.OriginalAttributesAttribute`, audits direct method/field references and game type visibility, and exits nonzero for nonpublic or unresolved references. Ordinary non-public member flags are also recognized when auditing an original, non-publicized game assembly. It never runs game code and does not copy game assemblies into the repository.

```sh
dotnet run --project scripts/PublicApiAudit -- \
  src/DarkFogSynthesis/bin/Release/net472/DarkFogSynthesis.dll \
  /your/reference/path/Assembly-CSharp.dll \
  /your/reference/path --json-report artifacts/public-api-audit.json
```

Additional positional arguments are dependency search directories. Output includes SHA-256 values tying the result to both exact input assemblies. The optional final `--json-report <path>` writes structured results; runtime packaging requires this successful report plus the ResourceAudit report, both matching the built plugin hash. Build.ps1 produces both before recording provenance.

The check conservatively requires external public visibility; it does not try to justify protected access through subclassing or friend-assembly grants. Reflection and Harmony target signatures require separate review. Passing this audit does **not** verify game integration, lifecycle ordering, production, save safety, achievements or integrity, and does not establish that public reference packages match the user's actual installation.
