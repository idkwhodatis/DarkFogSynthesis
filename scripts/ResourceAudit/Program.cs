using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Inspect PE metadata and embedded bytes only. Never load or execute the plugin or game.
return ResourceAudit.Run(args);

internal static class ResourceAudit
{
    private const string Prefix = "DarkFogSynthesis.Localization.";
    private static readonly string[] Locales = { "en-US", "zh-CN" };

    internal static int Run(string[] args)
    {
        try
        {
            if (args.SequenceEqual(new[] { "--self-test" })) { SelfTest(); return 0; }
            if (args.Length != 2 && (args.Length != 4 || args[2] != "--json-report"))
            {
                Console.Error.WriteLine("Usage: ResourceAudit <plugin.dll> <localization-source-directory> [--json-report <path>] | --self-test");
                return 2;
            }
            var report = Audit(args[0], args[1]);
            if (args.Length == 4)
            {
                string path = Path.GetFullPath(args[3]);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            }
            Console.WriteLine("PASS: both bilingual JSON dictionaries are embedded in the main DLL, exactly match approved source bytes, and require no satellite DLLs.");
            Console.WriteLine("Plugin SHA256: " + report.assemblySha256);
            Console.WriteLine("Scope: artifact resources only. No game/plugin code was loaded or executed.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("Resource audit failed: " + error.Message);
            return 1;
        }
    }

    private static AuditReport Audit(string assemblyPath, string sourceDirectory)
    {
        string fullAssemblyPath = Path.GetFullPath(assemblyPath);
        string directory = Path.GetDirectoryName(fullAssemblyPath)!;
        if (Directory.EnumerateFiles(directory, "DarkFogSynthesis.resources.dll", SearchOption.AllDirectories).Any())
            throw new InvalidDataException("Unexpected localization satellite DLL. Clean the build outputs and rebuild; all dictionaries must be in the main DLL.");
        byte[] assembly = File.ReadAllBytes(fullAssemblyPath);
        using var stream = new MemoryStream(assembly, false);
        using var pe = new PEReader(stream);
        if (!pe.HasMetadata || pe.PEHeaders.CorHeader == null) throw new InvalidDataException("Not a managed PE assembly.");
        MetadataReader metadata = pe.GetMetadataReader();
        var resources = new Dictionary<string, ManifestResource>(StringComparer.Ordinal);
        foreach (ManifestResourceHandle handle in metadata.ManifestResources)
        {
            ManifestResource resource = metadata.GetManifestResource(handle);
            string name = metadata.GetString(resource.Name);
            if (!resources.TryAdd(name, resource)) throw new InvalidDataException("Duplicate manifest resource: " + name);
        }
        var expected = Locales.Select(locale => Prefix + "Strings." + locale + ".json").ToHashSet(StringComparer.Ordinal);
        if (!resources.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected))
            throw new InvalidDataException("Main assembly must contain exactly the two expected locale resources: " + string.Join(", ", expected));
        var section = pe.PEHeaders.CorHeader.ResourcesDirectory;
        if (section.Size <= 0) throw new InvalidDataException("Missing embedded-resource data section.");
        var data = pe.GetSectionData(section.RelativeVirtualAddress).GetContent(0, section.Size);
        var entries = new List<ResourceEntry>();
        HashSet<string>? firstKeys = null;
        foreach (string locale in Locales)
        {
            string sourceName = "Strings." + locale + ".json";
            string name = Prefix + sourceName;
            ManifestResource resource = resources[name];
            if (!resource.Implementation.IsNil) throw new InvalidDataException("Locale must be embedded, not linked: " + name);
            if (resource.Offset < 0 || resource.Offset > data.Length - sizeof(int)) throw new InvalidDataException("Invalid embedded-resource offset: " + name);
            int offset = checked((int)resource.Offset);
            int length = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, sizeof(int)));
            if (length < 0 || length > data.Length - offset - sizeof(int)) throw new InvalidDataException("Invalid embedded-resource length: " + name);
            byte[] embedded = data.AsSpan(offset + sizeof(int), length).ToArray();
            byte[] source = File.ReadAllBytes(Path.Combine(sourceDirectory, sourceName));
            HashSet<string> keys = ReadLocale(embedded, name);
            ReadLocale(source, sourceName);
            if (!embedded.AsSpan().SequenceEqual(source)) throw new InvalidDataException("Embedded locale differs from approved source bytes: " + sourceName);
            if (firstKeys != null && !firstKeys.SetEquals(keys)) throw new InvalidDataException("English/Chinese localization key sets differ.");
            firstKeys = keys;
            entries.Add(new ResourceEntry(name, sourceName, Hash(source), keys.Count));
        }
        return new AuditReport(1, Hash(assembly), true, entries.ToArray());
    }

    private static HashSet<string> ReadLocale(byte[] bytes, string name)
    {
        using JsonDocument json = JsonDocument.Parse(bytes);
        if (json.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Locale is not a dictionary: " + name);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in json.RootElement.EnumerateObject())
        {
            if (!keys.Add(property.Name)) throw new InvalidDataException("Duplicate JSON key: " + property.Name);
            if (!property.Name.StartsWith("dark_fog_synthesis.", StringComparison.Ordinal) || property.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.Value.GetString()))
                throw new InvalidDataException("Locale requires namespaced keys and nonempty string values: " + name);
        }
        if (!RequiredKeys().IsSubsetOf(keys)) throw new InvalidDataException("Missing frozen technology/recipe/tab localization keys: " + name);
        return keys;
    }

    private static HashSet<string> RequiredKeys()
    {
        var required = new HashSet<string>(StringComparer.Ordinal) { "dark_fog_synthesis.recipes.tab" };
        foreach (string tech in new[] { "energy_analysis", "information_topology" })
            foreach (string field in new[] { "name", "description", "conclusion" })
                required.Add($"dark_fog_synthesis.tech.{tech}.{field}");
        foreach (string recipe in new[] { "energy_shard", "dark_fog_matrix", "silicon_neuron", "matter_recombinator", "negentropy_singularity", "core_element" })
            foreach (string field in new[] { "name", "description" })
                required.Add($"dark_fog_synthesis.recipe.{recipe}.{field}");
        return required;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private sealed record ResourceEntry(string name, string sourceName, string sha256, int keyCount);
    private sealed record AuditReport(int schemaVersion, string assemblySha256, bool satelliteFree, ResourceEntry[] resources);

    private static void SelfTest()
    {
        string root = Path.Combine(Path.GetTempPath(), "darkfog-resource-audit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        int count = 0;
        try
        {
            var values = RequiredKeys().Order(StringComparer.Ordinal).ToDictionary(key => key, _ => "Fixture 文本");
            byte[] valid = JsonSerializer.SerializeToUtf8Bytes(values);
            var good = Locales.Select(locale => (Name: Prefix + "Strings." + locale + ".json", Bytes: valid)).ToArray();
            string assembly = Path.Combine(root, "fixture.dll");
            void RunCase(string name, (string Name, byte[] Bytes)[] entries, bool passes, string expected = "", bool linked = false)
            {
                foreach (string locale in Locales) File.WriteAllBytes(Path.Combine(root, "Strings." + locale + ".json"), valid);
                WriteFixture(assembly, entries, linked);
                try
                {
                    Audit(assembly, root);
                    if (!passes) throw new Exception("Self-test unexpectedly accepted " + name);
                }
                catch (Exception error) when (!passes && error.Message.Contains(expected, StringComparison.Ordinal)) { }
                count++;
                Console.WriteLine("PASS self-test: " + name);
            }
            RunCase("valid main-assembly bilingual resources", good, true);
            RunCase("original startup failure: zero main resources", Array.Empty<(string, byte[])>(), false, "exactly the two");
            RunCase("one missing language", good.Take(1).ToArray(), false, "exactly the two");
            RunCase("culture-stripped resource name", new[] { (Prefix + "Strings.json", valid) }, false, "exactly the two");
            RunCase("duplicate manifest resource name", good.Concat(good.Take(1)).ToArray(), false, "Duplicate manifest");
            RunCase("linked resource rejected", good, false, "not linked", true);
            RunCase("truncated invalid JSON", new[] { (good[0].Name, Encoding.UTF8.GetBytes("{")), good[1] }, false, "Expected");
            byte[] duplicate = Encoding.UTF8.GetBytes("{\"dark_fog_synthesis.x\":\"one\",\"dark_fog_synthesis.x\":\"two\"}");
            RunCase("duplicate JSON key", new[] { (good[0].Name, duplicate), good[1] }, false, "Duplicate JSON key");
            RunCase("non-dictionary JSON", new[] { (good[0].Name, Encoding.UTF8.GetBytes("[]")), good[1] }, false, "not a dictionary");
            RunCase("missing frozen keys", new[] { (good[0].Name, Encoding.UTF8.GetBytes("{}")), good[1] }, false, "Missing frozen");
            RunCase("non-string locale value", new[] { (good[0].Name, Encoding.UTF8.GetBytes("{\"dark_fog_synthesis.x\":42}")), good[1] }, false, "nonempty string");
            RunCase("blank locale value", new[] { (good[0].Name, Encoding.UTF8.GetBytes("{\"dark_fog_synthesis.x\":\" \"}")), good[1] }, false, "nonempty string");
            var modified = new Dictionary<string, string>(values) { [values.Keys.First()] = "Changed" };
            RunCase("embedded bytes differ from approved source", new[] { (good[0].Name, JsonSerializer.SerializeToUtf8Bytes(modified)), good[1] }, false, "differs from approved");
            WriteFixture(assembly, good, false);
            var extra = new Dictionary<string, string>(values) { ["dark_fog_synthesis.extra"] = "Extra" };
            byte[] unequal = JsonSerializer.SerializeToUtf8Bytes(extra);
            File.WriteAllBytes(Path.Combine(root, "Strings.zh-CN.json"), unequal);
            WriteFixture(assembly, new[] { good[0], (good[1].Name, unequal) }, false);
            ExpectFailure("mismatched locale key sets", () => Audit(assembly, root), "key sets differ"); count++;
            Directory.CreateDirectory(Path.Combine(root, "en-US"));
            File.WriteAllBytes(Path.Combine(root, "en-US", "DarkFogSynthesis.resources.dll"), Array.Empty<byte>());
            RunCase("satellite DLL rejected", good, false, "satellite DLL");
            Console.WriteLine($"PASS: {count} metadata-only resource audit self-tests. Fixtures contain no game/plugin executable code.");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void ExpectFailure(string name, Action action, string expected)
    {
        try { action(); }
        catch (Exception error) when (error.Message.Contains(expected, StringComparison.Ordinal))
        {
            Console.WriteLine("PASS self-test: " + name);
            return;
        }
        throw new Exception("Self-test unexpectedly accepted " + name);
    }

    private static void WriteFixture(string path, (string Name, byte[] Bytes)[] resources, bool linked)
    {
        // A metadata-only test PE: no methods, runtime dependency, or game symbols.
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString("ResourceAuditFixture.dll"), metadata.GetOrAddGuid(Guid.Empty), default, default);
        metadata.AddAssembly(metadata.GetOrAddString("ResourceAuditFixture"), new Version(1, 0), default, default, 0, AssemblyHashAlgorithm.None);
        var blob = new BlobBuilder();
        EntityHandle external = linked ? metadata.AddAssemblyFile(metadata.GetOrAddString("linked.resources"), default, false) : default;
        foreach (var resource in resources)
        {
            uint offset = checked((uint)blob.Count);
            blob.WriteInt32(resource.Bytes.Length);
            blob.WriteBytes(resource.Bytes);
            metadata.AddManifestResource(ManifestResourceAttributes.Public, metadata.GetOrAddString(resource.Name), external, offset);
        }
        var pe = new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(metadata), new BlobBuilder(), managedResources: blob);
        var output = new BlobBuilder();
        pe.Serialize(output);
        File.WriteAllBytes(path, output.ToArray());
    }
}
