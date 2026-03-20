using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace ROBdk97.XmlDocToMd.Conversion;

/// <summary>
/// Provides reflection-based type and member information queries for enriching member
/// signatures with resolved type names and visibility information extracted from
/// compiled assemblies.
/// </summary>
internal static class ReflectionHelper
{

    private static readonly Dictionary<string, Assembly> _assemblyCache =
        new(StringComparer.OrdinalIgnoreCase);


    private static readonly Dictionary<string, Type?> _typeCache =
        new(StringComparer.OrdinalIgnoreCase);


    private static readonly Dictionary<string, Type?> _returnTypeCache =
        new(StringComparer.OrdinalIgnoreCase);


    private static readonly Dictionary<string, string?> _returnTypeNameCache =
        new(StringComparer.OrdinalIgnoreCase);


    private static readonly Dictionary<string, bool> _isPublicCache =
        new(StringComparer.OrdinalIgnoreCase);


    private static string _currentAssemblyPath = string.Empty;

    private const BindingFlags MemberLookupBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

    /// <summary>
    /// Sets the current assembly path to be used for reflection operations. Should be called
    /// before processing each XML file.
    /// </summary>
    internal static void SetCurrentXmlFile(string xmlFilePath)
    {
        ArgumentNullException.ThrowIfNull(xmlFilePath);
        _currentAssemblyPath = ResolveAssemblyPath(xmlFilePath);
    }

    private static string GetCurrentAssemblyPath() =>
        _currentAssemblyPath;

    private static string ResolveAssemblyPath(string xmlFilePath)
    {
        var dllPath = Path.ChangeExtension(xmlFilePath, ".dll");
        if (File.Exists(dllPath))
        {
            return dllPath;
        }

        var exePath = Path.ChangeExtension(xmlFilePath, ".exe");
        if (File.Exists(exePath))
        {
            return exePath;
        }

        return dllPath;
    }

    /// <summary>
    /// Returns the <see cref="Type"/> of the field <paramref name="fieldName"/> declared
    /// on <paramref name="fullClassName"/>, or <see langword="null"/> if resolution fails.
    /// </summary>
    /// <param name="fullClassName">Fully-qualified class name (e.g. <tt>MyNs.MyClass</tt>).</param>
    /// <param name="fieldName">Simple field name without qualification.</param>
    internal static Type? GetFieldType(string fullClassName, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(fullClassName);
        ArgumentNullException.ThrowIfNull(fieldName);

        try
        {
            Type? type = GetTypeT(fullClassName);
            if (type is null) return null;
            FieldInfo? field = FindField(type, fieldName);
            return field?.FieldType;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Returns the return <see cref="Type"/> of <paramref name="methodName"/> declared on
    /// <paramref name="fullClassName"/>, or <see langword="null"/> if resolution fails.
    /// </summary>
    /// <param name="fullClassName">Fully-qualified class name.</param>
    /// <param name="methodName">
    /// Method name, optionally including a parameter list (e.g. <tt>DoWork(System.String)</tt>).
    /// The parameter list is stripped before the lookup if an exact match fails.
    /// </param>
    internal static Type? GetMethodReturnType(string fullClassName, string methodName)
    {
        ArgumentNullException.ThrowIfNull(fullClassName);
        ArgumentNullException.ThrowIfNull(methodName);

        try
        {
            Type? type = GetTypeT(fullClassName);
            if (type is null) return null;
            var method = type.GetMethod(methodName);
            if (method is null)
            {
                int parenIndex = methodName.IndexOf('(');
                if (parenIndex > 0)
                    methodName = methodName[..parenIndex];
                method = type.GetMethods().FirstOrDefault(m => m.Name == methodName);
            }
            return method?.ReturnType;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Resolves <paramref name="fullClassName"/> to a <see cref="Type"/>, loading the
    /// assembly from disk on first access and caching both the assembly and the type.
    /// </summary>
    private static Type? GetTypeT(string fullClassName)
    {
        string assemblyPath = GetCurrentAssemblyPath();

        if (!_assemblyCache.TryGetValue(assemblyPath, out var assembly))
        {
            if (File.Exists(assemblyPath))
            {
                assembly = Assembly.LoadFrom(assemblyPath);
                _assemblyCache[assemblyPath] = assembly;
                LoadReferencedAssemblies(assembly, Path.GetDirectoryName(assemblyPath));
            }
            else
            {
                Console.WriteLine($"Assembly file not found: {assemblyPath}");
                Debug.WriteLine($"Assembly file not found: {assemblyPath}");
                return null;
            }
        }

        string typeCacheKey = $"{assemblyPath}\0{fullClassName}";
        if (_typeCache.TryGetValue(typeCacheKey, out var cachedType))
            return cachedType;

        var type = GetTypeFromAssembly(fullClassName, assembly);
        _typeCache[typeCacheKey] = type;
        return type;
    }

    private static void LoadReferencedAssemblies(Assembly assembly, string? assemblyDirectory)
    {
        if (string.IsNullOrWhiteSpace(assemblyDirectory) || !Directory.Exists(assemblyDirectory))
        {
            return;
        }

        foreach (var referencedAssemblyName in assembly.GetReferencedAssemblies())
        {
            if (AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => string.Equals(a.GetName().Name, referencedAssemblyName.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var candidatePaths = new[]
            {
                Path.Combine(assemblyDirectory, referencedAssemblyName.Name + ".dll"),
                Path.Combine(assemblyDirectory, referencedAssemblyName.Name + ".exe")
            };

            foreach (var candidatePath in candidatePaths)
            {
                if (!File.Exists(candidatePath) || _assemblyCache.ContainsKey(candidatePath))
                {
                    continue;
                }

                try
                {
                    var referencedAssembly = Assembly.LoadFrom(candidatePath);
                    _assemblyCache[candidatePath] = referencedAssembly;
                    LoadReferencedAssemblies(referencedAssembly, Path.GetDirectoryName(candidatePath));
                    break;
                }
                catch (FileLoadException ex)
                {
                    Debug.WriteLine($"Failed to load referenced assembly '{candidatePath}': {ex.Message}");
                }
                catch (BadImageFormatException ex)
                {
                    Debug.WriteLine($"Failed to load referenced assembly '{candidatePath}': {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Attempts to locate <paramref name="fullClassName"/> in <paramref name="assembly"/>
    /// using three strategies: direct lookup, parent-class lookup, and nested-type lookup.
    /// </summary>
    /// <param name="fullClassName">Fully-qualified class name to resolve.</param>
    /// <param name="assembly">The loaded assembly to search.</param>
    /// <returns>The resolved <see cref="Type"/>, or <see langword="null"/> on failure.</returns>
    private static Type? GetTypeFromAssembly(string fullClassName, Assembly assembly)
    {
        try
        {
            var type = assembly.GetType(fullClassName);
            if (type != null) return type;

            var nameBeforeLastDot = GetNameBeforeLastDot(fullClassName);
            if (nameBeforeLastDot != null)
            {
                type = assembly.GetType(nameBeforeLastDot);
                if (type != null) return type;
            }

            type = GetNestedType(fullClassName, assembly);
            if (type != null) return type;

            return null;
        }
        catch (TypeLoadException)
        {
            return null;
        }
    }

    /// <summary>
    /// Returns the portion of <paramref name="name"/> before its last <tt>'."</tt>,
    /// or <see langword="null"/> if no dot is present.
    /// </summary>
    private static string? GetNameBeforeLastDot(string name)
    {
        int lastDotIndex = name.LastIndexOf('.');
        return lastDotIndex == -1 ? null : name[..lastDotIndex];
    }

    /// <summary>
    /// Resolves a nested type by splitting <paramref name="fullClassName"/> at the last
    /// <tt>'."</tt> and calling <see cref="Type.GetNestedType(string)"/>.
    /// </summary>
    private static Type? GetNestedType(string fullClassName, Assembly assembly)
    {
        int lastDotIndex = fullClassName.LastIndexOf('.');
        if (lastDotIndex == -1) return null;

        string className = fullClassName[..lastDotIndex];
        string nestedClassName = fullClassName[(lastDotIndex + 1)..];

        Type? baseType = assembly.GetType(className);
        return baseType?.GetNestedType(nestedClassName);
    }

    /// <summary>
    /// Returns the <see cref="Type"/> of the property <paramref name="propertyName"/>
    /// declared on <paramref name="fullClassName"/>, or <see langword="null"/> on failure.
    /// </summary>
    private static Type? GetPropertyType(string fullClassName, string propertyName)
    {
        try
        {
            var type = GetTypeT(fullClassName);
            if (type is null) return null;
            var property = FindProperty(type, propertyName);
            return property?.PropertyType;
        }
        catch
        {
            return null;
        }
    }

    private static FieldInfo? FindField(Type type, string fieldName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var field = current.GetField(fieldName, MemberLookupBindingFlags);
            if (field is not null)
                return field;
        }

        return null;
    }

    private static PropertyInfo? FindProperty(Type type, string propertyName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var property = current.GetProperty(propertyName, MemberLookupBindingFlags);
            if (property is not null)
                return property;
        }

        foreach (var interfaceType in type.GetInterfaces())
        {
            var property = interfaceType.GetProperty(propertyName, MemberLookupBindingFlags);
            if (property is not null)
                return property;
        }

        return null;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the member identified by (<paramref name="fullClassName"/>
    /// <paramref name="name"/>, <paramref name="t"/>) is <see langword="public"/>
    /// </summary>
    /// <param name="fullClassName">Declaring type's fully-qualified name.</param>
    /// <param name="name">Simple member name.</param>
    /// <param name="t">
    /// Single-letter kind discriminator: <tt>"T"</tt> type, <tt>"F"</tt> field,
    /// <tt>"P"</tt> property, <tt>"M"</tt> method, <tt>"E"</tt> event.
    /// </param>
    private static bool IsPublic(string fullClassName, string name, string t)
    {
        try
        {
            const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

            var type = GetTypeT(fullClassName);
            if (type is null)
                return true;
            if (t == "T")
            {
                return type.IsVisible;
            }
            if (t == "F")
            {
                var field = type.GetField(name, bindingFlags);
                if (field != null)
                {
                    return field.IsPublic;
                }
                return true;
            }
            else if (t == "P")
            {
                var property = type.GetProperty(name, bindingFlags);
                if (property != null)
                {
                    var getter = property.GetGetMethod(true);
                    if (getter != null)
                        return getter.IsPublic;
                    var setter = property.GetSetMethod(true);
                    if (setter != null)
                        return setter.IsPublic;
                }
                return true;
            }
            else if (t == "M")
            {
                if (name.Contains("#ctor"))
                    return true;
                var parenIndex = name.IndexOf('(');
                var methodName = name;
                if (parenIndex > 0)
                    methodName = name[..parenIndex];
                var method = type.GetMethods(bindingFlags).FirstOrDefault(m => m.Name == methodName);
                if (method != null)
                    return method.IsPublic;
                return true;
            }
            else if (t == "E")
            {
                var eventInfo = type.GetEvent(name, bindingFlags);
                if (eventInfo != null)
                {
                    return eventInfo.AddMethod?.IsPublic == true
                        || eventInfo.RemoveMethod?.IsPublic == true
                        || eventInfo.RaiseMethod?.IsPublic == true;
                }
                return true;
            }
            return true;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when the member described by the <c>name</c> attribute
    /// of <paramref name="element"/> is publicly visible in the compiled assembly.
    /// </summary>
    /// <param name="element">
    /// An XML <c>&lt;member&gt;</c> element whose <c>name</c> attribute follows the
    /// standard <tt>T:Ns.Class</tt> / <tt>M:Ns.Class.Method</tt> format.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the member is public or its visibility cannot be
    /// resolved; <see langword="false"/> only when the member is confirmed to be
    /// non-public or the element is <see langword="null"/>.
    /// </returns>
    /// <note>
    /// Results are cached by <tt>dllPath\0nameAttr</tt> so repeated calls for the same
    /// member within one conversion session are effectively free.
    /// </note>
    internal static bool IsPublic(XElement element)
    {
        try
        {
            if (element is null)
                return false;
            var nameAttr = element.Attribute("name")?.Value;
            if (nameAttr is null)
                return false;
            if (!nameAttr.Contains(':'))
                return false;

            var cacheKey = $"{GetCurrentAssemblyPath()}\0{nameAttr}";
            if (_isPublicCache.TryGetValue(cacheKey, out bool cached))
                return cached;

            var result = ComputeIsPublic(nameAttr);
            _isPublicCache[cacheKey] = result;
            return result;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool ComputeIsPublic(string nameAttr)
    {
        var type = nameAttr.Split(':')[0];
        // Check if the type is T or M or P or F or E
        if (type != "T" && type != "M" && type != "P" && type != "F" && type != "E")
            return true;
        var className = nameAttr.Split(':')[1];
        var attributeName = className;
        if (className.Contains('('))
            className = className.Split('(')[0];
        // Remove Method name from class name if not T
        if (type != "T" && className.Contains('.'))
        {
            if (className.Contains(".#"))
            {
                // Constructor — the member itself is always "public" by convention,
                // but the declaring type must also be public.
                var declaringClass = className[..className.LastIndexOf(".#")];
                return IsPublic(declaringClass, string.Empty, "T");
            }
            else
                className = className[..className.LastIndexOf('.')];
        }
        // Remove class name from attribute name
        if (type != "T")
            attributeName = attributeName[(className.Length + 1)..];
        else
            attributeName = null;
        // For members, also verify the declaring type is publicly visible
        if (type != "T" && !IsPublic(className, string.Empty, "T"))
            return false;
        return IsPublic(className, attributeName ?? string.Empty, type);
    }

    /// <summary>
    /// Returns the return/value <see cref="Type"/> for the member described by
    /// (<paramref name="className"/>, <paramref name="type"/>, <paramref name="attribute"/>),
    /// or <see langword="null"/> when the DLL is unavailable or the member cannot be
    /// resolved.
    /// </summary>
    /// <param name="className">Fully-qualified declaring type name.</param>
    /// <param name="type">
    /// Kind discriminator: <tt>"M"</tt> method, <tt>"P"</tt> property, <tt>"F"</tt> field.
    /// </param>
    /// <param name="attribute">Simple member name (method may include parameter list).</param>
    internal static Type? GetReturnType(string className, string type, string attribute)
    {
        ArgumentNullException.ThrowIfNull(className);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(attribute);

        var key = $"{GetCurrentAssemblyPath()}\0{className}\0{type}\0{attribute}";
        if (_returnTypeCache.TryGetValue(key, out Type? cached))
            return cached;

        Type? result = type switch
        {
            "M" => GetMethodReturnType(className, attribute),
            "P" => GetPropertyType(className, attribute),
            "F" => GetFieldType(className, attribute),
            _ => null
        };
        _returnTypeCache[key] = result;
        return result;
    }

    /// <summary>
    /// Returns the metadata-based type name for the member described by
    /// (<paramref name="className"/>, <paramref name="type"/>, <paramref name="attribute"/>),
    /// or <see langword="null"/> when the DLL is unavailable or the member cannot be
    /// resolved.
    /// </summary>
    /// <param name="className">Fully-qualified declaring type name.</param>
    /// <param name="type">
    /// Kind discriminator: <tt>"M"</tt> method, <tt>"P"</tt> property, <tt>"F"</tt> field.
    /// </param>
    /// <param name="attribute">Simple member name (method may include parameter list).</param>
    internal static string? GetReturnTypeName(string className, string type, string attribute)
    {
        ArgumentNullException.ThrowIfNull(className);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(attribute);

        var key = $"{GetCurrentAssemblyPath()}\0{className}\0{type}\0{attribute}";
        if (_returnTypeNameCache.TryGetValue(key, out string? cached))
            return cached;

        var result = type switch
        {
            "P" => GetPropertyTypeNameFromMetadata(className, attribute),
            "F" => GetFieldTypeNameFromMetadata(className, attribute),
            _ => null
        };

        _returnTypeNameCache[key] = result;
        return result;
    }

    private static string? GetPropertyTypeNameFromMetadata(string fullClassName, string propertyName)
    {
        try
        {
            return ReadMetadata(reader =>
            {
                var typeHandle = FindTypeDefinition(reader, fullClassName);
                if (typeHandle.IsNil)
                    return null;

                var typeDefinition = reader.GetTypeDefinition(typeHandle);
                foreach (var propertyHandle in typeDefinition.GetProperties())
                {
                    var property = reader.GetPropertyDefinition(propertyHandle);
                    if (!string.Equals(reader.GetString(property.Name), propertyName, StringComparison.Ordinal))
                        continue;

                    return property.DecodeSignature(TypeNameSignatureProvider.Instance, genericContext: null).ReturnType;
                }

                return null;
            });
        }
        catch
        {
            return null;
        }
    }

    private static string? GetFieldTypeNameFromMetadata(string fullClassName, string fieldName)
    {
        try
        {
            return ReadMetadata(reader =>
            {
                var typeHandle = FindTypeDefinition(reader, fullClassName);
                if (typeHandle.IsNil)
                    return null;

                var typeDefinition = reader.GetTypeDefinition(typeHandle);
                foreach (var fieldHandle in typeDefinition.GetFields())
                {
                    var field = reader.GetFieldDefinition(fieldHandle);
                    if (!string.Equals(reader.GetString(field.Name), fieldName, StringComparison.Ordinal))
                        continue;

                    return field.DecodeSignature(TypeNameSignatureProvider.Instance, genericContext: null);
                }

                return null;
            });
        }
        catch
        {
            return null;
        }
    }

    private static TypeDefinitionHandle FindTypeDefinition(MetadataReader reader, string fullClassName)
    {
        var namespaceName = string.Empty;
        var typeName = fullClassName;
        var lastDotIndex = fullClassName.LastIndexOf('.');
        if (lastDotIndex >= 0)
        {
            namespaceName = fullClassName[..lastDotIndex];
            typeName = fullClassName[(lastDotIndex + 1)..];
        }

        foreach (var handle in reader.TypeDefinitions)
        {
            var definition = reader.GetTypeDefinition(handle);
            if (string.Equals(reader.GetString(definition.Namespace), namespaceName, StringComparison.Ordinal)
                && string.Equals(reader.GetString(definition.Name), typeName, StringComparison.Ordinal))
            {
                return handle;
            }
        }

        return default;
    }

    private static string? ReadMetadata(Func<MetadataReader, string?> read)
    {
        ArgumentNullException.ThrowIfNull(read);

        var assemblyPath = GetCurrentAssemblyPath();
        if (!File.Exists(assemblyPath))
            return null;

        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        if (!peReader.HasMetadata)
            return null;

        return read(peReader.GetMetadataReader());
    }

    private sealed class TypeNameSignatureProvider : ISignatureTypeProvider<string, object?>
    {
        internal static readonly TypeNameSignatureProvider Instance = new();

        public string GetArrayType(string elementType, ArrayShape shape)
        {
            return elementType + "[]";
        }

        public string GetByReferenceType(string elementType)
        {
            return elementType + "@";
        }

        public string GetFunctionPointerType(MethodSignature<string> signature)
        {
            return "System.IntPtr";
        }

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
        {
            return $"{genericType}{{{string.Join(",", typeArguments)}}}";
        }

        public string GetGenericMethodParameter(object? genericContext, int index)
        {
            return $"``{index}";
        }

        public string GetGenericTypeParameter(object? genericContext, int index)
        {
            return $"`{index}";
        }

        public string GetModifiedType(string modifierType, string unmodifiedType, bool isRequired)
        {
            return unmodifiedType;
        }

        public string GetPinnedType(string elementType)
        {
            return elementType;
        }

        public string GetPointerType(string elementType)
        {
            return elementType + "*";
        }

        public string GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            return typeCode switch
            {
                PrimitiveTypeCode.Boolean => "System.Boolean",
                PrimitiveTypeCode.Byte => "System.Byte",
                PrimitiveTypeCode.Char => "System.Char",
                PrimitiveTypeCode.Double => "System.Double",
                PrimitiveTypeCode.Int16 => "System.Int16",
                PrimitiveTypeCode.Int32 => "System.Int32",
                PrimitiveTypeCode.Int64 => "System.Int64",
                PrimitiveTypeCode.IntPtr => "System.IntPtr",
                PrimitiveTypeCode.Object => "System.Object",
                PrimitiveTypeCode.SByte => "System.SByte",
                PrimitiveTypeCode.Single => "System.Single",
                PrimitiveTypeCode.String => "System.String",
                PrimitiveTypeCode.UInt16 => "System.UInt16",
                PrimitiveTypeCode.UInt32 => "System.UInt32",
                PrimitiveTypeCode.UInt64 => "System.UInt64",
                PrimitiveTypeCode.UIntPtr => "System.UIntPtr",
                PrimitiveTypeCode.Void => "System.Void",
                _ => typeCode.ToString()
            };
        }

        public string GetSZArrayType(string elementType)
        {
            return elementType + "[]";
        }

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            var type = reader.GetTypeDefinition(handle);
            return CombineTypeName(reader.GetString(type.Namespace), reader.GetString(type.Name));
        }

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            var type = reader.GetTypeReference(handle);
            return CombineTypeName(reader.GetString(type.Namespace), reader.GetString(type.Name));
        }

        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            var specification = reader.GetTypeSpecification(handle);
            return specification.DecodeSignature(this, genericContext);
        }

        public string GetUnsupportedSignatureTypeKind(byte rawTypeKind)
        {
            return string.Empty;
        }

        private static string CombineTypeName(string namespaceName, string typeName)
        {
            return string.IsNullOrWhiteSpace(namespaceName)
                ? typeName
                : namespaceName + "." + typeName;
        }
    }
}
