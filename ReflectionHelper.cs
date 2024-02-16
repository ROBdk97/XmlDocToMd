using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace ROBdk97.XmlDocToMd
{
    internal static class ReflectionHelper
    {
        private static Assembly _assembly;

        /// <summary>
        /// Get the type of the field in the class of an assembly
        /// </summary>
        /// <param name="fullClassName"></param>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        internal static Type GetFieldType(string fullClassName, string fieldName)
        {
            try
            {
                Type type = GetTypeT(fullClassName);
                FieldInfo field = type?.GetField(fieldName);
                return field?.FieldType ?? null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the type of the method in the class of an assembly
        /// </summary>
        /// <param name="fullClassName"></param>
        /// <param name="methodName"></param>
        /// <returns></returns>
        internal static Type GetMethodReturnType(string fullClassName, string methodName)
        {
            try
            {
                Type type = GetTypeT(fullClassName);
                if (type is null) return null;
                MethodInfo method = type?.GetMethod(methodName);
                if (method == null)
                {
                    //remove the parameters
                    methodName = methodName.Substring(0, methodName.IndexOf('('));
                    method = type?.GetMethods().FirstOrDefault(m => m.Name == methodName);
                }
                return method?.ReturnType ?? null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Type GetTypeT(string fullClassName)
        {
            string dllPath = XmlToMarkdown.CurrentXmlFile.Replace(".xml", ".dll");
            // Check if the assembly is already loaded
            if (_assembly == null || _assembly.GetName().Name != Path.GetFileNameWithoutExtension(dllPath))
            {
                if (File.Exists(dllPath))
                    _assembly = Assembly.LoadFrom(dllPath);
                else
                {
                    Console.WriteLine($"DLL file not found: {dllPath}");
                    Debug.WriteLine($"DLL file not found: {dllPath}");
                    return null;
                }
            }
            return GetTypeFromAssembly(fullClassName);
        }

        /// <summary>
        /// Get the type of the class in the assembly
        /// </summary>
        /// <param name="fullClassName"></param>
        /// <returns></returns>
        private static Type GetTypeFromAssembly(string fullClassName)
        {
            try
            {
                // Attempt to get the type directly
                var type = _assembly.GetType(fullClassName);
                if (type != null) return type;

                // Attempt to get the type by removing everything after the last dot
                type = _assembly.GetType(GetNameBeforeLastDot(fullClassName));
                if (type != null) return type;

                // Attempt to get the nested type
                type = GetNestedType(fullClassName, _assembly);
                if (type != null) return type;

                // At this point, throw an exception if the type is not found
                throw new TypeLoadException($"Type '{fullClassName}' could not be found in the assembly '{_assembly.GetName().Name}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Type '{fullClassName}' could not be found in the assembly '{_assembly.GetName().Name}'");
                Debug.WriteLine($"Type '{fullClassName}' could not be found in the assembly '{_assembly.GetName().Name}'");
                if (ex is TypeLoadException)
                    return null;
                else
                    throw;
            }
        }
        /// <summary>
        /// Get the name of the class before the last dot
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static string GetNameBeforeLastDot(string name)
        {
            var lastDotIndex = name.LastIndexOf('.');
            return lastDotIndex == -1 ? null : name[..lastDotIndex];
        }

        /// <summary>
        /// Get the nested type of the class in the assembly
        /// </summary>
        /// <param name="fullClassName"></param>
        /// <param name="_assembly"></param>
        /// <returns></returns>
        private static Type GetNestedType(string fullClassName, Assembly _assembly)
        {
            var lastDotIndex = fullClassName.LastIndexOf('.');
            if (lastDotIndex == -1) return null;

            var className = fullClassName.Substring(0, lastDotIndex);
            var nestedClassName = fullClassName.Substring(lastDotIndex + 1);

            return _assembly.GetType(className)?.GetNestedType(nestedClassName);
        }



        /// <summary>
        /// Get the type of the property in the class of an assembly
        /// </summary>
        /// <param name="fullClassName"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        private static Type GetPropertyType(string fullClassName, string propertyName)
        {
            try
            {
                Type type = GetTypeT(fullClassName);
                PropertyInfo property = type?.GetProperty(propertyName);
                return property?.PropertyType ?? null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if the Method/Property/Field is public
        /// </summary>
        /// <param name="fullClassName"></param>
        /// <param name="name"></param>
        /// <param name="t"></param>
        /// <returns></returns>        
        private static bool IsPublic(string fullClassName, string name, string t)
        {
            try
            {
                Type type = GetTypeT(fullClassName);
                if (type is null)
                    return true;
                if (t == "T")
                {
                    return type.IsPublic;
                }
                if (t == "F")
                {
                    FieldInfo field = type?.GetField(name);
                    if (field != null)
                    {
                        return field.IsPublic;
                    }
                }
                else if (t == "P")
                {
                    PropertyInfo property = type?.GetProperty(name);
                    if (property != null)
                    {
                        if (property.GetGetMethod() != null)
                            return property.GetGetMethod().IsPublic;
                        if (property.GetSetMethod() != null)
                            return property.GetSetMethod().IsPublic;
                    }
                }
                else if (t == "M")
                {
                    if (name.Contains("#ctor"))
                        return true;
                    MethodInfo method = type?.GetMethod(name);
                    if (name.Contains('('))
                        name = name.Substring(0, name.IndexOf('('));
                    method = type?.GetMethods().FirstOrDefault(m => m.Name == name);
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
        /// Check if the Method/Property/Field is public
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        internal static bool IsPublic(XElement element)
        {
            try
            {
                if (element is null)
                    return false;
                string className = element.Attribute("name")?.Value;
                if (className is null)
                    return false;
                // Get type of the element
                if (!className.Contains(':'))
                    return false;
                string type = className.Split(':')[0];
                // Check if the is T or M or P or F
                if (type != "T" && type != "M" && type != "P" && type != "F")
                    return false;
                className = className.Split(':')[1];
                string attributeName = className;
                if (className.Contains('('))
                    className = className.Split('(')[0];
                // Remove Method name from class name if not T
                if (type != "T" && className.Contains('.'))
                    if (className.Contains(".#"))
                        return true; // Constructor is always public
                    else
                        className = className.Remove(className.LastIndexOf('.'));
                // Remove class name from attribute name
                if (type != "T")
                    attributeName = attributeName.Remove(0, className.Length + 1);
                else
                    attributeName = null;
                return IsPublic(className, attributeName, type);
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static Type GetReturnType(string className, string type, string attribute)
        {
            if (type == "M")
                return GetMethodReturnType(className, attribute);
            else if (type == "P")
                return GetPropertyType(className, attribute);
            else if (type == "F")
                return GetFieldType(className, attribute);
            return null;
        }
    }
}
