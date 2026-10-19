using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace NanoraUnityMCP.Core
{
    /// <summary>
    /// Provides Roslyn-based C# compilation for dynamic code execution.
    /// Uses reflection to load Roslyn assemblies dynamically, allowing graceful fallback when unavailable.
    /// </summary>
    public static class RoslynCompiler
    {
        private static bool s_Initialized;
        private static bool s_RoslynAvailable;
        private static Type s_CSharpSyntaxTreeType;
        private static Type s_CSharpCompilationType;
        private static Type s_CSharpCompilationOptionsType;
        private static Type s_OutputKindType;
        private static Type s_OptimizationLevelType;
        private static Type s_MetadataReferenceType;
        private static Type s_DiagnosticSeverityType;
        private static List<object> s_CachedReferences;
        private static readonly object s_Lock = new object();

        /// <summary>
        /// Checks if Roslyn is available for compilation.
        /// If not available, attempts to load it.
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
                EnsureInitialized();
                // If still not available, try to reload (e.g., after DLLs are added)
                if (!s_RoslynAvailable && s_RetryCount < k_MaxRetries)
                {
                    lock (s_Lock)
                    {
                        s_RetryCount++;
                        s_Initialized = false;
                    }
                    EnsureInitialized();
                }
                return s_RoslynAvailable;
            }
        }

        private const int k_MaxRetries = 3;
        private static int s_RetryCount = 0;

        /// <summary>
        /// Compiles and executes C# code, returning the result.
        /// </summary>
        /// <param name="code">Complete C# source code with class and method</param>
        /// <param name="className">Name of the class containing the Execute method</param>
        /// <param name="methodName">Name of the static method to invoke</param>
        /// <returns>Result of execution as string</returns>
        public static string CompileAndExecute(string code, string className = "DynamicScript", string methodName = "Execute")
        {
            EnsureInitialized();

            if (!s_RoslynAvailable)
            {
                throw new RoslynNotAvailableException("Roslyn assemblies are not loaded. Falling back to reflection mode.");
            }

            var references = GetAssemblyReferences();

            // CSharpSyntaxTree.ParseText(code)
            // Find the overload that takes string as first parameter
            var parseTextMethod = s_CSharpSyntaxTreeType.GetMethods()
                .Where(m => m.Name == "ParseText" && m.GetParameters().Length > 0 &&
                       m.GetParameters()[0].ParameterType == typeof(string))
                .OrderBy(m => m.GetParameters().Length)
                .FirstOrDefault();

            if (parseTextMethod == null)
            {
                throw new CompilationException("Could not find CSharpSyntaxTree.ParseText(string) method");
            }

            var paramInfo = string.Join(", ", parseTextMethod.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
            Debug.Log($"[RoslynCompiler] ParseText method: {paramInfo}");

            // Build the parameter array based on what the method expects
            var parseParams = parseTextMethod.GetParameters();
            var parseArgs = new object[parseParams.Length];
            parseArgs[0] = code;
            for (int i = 1; i < parseParams.Length; i++)
            {
                var p = parseParams[i];
                if (p.HasDefaultValue)
                    parseArgs[i] = p.DefaultValue;
                else if (p.ParameterType.IsValueType)
                    parseArgs[i] = Activator.CreateInstance(p.ParameterType);
                else
                    parseArgs[i] = null;
            }

            object syntaxTree = parseTextMethod.Invoke(null, parseArgs);
            Debug.Log($"[RoslynCompiler] Parsed syntax tree: {syntaxTree != null}");

            // OutputKind.DynamicallyLinkedLibrary
            var outputKindDll = Enum.Parse(s_OutputKindType, "DynamicallyLinkedLibrary");
            Debug.Log($"[RoslynCompiler] OutputKind: {outputKindDll != null}");

            // OptimizationLevel.Release
            var optimizationRelease = Enum.Parse(s_OptimizationLevelType, "Release");
            Debug.Log($"[RoslynCompiler] OptimizationLevel: {optimizationRelease != null}");

            // new CSharpCompilationOptions(...) - need to find a constructor
            var constructors = s_CSharpCompilationOptionsType.GetConstructors();
            Debug.Log($"[RoslynCompiler] Found {constructors.Length} constructors for CSharpCompilationOptions");

            // Use the constructor with only OutputKind parameter if available, or the one with most params
            var optionsCtor = s_CSharpCompilationOptionsType.GetConstructors()
                .OrderBy(c => c.GetParameters().Length)
                .FirstOrDefault();

            if (optionsCtor == null)
            {
                throw new CompilationException("Could not find CSharpCompilationOptions constructor");
            }

            var ctorParams = optionsCtor.GetParameters();
            var ctorArgs = new object[ctorParams.Length];
            Debug.Log($"[RoslynCompiler] Using constructor with {ctorParams.Length} params");

            for (int i = 0; i < ctorParams.Length; i++)
            {
                var p = ctorParams[i];
                if (p.Name == "outputKind" && p.ParameterType == s_OutputKindType)
                {
                    ctorArgs[i] = outputKindDll;
                }
                else if (p.HasDefaultValue)
                {
                    ctorArgs[i] = p.DefaultValue;
                }
                else if (p.ParameterType.IsValueType)
                {
                    ctorArgs[i] = Activator.CreateInstance(p.ParameterType);
                }
                else
                {
                    ctorArgs[i] = null;
                }
            }

            object options = optionsCtor.Invoke(ctorArgs);
            Debug.Log($"[RoslynCompiler] Options created: {options != null}");

            // Skip additional options for now - the defaults should work
            Debug.Log($"[RoslynCompiler] Compilation options ready");

            // CSharpCompilation.Create(...)
            var createMethod = s_CSharpCompilationType.GetMethods()
                .Where(m => m.Name == "Create" && m.GetParameters().Length == 4)
                .FirstOrDefault();

            if (createMethod == null)
            {
                throw new CompilationException("Could not find CSharpCompilation.Create method");
            }

            Debug.Log($"[RoslynCompiler] Create method found: {createMethod != null}");

            var assemblyName = $"DynamicAssembly_{Guid.NewGuid():N}";
            var syntaxTrees = Array.CreateInstance(syntaxTree.GetType(), 1);
            syntaxTrees.SetValue(syntaxTree, 0);

            Debug.Log($"[RoslynCompiler] About to create compilation with {references.Count} references");

            // Create a typed array of MetadataReference for the Compilation.Create method
            var metadataRefArrayType = s_MetadataReferenceType.MakeArrayType();
            var typedReferences = Array.CreateInstance(s_MetadataReferenceType, references.Count);
            for (int i = 0; i < references.Count; i++)
            {
                typedReferences.SetValue(references[i], i);
            }

            object compilation = createMethod.Invoke(null, new object[] { assemblyName, syntaxTrees, typedReferences, options });
            Debug.Log($"[RoslynCompiler] Compilation created: {compilation != null}");

            // Emit to memory stream
            using (var ms = new MemoryStream())
            {
                var emitMethod = compilation.GetType().GetMethod("Emit", new[] { typeof(Stream) });
                if (emitMethod == null)
                {
                    // Try getting any Emit method
                    emitMethod = compilation.GetType().GetMethods()
                        .Where(m => m.Name == "Emit" && m.GetParameters().Length > 0)
                        .OrderBy(m => m.GetParameters().Length)
                        .FirstOrDefault();
                }
                Debug.Log($"[RoslynCompiler] Emit method found: {emitMethod != null}");

                if (emitMethod == null)
                {
                    throw new CompilationException("Could not find Emit method");
                }

                // Build emit args
                var emitParams = emitMethod.GetParameters();
                var emitArgs = new object[emitParams.Length];
                emitArgs[0] = ms;
                for (int i = 1; i < emitParams.Length; i++)
                {
                    var p = emitParams[i];
                    if (p.HasDefaultValue)
                        emitArgs[i] = p.DefaultValue;
                    else if (p.ParameterType.IsValueType)
                        emitArgs[i] = Activator.CreateInstance(p.ParameterType);
                    else
                        emitArgs[i] = null;
                }

                object emitResult = emitMethod.Invoke(compilation, emitArgs);
                Debug.Log($"[RoslynCompiler] Emit result: {emitResult != null}");

                // Check Success property
                var successProp = emitResult.GetType().GetProperty("Success");
                bool success = (bool)successProp.GetValue(emitResult);

                if (!success)
                {
                    var diagnosticsProp = emitResult.GetType().GetProperty("Diagnostics");
                    var diagnostics = (System.Collections.IEnumerable)diagnosticsProp.GetValue(emitResult);

                    var errors = new List<string>();
                    var errorSeverity = Enum.Parse(s_DiagnosticSeverityType, "Error");

                    foreach (var diag in diagnostics)
                    {
                        var severityProp = diag.GetType().GetProperty("Severity");
                        var severity = severityProp.GetValue(diag);

                        if (severity.Equals(errorSeverity))
                        {
                            errors.Add(FormatDiagnostic(diag, code));
                        }
                    }

                    throw new CompilationException(
                        $"Compilation failed with {errors.Count} error(s):\n" +
                        string.Join("\n", errors)
                    );
                }

                ms.Seek(0, SeekOrigin.Begin);
                var assembly = Assembly.Load(ms.ToArray());

                var type = assembly.GetType(className);
                if (type == null)
                {
                    throw new ExecutionException($"Type '{className}' not found in compiled assembly");
                }

                var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    throw new ExecutionException($"Static method '{methodName}' not found in type '{className}'");
                }

                try
                {
                    var executeResult = method.Invoke(null, null);
                    return FormatResult(executeResult);
                }
                catch (TargetInvocationException ex)
                {
                    throw new ExecutionException($"Runtime error: {ex.InnerException?.Message ?? ex.Message}", ex.InnerException ?? ex);
                }
            }
        }

        private static void EnsureInitialized()
        {
            if (s_Initialized) return;

            lock (s_Lock)
            {
                if (s_Initialized) return;

                try
                {
                    // Try to load Roslyn types from AppDomain first
                    var csharpAsm = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == "Microsoft.CodeAnalysis.CSharp");

                    var codeAnalysisAsm = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == "Microsoft.CodeAnalysis");

                    // If not found, try to load from Plugins folder
                    if (csharpAsm == null || codeAnalysisAsm == null)
                    {
                        Debug.Log("[RoslynCompiler] Roslyn assemblies not in AppDomain, attempting to load from Plugins folder...");
                        TryLoadRoslynFromPlugins(ref codeAnalysisAsm, ref csharpAsm);
                    }

                    if (csharpAsm == null || codeAnalysisAsm == null)
                    {
                        Debug.Log("[RoslynCompiler] Roslyn assemblies not available");
                        s_RoslynAvailable = false;
                        s_Initialized = true;
                        return;
                    }

                    s_CSharpSyntaxTreeType = csharpAsm.GetType("Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree");
                    s_CSharpCompilationType = csharpAsm.GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilation");
                    s_CSharpCompilationOptionsType = csharpAsm.GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions");
                    s_OutputKindType = codeAnalysisAsm.GetType("Microsoft.CodeAnalysis.OutputKind");
                    s_OptimizationLevelType = codeAnalysisAsm.GetType("Microsoft.CodeAnalysis.OptimizationLevel");
                    s_MetadataReferenceType = codeAnalysisAsm.GetType("Microsoft.CodeAnalysis.MetadataReference");
                    s_DiagnosticSeverityType = codeAnalysisAsm.GetType("Microsoft.CodeAnalysis.DiagnosticSeverity");

                    Debug.Log($"[RoslynCompiler] Type loading results:");
                    Debug.Log($"  CSharpSyntaxTree: {s_CSharpSyntaxTreeType != null}");
                    Debug.Log($"  CSharpCompilation: {s_CSharpCompilationType != null}");
                    Debug.Log($"  CSharpCompilationOptions: {s_CSharpCompilationOptionsType != null}");
                    Debug.Log($"  OutputKind: {s_OutputKindType != null}");
                    Debug.Log($"  MetadataReference: {s_MetadataReferenceType != null}");

                    if (s_CSharpSyntaxTreeType == null || s_CSharpCompilationType == null ||
                        s_CSharpCompilationOptionsType == null || s_OutputKindType == null ||
                        s_MetadataReferenceType == null)
                    {
                        Debug.Log("[RoslynCompiler] Required Roslyn types not found");
                        s_RoslynAvailable = false;
                    }
                    else
                    {
                        Debug.Log("[RoslynCompiler] Roslyn initialized successfully");
                        s_RoslynAvailable = true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[RoslynCompiler] Failed to initialize Roslyn: {ex.Message}");
                    s_RoslynAvailable = false;
                }

                s_Initialized = true;
            }
        }

        /// <summary>
        /// Attempts to load Roslyn assemblies from the Plugins folder.
        /// </summary>
        private static void TryLoadRoslynFromPlugins(ref Assembly codeAnalysisAsm, ref Assembly csharpAsm)
        {
            try
            {
                // Find the Plugins folder path
                var pluginsPath = Path.Combine(
                    UnityEngine.Application.dataPath,
                    "NanoraUnityMCP",
                    "Editor",
                    "Plugins"
                );

                if (!Directory.Exists(pluginsPath))
                {
                    Debug.Log($"[RoslynCompiler] Plugins folder not found: {pluginsPath}");
                    return;
                }

                var codeAnalysisPath = Path.Combine(pluginsPath, "Microsoft.CodeAnalysis.dll");
                var csharpPath = Path.Combine(pluginsPath, "Microsoft.CodeAnalysis.CSharp.dll");

                if (!File.Exists(codeAnalysisPath) || !File.Exists(csharpPath))
                {
                    Debug.Log($"[RoslynCompiler] Roslyn DLLs not found in Plugins folder");
                    return;
                }

                // Load the base assembly first
                if (codeAnalysisAsm == null)
                {
                    Debug.Log($"[RoslynCompiler] Loading: {codeAnalysisPath}");
                    codeAnalysisAsm = Assembly.LoadFrom(codeAnalysisPath);
                }

                // Then load the CSharp assembly
                if (csharpAsm == null)
                {
                    Debug.Log($"[RoslynCompiler] Loading: {csharpPath}");
                    csharpAsm = Assembly.LoadFrom(csharpPath);
                }

                Debug.Log($"[RoslynCompiler] Successfully loaded Roslyn from Plugins folder");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RoslynCompiler] Failed to load Roslyn from Plugins: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets all necessary assembly references for compilation.
        /// </summary>
        private static List<object> GetAssemblyReferences()
        {
            lock (s_Lock)
            {
                if (s_CachedReferences != null)
                {
                    return s_CachedReferences;
                }

                var references = new List<object>();
                var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // MetadataReference.CreateFromFile method - find the one with string as first parameter
                var createFromFileMethods = s_MetadataReferenceType.GetMethods()
                    .Where(m => m.Name == "CreateFromFile" && m.GetParameters().Length > 0 &&
                           m.GetParameters()[0].ParameterType == typeof(string))
                    .OrderBy(m => m.GetParameters().Length)
                    .ToList();

                Debug.Log($"[RoslynCompiler] Found {createFromFileMethods.Count} CreateFromFile methods");

                var createFromFileMethod = createFromFileMethods.FirstOrDefault();
                if (createFromFileMethod != null)
                {
                    var paramInfo = string.Join(", ", createFromFileMethod.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    Debug.Log($"[RoslynCompiler] Using CreateFromFile({paramInfo})");
                }

                // Core .NET assemblies
                var coreAssemblies = new[]
                {
                    typeof(object).Assembly,
                    typeof(Console).Assembly,
                    typeof(Enumerable).Assembly,
                    typeof(List<>).Assembly,
                    typeof(System.Text.StringBuilder).Assembly,
                    // System.dll on Unity's Mono profile: SortedDictionary, Queue, Stack, LinkedList, Regex, Uri...
                    // It is skipped by the project-assembly scan below (names starting with "System").
                    typeof(Queue<>).Assembly,
                    typeof(System.Text.RegularExpressions.Regex).Assembly,
                    typeof(Uri).Assembly,
                };

                foreach (var asm in coreAssemblies)
                {
                    TryAddReference(references, addedPaths, asm, createFromFileMethod);
                }

                // Add System.Runtime and other core libs
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var name = asm.GetName().Name;
                    if (name == "System" || name == "System.Core" || name == "System.Runtime" || name == "System.Collections" ||
                        name == "System.Linq" || name == "netstandard")
                    {
                        TryAddReference(references, addedPaths, asm, createFromFileMethod);
                    }
                }

                // Unity assemblies
                TryAddReference(references, addedPaths, typeof(GameObject).Assembly, createFromFileMethod);
                TryAddReference(references, addedPaths, typeof(Vector3).Assembly, createFromFileMethod);

                // UnityEditor
                var editorAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "UnityEditor.CoreModule" || a.GetName().Name == "UnityEditor");
                if (editorAssembly != null)
                {
                    TryAddReference(references, addedPaths, editorAssembly, createFromFileMethod);
                }

                // All loaded UnityEngine/UnityEditor assemblies
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var name = asm.GetName().Name;
                    if (name.StartsWith("UnityEngine") || name.StartsWith("UnityEditor"))
                    {
                        TryAddReference(references, addedPaths, asm, createFromFileMethod);
                    }
                }

                // Project assemblies - include all non-system, non-Unity assemblies
                // This ensures third-party packages with .asmdef files are also referenced
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.IsDynamic) continue;

                    var name = asm.GetName().Name;

                    // Skip assemblies already added above
                    if (name.StartsWith("UnityEngine") || name.StartsWith("UnityEditor")) continue;
                    if (name.StartsWith("System") || name == "mscorlib" || name == "netstandard") continue;
                    if (name.StartsWith("nunit") || name.StartsWith("NUnit")) continue;
                    if (name.StartsWith("Mono.")) continue;

                    TryAddReference(references, addedPaths, asm, createFromFileMethod);
                }

                s_CachedReferences = references;
                Debug.Log($"[RoslynCompiler] Loaded {references.Count} assembly references");

                return references;
            }
        }

        private static void TryAddReference(List<object> references, HashSet<string> addedPaths,
            Assembly assembly, MethodInfo createFromFileMethod)
        {
            try
            {
                if (assembly.IsDynamic)
                    return;

                var location = assembly.Location;
                if (string.IsNullOrEmpty(location))
                    return;

                if (addedPaths.Contains(location))
                    return;

                if (!File.Exists(location))
                    return;

                // Build arguments array matching the method signature
                var parameters = createFromFileMethod.GetParameters();
                var args = new object[parameters.Length];
                args[0] = location;

                // Fill remaining parameters with defaults/nulls
                for (int i = 1; i < parameters.Length; i++)
                {
                    var param = parameters[i];
                    if (param.HasDefaultValue)
                    {
                        args[i] = param.DefaultValue;
                    }
                    else if (param.ParameterType.IsValueType)
                    {
                        args[i] = Activator.CreateInstance(param.ParameterType);
                    }
                    else
                    {
                        args[i] = null;
                    }
                }

                var reference = createFromFileMethod.Invoke(null, args);
                references.Add(reference);
                addedPaths.Add(location);
            }
            catch (TargetInvocationException ex)
            {
                // Only log core assembly failures for debugging
                var name = assembly.GetName().Name;
                if (name == "mscorlib" || name == "System" || name == "System.Core" || name == "UnityEngine")
                {
                    Debug.LogWarning($"[RoslynCompiler] Failed to add core reference for {name}: {ex.InnerException?.Message ?? ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Debug.LogWarning($"[RoslynCompiler] Inner exception type: {ex.InnerException.GetType().FullName}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Only log core assembly failures
                var name = assembly.GetName().Name;
                if (name == "mscorlib" || name == "System" || name == "System.Core" || name == "UnityEngine")
                {
                    Debug.LogWarning($"[RoslynCompiler] Failed to add core reference for {name}: {ex.Message}");
                }
            }
        }

        private static string FormatDiagnostic(object diagnostic, string code)
        {
            try
            {
                var locationProp = diagnostic.GetType().GetProperty("Location");
                var location = locationProp.GetValue(diagnostic);

                var getLineSpanMethod = location.GetType().GetMethod("GetLineSpan");
                var lineSpan = getLineSpanMethod.Invoke(location, null);

                var startProp = lineSpan.GetType().GetProperty("StartLinePosition");
                var startPos = startProp.GetValue(lineSpan);

                var lineProp = startPos.GetType().GetProperty("Line");
                var charProp = startPos.GetType().GetProperty("Character");

                int lineNumber = (int)lineProp.GetValue(startPos) + 1;
                int column = (int)charProp.GetValue(startPos) + 1;

                var getMessageMethod = diagnostic.GetType().GetMethod("GetMessage", new Type[0]);
                string message = (string)getMessageMethod.Invoke(diagnostic, null);

                var lines = code.Split('\n');
                var contextLine = lineNumber <= lines.Length ? lines[lineNumber - 1].TrimEnd() : "";

                return $"  Line {lineNumber}, Col {column}: {message}\n    {contextLine}";
            }
            catch
            {
                return diagnostic.ToString();
            }
        }

        private static string FormatResult(object result)
        {
            if (result == null)
                return "(null)";

            if (result is string str)
                return str;

            if (result is System.Collections.IEnumerable enumerable && !(result is string))
            {
                var items = new List<string>();
                foreach (var item in enumerable)
                {
                    items.Add(item?.ToString() ?? "(null)");
                    if (items.Count >= 100)
                    {
                        items.Add("... (truncated)");
                        break;
                    }
                }
                return $"[{result.GetType().Name}]\n" + string.Join("\n", items);
            }

            // For Unity objects, provide more details
            if (result is UnityEngine.Object unityObj)
            {
                return $"{unityObj.GetType().Name}: {unityObj.name} (ID: {unityObj.GetInstanceID()})";
            }

            return result.ToString();
        }

        /// <summary>
        /// Clears the cached assembly references. Call when assemblies change.
        /// </summary>
        public static void ClearCache()
        {
            lock (s_Lock)
            {
                s_CachedReferences = null;
            }
        }

        /// <summary>
        /// Resets the initialization state to allow re-trying Roslyn loading.
        /// </summary>
        public static void ResetInitialization()
        {
            lock (s_Lock)
            {
                s_Initialized = false;
                s_RoslynAvailable = false;
                s_CachedReferences = null;
                s_CSharpSyntaxTreeType = null;
                s_CSharpCompilationType = null;
                s_CSharpCompilationOptionsType = null;
                s_OutputKindType = null;
                s_OptimizationLevelType = null;
                s_MetadataReferenceType = null;
                s_DiagnosticSeverityType = null;
            }
        }
    }

    /// <summary>
    /// Exception thrown when Roslyn assemblies are not available.
    /// </summary>
    public class RoslynNotAvailableException : Exception
    {
        public RoslynNotAvailableException(string message) : base(message) { }
    }

    /// <summary>
    /// Exception thrown when C# compilation fails.
    /// </summary>
    public class CompilationException : Exception
    {
        public CompilationException(string message) : base(message) { }
    }

    /// <summary>
    /// Exception thrown when compiled code execution fails.
    /// </summary>
    public class ExecutionException : Exception
    {
        public ExecutionException(string message) : base(message) { }
        public ExecutionException(string message, Exception inner) : base(message, inner) { }
    }
}
