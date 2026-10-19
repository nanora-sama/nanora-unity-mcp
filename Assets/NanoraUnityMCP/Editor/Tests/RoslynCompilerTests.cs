using System;
using NUnit.Framework;
using UnityEngine;
using NanoraUnityMCP.Core;

namespace NanoraUnityMCP.Tests
{
    /// <summary>
    /// Tests for the RoslynCompiler class.
    /// Tests are skipped when Roslyn assemblies are not available (e.g., in CI).
    /// </summary>
    [TestFixture]
    public class RoslynCompilerTests
    {
        [SetUp]
        public void SetUp()
        {
            RoslynCompiler.ClearCache();
        }

        private void SkipIfRoslynNotAvailable()
        {
            if (!RoslynCompiler.IsAvailable)
            {
                Assert.Ignore("Roslyn assemblies not available - skipping test");
            }
        }

        [Test]
        public void IsAvailable_ReturnsConsistentValue()
        {
            // This test always runs to verify the availability check works
            var first = RoslynCompiler.IsAvailable;
            var second = RoslynCompiler.IsAvailable;
            Assert.AreEqual(first, second, "IsAvailable should return consistent values");
        }

        [Test]
        [Category("RequiresEditor")]
        [Explicit("Only runs when Roslyn assemblies are not available")]
        public void CompileAndExecute_WhenRoslynNotAvailable_ThrowsRoslynNotAvailableException()
        {
            if (RoslynCompiler.IsAvailable)
            {
                Assert.Ignore("Roslyn is available - this test is for when it's not");
            }

            var code = @"
public class DynamicScript
{
    public static object Execute() => 42;
}";

            Assert.Throws<RoslynNotAvailableException>(() => RoslynCompiler.CompileAndExecute(code));
        }

        [Test]
        public void CompileAndExecute_SimpleReturn_ReturnsValue()
        {
            SkipIfRoslynNotAvailable();

            var code = @"
public class DynamicScript
{
    public static object Execute()
    {
        return 42;
    }
}";

            var result = RoslynCompiler.CompileAndExecute(code);
            Assert.AreEqual("42", result);
        }

        [Test]
        public void CompileAndExecute_SystemDllTypes_Compile()
        {
            SkipIfRoslynNotAvailable();

            // SortedDictionary, Queue and Regex live in System.dll on Unity's Mono profile,
            // not in mscorlib, so they need that assembly in the reference set.
            var code = @"
using System.Collections.Generic;
using System.Text.RegularExpressions;
public class DynamicScript
{
    public static object Execute()
    {
        var sorted = new SortedDictionary<string, int> { { ""b"", 2 }, { ""a"", 1 } };
        var queue = new Queue<int>();
        queue.Enqueue(3);
        return string.Join("","", sorted.Keys) + queue.Peek() + Regex.Replace(""x1"", @""\d"", ""#"");
    }
}";

            var result = RoslynCompiler.CompileAndExecute(code);
            Assert.AreEqual("a,b3x#", result);
        }

        [Test]
        public void CompileAndExecute_StringReturn_ReturnsString()
        {
            SkipIfRoslynNotAvailable();

            var code = @"
public class DynamicScript
{
    public static object Execute()
    {
        return ""Hello, Roslyn!"";
    }
}";

            var result = RoslynCompiler.CompileAndExecute(code);
            Assert.AreEqual("Hello, Roslyn!", result);
        }

        [Test]
        public void CompileAndExecute_WithUsings_Works()
        {
            SkipIfRoslynNotAvailable();

            var code = @"
using System;
using System.Linq;
using System.Collections.Generic;

public class DynamicScript
{
    public static object Execute()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        return list.Where(x => x > 2).Sum();
    }
}";

            var result = RoslynCompiler.CompileAndExecute(code);
            Assert.AreEqual("12", result);
        }

        [Test]
        public void CompileAndExecute_UnityEngineAccess_Works()
        {
            SkipIfRoslynNotAvailable();

            var code = @"
using UnityEngine;

public class DynamicScript
{
    public static object Execute()
    {
        var v = new Vector3(1, 2, 3);
        return v.magnitude.ToString(""F2"");
    }
}";

            var result = RoslynCompiler.CompileAndExecute(code);
            var magnitude = Mathf.Sqrt(1 + 4 + 9);
            Assert.AreEqual(magnitude.ToString("F2"), result);
        }

        [Test]
        public void CompileAndExecute_CompilationError_ThrowsCompilationException()
        {
            SkipIfRoslynNotAvailable();

            var code = @"
public class DynamicScript
{
    public static object Execute()
    {
        return invalidVariable;  // This will cause a compilation error
    }
}";

            Assert.Throws<CompilationException>(() => RoslynCompiler.CompileAndExecute(code));
        }

        [Test]
        public void CompileAndExecute_RuntimeError_ThrowsExecutionException()
        {
            SkipIfRoslynNotAvailable();

            var code = @"
using System;

public class DynamicScript
{
    public static object Execute()
    {
        throw new InvalidOperationException(""Test exception"");
    }
}";

            var ex = Assert.Throws<ExecutionException>(() => RoslynCompiler.CompileAndExecute(code));
            Assert.That(ex.Message, Does.Contain("Test exception"));
        }

        [Test]
        public void CompileAndExecute_NullReturn_ReturnsNullString()
        {
            SkipIfRoslynNotAvailable();

            var code = @"
public class DynamicScript
{
    public static object Execute()
    {
        return null;
    }
}";

            var result = RoslynCompiler.CompileAndExecute(code);
            Assert.AreEqual("(null)", result);
        }

        [Test]
        public void CompileAndExecute_ListReturn_FormatsAsCollection()
        {
            SkipIfRoslynNotAvailable();

            var code = @"
using System.Collections.Generic;

public class DynamicScript
{
    public static object Execute()
    {
        return new List<string> { ""a"", ""b"", ""c"" };
    }
}";

            var result = RoslynCompiler.CompileAndExecute(code);
            Assert.That(result, Does.Contain("List"));
            Assert.That(result, Does.Contain("a"));
            Assert.That(result, Does.Contain("b"));
            Assert.That(result, Does.Contain("c"));
        }

        [Test]
        public void ClearCache_ResetsReferences()
        {
            SkipIfRoslynNotAvailable();

            // First compilation loads references
            var code = @"
public class DynamicScript
{
    public static object Execute() => 1;
}";
            RoslynCompiler.CompileAndExecute(code);

            // Clear cache
            RoslynCompiler.ClearCache();

            // Should work again after cache clear
            var result = RoslynCompiler.CompileAndExecute(code);
            Assert.AreEqual("1", result);
        }
    }
}
