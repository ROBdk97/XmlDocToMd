using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

namespace ROBdk97.XmlDocToMd.Conversion;

/// <summary>
/// Provides reflection-based type and member information queries for enriching member
/// signatures with resolved type names and visibility information extracted from
/// compiled assemblies.
/// </summary>
internal static class ReflectionHelper
{
    #region Fields

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

    /// <summary>
    /// Binding flags used for all member lookups so that public and non-public,
    /// instance and static members are all considered.
    /// </summary>
    private const BindingFlags MemberLookupBindingFlags =
        BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static |
        BindingFlags.FlattenHierarchy;

    #endregion

    #region Assembly / Type Loading

    /// <summary>
    /// Sets the path of the assembly that will be used for all subsequent reflection
    /// operations. Must be called before processing each XML file.
    /// </summary>
    /// <param name="xmlFilePath">
    /// Absolute path to the XML documentation file. The corresponding <c>.dll</c> or
    /// <c>.exe</c> is derived automatically by replacing the extension.
    /// </param>
    internal static void SetCurrentXmlFile(string xmlFilePath)
    {
        ArgumentNullException.ThrowIfNull(xmlFilePath);
        _currentAssemblyPath = ResolveAssemblyPath(xmlFilePath);
    }

    /// <summary>
    /// Returns the assembly path that was last set by <see cref="SetCurrentXmlFile"/>.
    /// </summary>
    private static string GetCurrentAssemblyPath() => _currentAssemblyPath;

    /// <summary>
    /// Resolves the compiled assembly path that corresponds to <paramref name="xmlFilePath"/>
    /// by probing for a same-name <c>.dll</c> first, then <c>.exe</c>.
    /// Falls back to the <c>.dll</c> path even when it does not exist.
    /// </summary>
    private static string ResolveAssemblyPath(string xmlFilePath)
    {
        var dllPath = Path.ChangeExtension(xmlFilePath, ".dll");
        if (File.Exists(dllPath))
            return dllPath;

        var exePath = Path.ChangeExtension(xmlFilePath, ".exe");
        if (File.Exists(exePath))
            return exePath;

        return dllPath;
    }

    /// <summary>
    /// Resolves <paramref name="fullClassName"/> to a <see cref="Type"/>, loading the
    /// assembly from disk on first access and caching both the assembly and the type.
    /// </summary>
    /// <param name="fullClassName">Fully-qualified class name to resolve.</param>
    /// <returns>The resolved <see cref="Type"/>, or <see langword="null"/> on failure.</returns>
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

    /// <summary>
    /// Eagerly loads assemblies that are referenced by <paramref name="assembly"/> and
    /// present in the same directory, so that type resolution works for cross-assembly
    /// types without relying on the default load context.
    /// </summary>
    /// <param name="assembly">The assembly whose references should be loaded.</param>
    /// <param name="assemblyDirectory">
    /// Directory to probe for referenced assemblies. Silently skipped when
    /// <see langword="null"/> or non-existent.
    /// </param>
    private static void LoadReferencedAssemblies(Assembly assembly, string? assemblyDirectory)
    {
        if (string.IsNullOrWhiteSpace(assemblyDirectory) || !Directory.Exists(assemblyDirectory))
            return;

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
                    continue;

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

            return GetNestedType(fullClassName, assembly);
        }
        catch (TypeLoadException)
        {
            return null;
        }
    }

    /// <summary>
    /// Returns the portion of <paramref name="name"/> before its last <c>.</c>,
    /// or <see langword="null"/> if no dot is present.
    /// </summary>
    private static string? GetNameBeforeLastDot(string name)
    {
        int lastDotIndex = name.LastIndexOf('.');
        return lastDotIndex == -1 ? null : name[..lastDotIndex];
    }

    /// <summary>
    /// Resolves a nested type by splitting <paramref name="fullClassName"/> at the last
    /// dot and calling <see cref="Type.GetNestedType(string)"/>.
    /// </summary>
    /// <param name="fullClassName">Fully-qualified name that may refer to a nested type.</param>
    /// <param name="assembly">The assembly to search within.</param>
    /// <returns>The nested <see cref="Type"/>, or <see langword="null"/> if not found.</returns>
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
    /// Walks the inheritance chain of <paramref name="type"/> to find a field named
    /// <paramref name="fieldName"/>, including non-public and static fields.
    /// </summary>
    /// <param name="type">The type to start the search from.</param>
    /// <param name="fieldName">Simple field name without qualification.</param>
    /// <returns>The <see cref="FieldInfo"/>, or <see langword="null"/> if not found.</returns>
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

    /// <summary>
    /// Walks the inheritance chain and interfaces of <paramref name="type"/> to find a
    /// property named <paramref name="propertyName"/>.
    /// </summary>
    /// <param name="type">The type to start the search from.</param>
    /// <param name="propertyName">Simple property name without qualification.</param>
    /// <returns>The <see cref="PropertyInfo"/>, or <see langword="null"/> if not found.</returns>
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

    #endregion

    #region Public Member Queries

    /// <summary>
    /// Returns the <see cref="Type"/> of the field <paramref name="fieldName"/> declared
    /// on <paramref name="fullClassName"/>, or <see langword="null"/> if resolution fails.
    /// </summary>
    /// <param name="fullClassName">Fully-qualified class name (e.g. <c>MyNs.MyClass</c>).</param>
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
    /// Method name, optionally including a parameter list (e.g. <c>DoWork(System.String)</c>).
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
    /// Returns the return/value <see cref="Type"/> for the member described by
    /// (<paramref name="className"/>, <paramref name="type"/>, <paramref name="attribute"/>),
    /// or <see langword="null"/> when the DLL is unavailable or the member cannot be resolved.
    /// </summary>
    /// <param name="className">Fully-qualified declaring type name.</param>
    /// <param name="type">
    /// Kind discriminator: <c>"M"</c> method, <c>"P"</c> property, <c>"F"</c> field.
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
    /// or <see langword="null"/> when the DLL is unavailable or the member cannot be resolved.
    /// </summary>
    /// <param name="className">Fully-qualified declaring type name.</param>
    /// <param name="type">
    /// Kind discriminator: <c>"M"</c> method, <c>"P"</c> property, <c>"F"</c> field.
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

    #endregion

    #region Visibility

    /// <summary>
    /// Returns <see langword="true"/> when the member described by the <c>name</c> attribute
    /// of <paramref name="element"/> is publicly visible in the compiled assembly.
    /// </summary>
    /// <param name="element">
    /// An XML <c>&lt;member&gt;</c> element whose <c>name</c> attribute follows the
    /// standard <c>T:Ns.Class</c> / <c>M:Ns.Class.Method</c> format.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the member is public or its visibility cannot be
    /// resolved; <see langword="false"/> only when the member is confirmed to be
    /// non-public or the element is <see langword="null"/>.
    /// </returns>
    /// <note>
    /// Results are cached by <c>dllPath\0nameAttr</c> so repeated calls for the same
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

    /// <summary>
    /// Determines the actual public visibility of the member identified by
    /// <paramref name="nameAttr"/> by reflecting over the compiled assembly.
    /// Constructors are treated as public by convention.
    /// </summary>
    /// <param name="nameAttr">
    /// Raw XML doc <c>name</c> attribute value, e.g. <c>T:MyNs.MyClass</c> or
    /// <c>M:MyNs.MyClass.DoWork(System.String)</c>.
    /// </param>
    private static bool ComputeIsPublic(string nameAttr)
    {
        var type = nameAttr.Split(':')[0];
        if (type != "T" && type != "M" && type != "P" && type != "F" && type != "E")
            return true;

        var className = nameAttr.Split(':')[1];
        var attributeName = className;
        if (className.Contains('('))
            className = className.Split('(')[0];

        if (type != "T" && className.Contains('.'))
        {
            if (className.Contains(".#"))
            {
                // Constructor — the declaring type must be public.
                var declaringClass = className[..className.LastIndexOf(".#")];
                return IsPublic(declaringClass, string.Empty, "T");
            }
            else
            {
                className = className[..className.LastIndexOf('.')];
            }
        }

        attributeName = type != "T" ? attributeName[(className.Length + 1)..] : null;

        // For members, also verify the declaring type is publicly visible.
        if (type != "T" && !IsPublic(className, string.Empty, "T"))
            return false;

        return IsPublic(className, attributeName ?? string.Empty, type);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the member identified by
    /// (<paramref name="fullClassName"/>, <paramref name="name"/>, <paramref name="t"/>)
    /// is <see langword="public"/>.
    /// </summary>
    /// <param name="fullClassName">Declaring type's fully-qualified name.</param>
    /// <param name="name">Simple member name.</param>
    /// <param name="t">
    /// Single-letter kind discriminator: <c>"T"</c> type, <c>"F"</c> field,
    /// <c>"P"</c> property, <c>"M"</c> method, <c>"E"</c> event.
    /// </param>
    private static bool IsPublic(string fullClassName, string name, string t)
    {
        try
        {
            const BindingFlags bindingFlags =
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static;

            var type = GetTypeT(fullClassName);
            if (type is null)
                return true;

            return t switch
            {
                "T" => type.IsVisible,
                "F" => type.GetField(name, bindingFlags)?.IsPublic ?? true,
                "P" => IsPropertyPublic(type, name, bindingFlags),
                "M" => IsMethodPublic(type, name, bindingFlags),
                "E" => IsEventPublic(type, name, bindingFlags),
                _ => true
            };
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when the property <paramref name="name"/> on
    /// <paramref name="type"/> has a public getter or setter.
    /// Defaults to <see langword="true"/> when the property cannot be found.
    /// </summary>
    private static bool IsPropertyPublic(Type type, string name, BindingFlags bindingFlags)
    {
        var property = type.GetProperty(name, bindingFlags);
        if (property is null)
            return true;

        var getter = property.GetGetMethod(true);
        if (getter != null)
            return getter.IsPublic;

        var setter = property.GetSetMethod(true);
        return setter?.IsPublic ?? true;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the method <paramref name="name"/> on
    /// <paramref name="type"/> is public.
    /// Constructors (<c>#ctor</c>) are always treated as public.
    /// Defaults to <see langword="true"/> when the method cannot be found.
    /// </summary>
    private static bool IsMethodPublic(Type type, string name, BindingFlags bindingFlags)
    {
        if (name.Contains("#ctor"))
            return true;

        var parenIndex = name.IndexOf('(');
        var methodName = parenIndex > 0 ? name[..parenIndex] : name;
        return type.GetMethods(bindingFlags).FirstOrDefault(m => m.Name == methodName)?.IsPublic ?? true;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the event <paramref name="name"/> on
    /// <paramref name="type"/> has at least one public accessor.
    /// Defaults to <see langword="true"/> when the event cannot be found.
    /// </summary>
    private static bool IsEventPublic(Type type, string name, BindingFlags bindingFlags)
    {
        var eventInfo = type.GetEvent(name, bindingFlags);
        if (eventInfo is null)
            return true;

        return eventInfo.AddMethod?.IsPublic == true
            || eventInfo.RemoveMethod?.IsPublic == true
            || eventInfo.RaiseMethod?.IsPublic == true;
    }

    #endregion

    #region Metadata (PEReader)

    /// <summary>
    /// Reads the PE metadata of the current assembly and invokes <paramref name="read"/>
    /// with the resulting <see cref="MetadataReader"/>.
    /// Returns <see langword="null"/> when the assembly file does not exist or contains
    /// no metadata.
    /// </summary>
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

    /// <summary>
    /// Searches the metadata for a <see cref="TypeDefinitionHandle"/> that matches
    /// <paramref name="fullClassName"/> by namespace and simple type name.
    /// </summary>
    /// <param name="reader">The <see cref="MetadataReader"/> for the current assembly.</param>
    /// <param name="fullClassName">Fully-qualified class name to look up.</param>
    /// <returns>
    /// The matching handle, or a nil <see cref="TypeDefinitionHandle"/> if not found.
    /// </returns>
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

    /// <summary>
    /// Reads the declared type name of property <paramref name="propertyName"/> on
    /// <paramref name="fullClassName"/> directly from the PE metadata.
    /// Returns <see langword="null"/> when the property cannot be found.
    /// </summary>
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

    /// <summary>
    /// Reads the declared type name of field <paramref name="fieldName"/> on
    /// <paramref name="fullClassName"/> directly from the PE metadata.
    /// Returns <see langword="null"/> when the field cannot be found.
    /// </summary>
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

    /// <summary>
    /// <see cref="ISignatureTypeProvider{TType,TGenericContext}"/> implementation that
    /// decodes PE metadata type signatures into their fully-qualified string representation.
    /// </summary>
    private sealed class TypeNameSignatureProvider : ISignatureTypeProvider<string, object?>
    {
        internal static readonly TypeNameSignatureProvider Instance = new();

        /// <inheritdoc/>
        public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[]";

        /// <inheritdoc/>
        public string GetByReferenceType(string elementType) => elementType + "@";

        /// <inheritdoc/>
        public string GetFunctionPointerType(MethodSignature<string> signature) => "System.IntPtr";

        /// <inheritdoc/>
        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
            => $"{genericType}{{{string.Join(",", typeArguments)}}}";

        /// <inheritdoc/>
        public string GetGenericMethodParameter(object? genericContext, int index) => $"``{index}";

        /// <inheritdoc/>
        public string GetGenericTypeParameter(object? genericContext, int index) => $"`{index}";

        /// <inheritdoc/>
        public string GetModifiedType(string modifierType, string unmodifiedType, bool isRequired) => unmodifiedType;

        /// <inheritdoc/>
        public string GetPinnedType(string elementType) => elementType;

        /// <inheritdoc/>
        public string GetPointerType(string elementType) => elementType + "*";

        /// <inheritdoc/>
        public string GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            return typeCode switch
            {
                PrimitiveTypeCode.Boolean => "System.Boolean",
                PrimitiveTypeCode.Byte    => "System.Byte",
                PrimitiveTypeCode.Char    => "System.Char",
                PrimitiveTypeCode.Double  => "System.Double",
                PrimitiveTypeCode.Int16   => "System.Int16",
                PrimitiveTypeCode.Int32   => "System.Int32",
                PrimitiveTypeCode.Int64   => "System.Int64",
                PrimitiveTypeCode.IntPtr  => "System.IntPtr",
                PrimitiveTypeCode.Object  => "System.Object",
                PrimitiveTypeCode.SByte   => "System.SByte",
                PrimitiveTypeCode.Single  => "System.Single",
                PrimitiveTypeCode.String  => "System.String",
                PrimitiveTypeCode.UInt16  => "System.UInt16",
                PrimitiveTypeCode.UInt32  => "System.UInt32",
                PrimitiveTypeCode.UInt64  => "System.UInt64",
                PrimitiveTypeCode.UIntPtr => "System.UIntPtr",
                PrimitiveTypeCode.Void    => "System.Void",
                _                         => typeCode.ToString()
            };
        }

        /// <inheritdoc/>
        public string GetSZArrayType(string elementType) => elementType + "[]";

        /// <inheritdoc/>
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            var type = reader.GetTypeDefinition(handle);
            return CombineTypeName(reader.GetString(type.Namespace), reader.GetString(type.Name));
        }

        /// <inheritdoc/>
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            var type = reader.GetTypeReference(handle);
            return CombineTypeName(reader.GetString(type.Namespace), reader.GetString(type.Name));
        }

        /// <inheritdoc/>
        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            var specification = reader.GetTypeSpecification(handle);
            return specification.DecodeSignature(this, genericContext);
        }

        /// <inheritdoc/>
        public string GetUnsupportedSignatureTypeKind(byte rawTypeKind) => string.Empty;

        private static string CombineTypeName(string namespaceName, string typeName)
            => string.IsNullOrWhiteSpace(namespaceName) ? typeName : namespaceName + "." + typeName;
    }

    #endregion

    #region Source File Discovery

    private static readonly Dictionary<string, string?> _sourceFileCache =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string[]> _repositorySourceFilesCache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Locates the source file that declares <paramref name="fullTypeName"/> within
    /// <paramref name="repositoryRootPath"/> by scoring candidate <c>.cs</c> files.
    /// Results are cached so repeated lookups within one session are free.
    /// </summary>
    /// <param name="fullTypeName">
    /// Fully-qualified type name (e.g. <c>MyNs.MyClass</c>). A leading <c>T:</c> prefix
    /// is stripped automatically.
    /// </param>
    /// <param name="repositoryRootPath">
    /// Absolute path to the repository root. <c>bin</c>, <c>obj</c>, and <c>.git</c>
    /// directories are excluded from the search.
    /// </param>
    /// <returns>
    /// Absolute path to the best-matching source file, or <see langword="null"/> when
    /// no suitable file is found.
    /// </returns>
    internal static string? FindSourceFile(string fullTypeName, string repositoryRootPath)
    {
        ArgumentNullException.ThrowIfNull(fullTypeName);
        ArgumentNullException.ThrowIfNull(repositoryRootPath);

        if (string.IsNullOrWhiteSpace(repositoryRootPath) || !Directory.Exists(repositoryRootPath))
            return null;

        var normalizedTypeName = NormalizeTypeName(fullTypeName);
        if (string.IsNullOrWhiteSpace(normalizedTypeName))
            return null;

        var cacheKey = repositoryRootPath + "\0" + normalizedTypeName;
        if (_sourceFileCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var sourceFiles = GetRepositorySourceFiles(repositoryRootPath);
        var typeSegments = normalizedTypeName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (typeSegments.Length == 0)
            return null;

        var candidateTypeNames = typeSegments.TakeLast(Math.Min(2, typeSegments.Length)).ToArray();

        // Prefer files whose name already matches a segment of the type name.
        var preferredFiles = sourceFiles
            .Where(file => candidateTypeNames.Any(typeName =>
                string.Equals(Path.GetFileNameWithoutExtension(file), typeName, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        var bestMatch = FindBestSourceFileMatch(preferredFiles, normalizedTypeName, typeSegments)
            ?? FindBestSourceFileMatch(sourceFiles, normalizedTypeName, typeSegments);

        _sourceFileCache[cacheKey] = bestMatch;
        return bestMatch;
    }

    /// <summary>
    /// Returns all <c>.cs</c> files under <paramref name="repositoryRootPath"/>, excluding
    /// <c>bin</c>, <c>obj</c>, and <c>.git</c> directories. Results are cached per root.
    /// </summary>
    private static string[] GetRepositorySourceFiles(string repositoryRootPath)
    {
        if (_repositorySourceFilesCache.TryGetValue(repositoryRootPath, out var cached))
            return cached;

        var files = Directory.EnumerateFiles(repositoryRootPath, "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsIgnoredSourceDirectory(file, repositoryRootPath))
            .ToArray();

        _repositorySourceFilesCache[repositoryRootPath] = files;
        return files;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="filePath"/> is located inside a
    /// directory that should be excluded from source-file discovery (<c>bin</c>, <c>obj</c>,
    /// or <c>.git</c>).
    /// </summary>
    private static bool IsIgnoredSourceDirectory(string filePath, string repositoryRootPath)
    {
        var relativePath = Path.GetRelativePath(repositoryRootPath, filePath);
        var parts = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        return parts.Any(part =>
            part.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            part.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
            part.Equals(".git", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Iterates <paramref name="files"/> and returns the one with the highest score
    /// according to <see cref="ScoreSourceFile"/>, or <see langword="null"/> when no
    /// file scores above zero.
    /// </summary>
    private static string? FindBestSourceFileMatch(
        IEnumerable<string> files, string normalizedTypeName, string[] typeSegments)
    {
        string? bestFile = null;
        var bestScore = 0;

        foreach (var file in files)
        {
            int score;
            try
            {
                score = ScoreSourceFile(file, normalizedTypeName, typeSegments);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestFile = file;
        }

        return bestFile;
    }

    /// <summary>
    /// Scores <paramref name="filePath"/> as a candidate source file for the type
    /// described by <paramref name="normalizedTypeName"/> / <paramref name="typeSegments"/>.
    /// </summary>
    /// <returns>
    /// A positive score for plausible matches, or <c>0</c> when the file is an
    /// auto-generated file or contains no type declaration for the candidate names.
    /// Higher scores indicate a stronger match.
    /// </returns>
    private static int ScoreSourceFile(string filePath, string normalizedTypeName, string[] typeSegments)
    {
        var content = File.ReadAllText(filePath);

        // Only skip files that carry the canonical generated-file header comment.
        if (Regex.IsMatch(content, @"(//|/\*)\s*<auto-generated", RegexOptions.CultureInvariant))
            return 0;

        var candidateTypeNames = typeSegments.TakeLast(Math.Min(2, typeSegments.Length)).ToArray();
        var declarationScore = candidateTypeNames.Sum(typeName => ContainsTypeDeclaration(content, typeName) ? 100 : 0);
        if (declarationScore == 0)
            return 0;

        var score = declarationScore;
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        if (string.Equals(fileName, candidateTypeNames[^1], StringComparison.OrdinalIgnoreCase))
            score += 40;
        if (candidateTypeNames.Length > 1 && string.Equals(fileName, candidateTypeNames[0], StringComparison.OrdinalIgnoreCase))
            score += 20;

        var namespacePrefixes = GetNamespacePrefixes(typeSegments);
        var matchingNamespaceDepth = namespacePrefixes
            .Where(prefix => ContainsNamespaceDeclaration(content, prefix))
            .Select(prefix => prefix.Count(c => c == '.') + 1)
            .DefaultIfEmpty()
            .Max();

        score += matchingNamespaceDepth * 10;

        if (ContainsTypeDeclaration(content, normalizedTypeName.Split('.').Last()))
            score += 10;

        return score;
    }

    /// <summary>
    /// Yields all namespace prefix strings that can be formed from the leading segments
    /// of <paramref name="typeSegments"/>.
    /// For example, <c>["MyNs", "Sub", "MyClass"]</c> yields <c>"MyNs"</c> then
    /// <c>"MyNs.Sub"</c>.
    /// </summary>
    private static IEnumerable<string> GetNamespacePrefixes(string[] typeSegments)
    {
        for (var i = 1; i < typeSegments.Length; i++)
            yield return string.Join('.', typeSegments.Take(i));
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="content"/> contains a
    /// <c>namespace</c> declaration that exactly matches <paramref name="namespaceName"/>.
    /// </summary>
    private static bool ContainsNamespaceDeclaration(string content, string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
            return false;

        return Regex.IsMatch(
            content,
            $@"\bnamespace\s+{Regex.Escape(namespaceName)}\b",
            RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="content"/> contains a type
    /// declaration (<c>class</c>, <c>struct</c>, <c>interface</c>, <c>enum</c>, or
    /// <c>record</c>) for <paramref name="typeName"/>, allowing arbitrary access
    /// modifiers between the keyword and the name.
    /// </summary>
    private static bool ContainsTypeDeclaration(string content, string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return false;

        return Regex.IsMatch(
            content,
            $@"\b(class|struct|interface|enum)(\s+\w+)*\s+{Regex.Escape(typeName)}\b|\brecord(?:\s+class|\s+struct)?\s+{Regex.Escape(typeName)}\b",
            RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Strips XML doc prefixes (<c>T:</c>), nested-type separators (<c>+</c>),
    /// by-ref/nullable/pointer suffixes, array brackets, and generic arity
    /// annotations from <paramref name="fullTypeName"/> to produce a clean
    /// dot-separated name.
    /// </summary>
    private static string NormalizeTypeName(string fullTypeName)
    {
        var normalized = fullTypeName.Trim();
        if (normalized.StartsWith("T:", StringComparison.Ordinal))
            normalized = normalized[2..];

        normalized = normalized.Replace('+', '.');
        normalized = normalized.TrimEnd('@', '?', '*');
        while (normalized.EndsWith("[]", StringComparison.Ordinal))
            normalized = normalized[..^2];

        var genericStart = normalized.IndexOf('{');
        if (genericStart >= 0)
            normalized = normalized[..genericStart];

        normalized = Regex.Replace(normalized, "`\\d+", string.Empty, RegexOptions.CultureInvariant);
        return normalized;
    }

    #endregion
}
