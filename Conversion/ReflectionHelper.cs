using System.Diagnostics;
using System.Reflection;

namespace ROBdk97.XmlDocToMd.Conversion;

/// <summary>
/// Provides reflection-based type and member information queries for enriching member
/// signatures with resolved type names and visibility information extracted from
/// compiled assemblies.
/// </summary>
internal static class ReflectionHelper
{
    // MIGRATED FROM STATIC: ROBdk97.XmlDocToMd.ReflectionHelper — should move to ConversionContext or a per-conversion scope
    private static readonly Dictionary<string, Assembly> _assemblyCache =
        new(StringComparer.OrdinalIgnoreCase);

    // MIGRATED FROM STATIC: ROBdk97.XmlDocToMd.ReflectionHelper — should move to ConversionContext or a per-conversion scope
    private static readonly Dictionary<string, Type> _typeCache =
        new(StringComparer.OrdinalIgnoreCase);

    // MIGRATED FROM STATIC: ROBdk97.XmlDocToMd.ReflectionHelper — should move to ConversionContext or a per-conversion scope
    private static readonly Dictionary<string, Type?> _returnTypeCache =
        new(StringComparer.OrdinalIgnoreCase);

    // MIGRATED FROM STATIC: ROBdk97.XmlDocToMd.ReflectionHelper — should move to ConversionContext or a per-conversion scope
    private static readonly Dictionary<string, bool> _isPublicCache =
        new(StringComparer.OrdinalIgnoreCase);

    // MIGRATED FROM STATIC: ROBdk97.XmlDocToMd.ReflectionHelper — mirrors ConversionContext.CurrentXmlFile; should be removed once callers pass ConversionContext
    private static string _currentDllPath = string.Empty;

    /// <summary>
    /// Sets the current DLL path to be used for reflection operations. Should be called
    /// before processing each XML file.
    /// </summary>
    internal static void SetCurrentXmlFile(string xmlFilePath)
    {
        ArgumentNullException.ThrowIfNull(xmlFilePath);
        _currentDllPath = xmlFilePath.Replace(".xml", ".dll");
    }

    private static string GetCurrentDllPath() =>
        _currentDllPath;

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
            FieldInfo? field = type.GetField(fieldName);
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
        string dllPath = GetCurrentDllPath();

        if (!_assemblyCache.TryGetValue(dllPath, out var assembly))
        {
            if (File.Exists(dllPath))
            {
                assembly = Assembly.LoadFrom(dllPath);
                _assemblyCache[dllPath] = assembly;
            }
            else
            {
                Console.WriteLine($"DLL file not found: {dllPath}");
                Debug.WriteLine($"DLL file not found: {dllPath}");
                return null;
            }
        }

        string typeCacheKey = $"{dllPath}\0{fullClassName}";
        if (_typeCache.TryGetValue(typeCacheKey, out var cachedType))
            return cachedType;

        var type = GetTypeFromAssembly(fullClassName, assembly);
        if (type is null) return null;
        _typeCache[typeCacheKey] = type;
        return type;
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
            // Attempt to get the type directly
            var type = assembly.GetType(fullClassName);
            if (type != null) return type;

            // Attempt to get the type by removing everything after the last dot
            var nameBeforeLastDot = GetNameBeforeLastDot(fullClassName);
            if (nameBeforeLastDot != null)
            {
                type = assembly.GetType(nameBeforeLastDot);
                if (type != null) return type;
            }

            // Attempt to get the nested type
            type = GetNestedType(fullClassName, assembly);
            if (type != null) return type;

            // At this point, throw an exception if the type is not found
            throw new TypeLoadException($"Type '{fullClassName}' could not be found in the assembly '{assembly.GetName().Name}'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Type '{fullClassName}' could not be found in the assembly '{assembly.GetName().Name}'");
            Debug.WriteLine($"Type '{fullClassName}' could not be found in the assembly '{assembly.GetName().Name}'");
            if (ex is TypeLoadException)
                return null;
            else
                throw;
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
            var property = type.GetProperty(propertyName);
            return property?.PropertyType;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when the member identified by (<paramref name="fullClassName"/>
    /// <paramref name="name"/>, <paramref name="t"/>) is <see langword="public"/>
    /// </summary>
    /// <param name="fullClassName">Declaring type's fully-qualified name.</param>
    /// <param name="name">Simple member name.</param>
    /// <param name="t">
    /// Single-letter kind discriminator: <tt>"T"</tt> type, <tt>"F"</tt> field,
    /// <tt>"P"</tt> property, <tt>"M"</tt> method.
    /// </param>
    private static bool IsPublic(string fullClassName, string name, string t)
    {
        try
        {
            var type = GetTypeT(fullClassName);
            if (type is null)
                return true;
            if (t == "T")
            {
                return type.IsPublic;
            }
            if (t == "F")
            {
                var field = type.GetField(name);
                if (field != null)
                {
                    return field.IsPublic;
                }
            }
            else if (t == "P")
            {
                var property = type.GetProperty(name);
                if (property != null)
                {
                    var getter = property.GetGetMethod();
                    if (getter != null)
                        return getter.IsPublic;
                    var setter = property.GetSetMethod();
                    if (setter != null)
                        return setter.IsPublic;
                }
            }
            else if (t == "M")
            {
                if (name.Contains("#ctor"))
                    return true;
                var parenIndex = name.IndexOf('(');
                var methodName = name;
                if (parenIndex > 0)
                    methodName = name[..parenIndex];
                var method = type.GetMethods().FirstOrDefault(m => m.Name == methodName);
                if (method != null)
                    return method.IsPublic;
            }
            return false;
        }
        catch
        {
            return false;
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
    /// <see langword="true"/> if the member is public; <see langword="false"/> when the
    /// member is non-public, the DLL is unavailable, or the element is <see langword="null"/>.
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

            var cacheKey = $"{GetCurrentDllPath()}\0{nameAttr}";
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
        // Check if the type is T or M or P or F
        if (type != "T" && type != "M" && type != "P" && type != "F")
            return false;
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

        var key = $"{GetCurrentDllPath()}\0{className}\0{type}\0{attribute}";
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
}
