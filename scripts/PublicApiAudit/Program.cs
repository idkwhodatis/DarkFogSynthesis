using System.Globalization;
using System.Security.Cryptography;
using Mono.Cecil;

// Metadata inspection only: this tool never loads a game assembly into the CLR or executes game code.
if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: dotnet run --project scripts/PublicApiAudit -- <plugin.dll> <Assembly-CSharp.dll> [dependency search directories...]");
    return 2;
}

try
{
    using var resolver = new AuditResolver();
    foreach (var directory in args.Skip(2).Concat(args.Take(2).Select(path => Path.GetDirectoryName(Path.GetFullPath(path))!)).Distinct())
        resolver.AddSearchDirectory(directory);
    using var game = AssemblyDefinition.ReadAssembly(args[1], new ReaderParameters { AssemblyResolver = resolver });
    resolver.Use(game);
    using var plugin = AssemblyDefinition.ReadAssembly(args[0], new ReaderParameters { AssemblyResolver = resolver });
    var findings = new SortedSet<string>(StringComparer.Ordinal);
    var unresolved = new SortedSet<string>(StringComparer.Ordinal);
    var checkedMembers = new HashSet<string>(StringComparer.Ordinal);
    var checkedTypes = new HashSet<string>(StringComparer.Ordinal);
    int directSites = 0;
    int originalAttributeCount = AllTypes(game.MainModule.Types).Sum(type => Original(type).HasValue ? 1 : 0)
        + AllTypes(game.MainModule.Types).SelectMany(type => type.Methods.Cast<ICustomAttributeProvider>().Concat(type.Fields))
            .Count(entity => Original(entity).HasValue);

    // Type references include local/parameter signatures, casts, generic arguments and attribute typeof operands.
    foreach (var reference in plugin.MainModule.GetTypeReferences())
        if (GameScope(reference)) CheckType(reference, "type/signature reference");

    foreach (var type in AllTypes(plugin.MainModule.Types))
        foreach (var caller in type.Methods.Where(method => method.HasBody))
            foreach (var instruction in caller.Body.Instructions)
            {
                if (instruction.Operand is TypeReference typeOperand)
                {
                    if (GameScope(typeOperand)) CheckType(typeOperand, caller.FullName + " IL_" + instruction.Offset.ToString("X4",CultureInfo.InvariantCulture));
                    continue;
                }
                if (instruction.Operand is not MemberReference reference || reference.DeclaringType == null || !GameScope(reference.DeclaringType)) continue;
                directSites++;
                string location = caller.FullName + " IL_" + instruction.Offset.ToString("X4",CultureInfo.InvariantCulture) + " " + instruction.OpCode;
                try
                {
                    IMemberDefinition? definition = reference switch
                    {
                        MethodReference method => method.Resolve(),
                        FieldReference field => field.Resolve(),
                        _ => null
                    };
                    if (definition == null) { unresolved.Add(location + ": " + reference.FullName); continue; }
                    checkedMembers.Add(definition.FullName);
                    CheckType(definition.DeclaringType, location);
                    int original = Original(definition) ?? definition switch
                    {
                        MethodDefinition method => (int)method.Attributes,
                        FieldDefinition field => (int)field.Attributes,
                        _ => 0
                    };
                    bool accessible = definition switch
                    {
                        MethodDefinition => (original & (int)MethodAttributes.MemberAccessMask) == (int)MethodAttributes.Public,
                        FieldDefinition => (original & (int)FieldAttributes.FieldAccessMask) == (int)FieldAttributes.Public,
                        _ => false
                    };
                    if (!accessible)
                        findings.Add(location + ": " + definition.FullName + " originally nonpublic (attributes=" + original + ")");
                }
                catch (Exception error) { unresolved.Add(location + ": " + reference.FullName + ": " + error.Message); }
            }

    Console.WriteLine("Plugin SHA256: " + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0]))).ToLowerInvariant());
    Console.WriteLine("Game reference SHA256: " + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[1]))).ToLowerInvariant());
    Console.WriteLine("OriginalAttributes metadata entries: " + originalAttributeCount);
    foreach (var finding in findings) Console.Error.WriteLine("NONPUBLIC " + finding);
    foreach (var error in unresolved) Console.Error.WriteLine("UNRESOLVED " + error);
    Console.WriteLine($"Audited {directSites} direct game member sites, {checkedMembers.Count} unique members, {checkedTypes.Count} game types: {findings.Count} nonpublic accesses, {unresolved.Count} unresolved references.");
    Console.WriteLine("Scope: external public accessibility against the supplied game's original metadata only. This does not establish gameplay compatibility, installed-game equivalence, or validity of reflection/Harmony patch targets.");
    return findings.Count == 0 && unresolved.Count == 0 ? 0 : 1;

    bool GameScope(TypeReference reference) => reference.GetElementType().Scope?.Name == game.Name.Name;

    void CheckType(TypeReference reference, string location)
    {
        try
        {
            var type = reference.Resolve();
            if (type == null) { unresolved.Add(location + ": cannot resolve type " + reference.FullName); return; }
            for (var current = type; current != null; current = current.DeclaringType)
            {
                checkedTypes.Add(current.FullName);
                int attributes = Original(current) ?? (int)current.Attributes;
                int visibility = attributes & (int)TypeAttributes.VisibilityMask;
                bool accessible = current.IsNested ? visibility == (int)TypeAttributes.NestedPublic : visibility == (int)TypeAttributes.Public;
                if (!accessible) findings.Add(location + ": " + current.FullName + " originally nonpublic type (attributes=" + attributes + ")");
            }
        }
        catch (Exception error) { unresolved.Add(location + ": type " + reference.FullName + ": " + error.Message); }
    }
}
catch (Exception error)
{
    Console.Error.WriteLine("AUDIT FAILED: " + error);
    return 2;
}

static int? Original(ICustomAttributeProvider entity)
{
    var attribute = entity.CustomAttributes.SingleOrDefault(a => a.AttributeType.FullName == "BepInEx.AssemblyPublicizer.OriginalAttributesAttribute");
    if (attribute == null) return null;
    if (attribute.ConstructorArguments.Count != 1) throw new InvalidDataException("Unexpected OriginalAttributes encoding.");
    return Convert.ToInt32(attribute.ConstructorArguments[0].Value,CultureInfo.InvariantCulture);
}

static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> roots)
{
    foreach (var type in roots)
    {
        yield return type;
        foreach (var nested in AllTypes(type.NestedTypes)) yield return nested;
    }
}

sealed class AuditResolver : DefaultAssemblyResolver
{
    public void Use(AssemblyDefinition assembly) => RegisterAssembly(assembly);
}
