using System;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

// Runs [Test] methods of the named fixtures by reflection (no Test Runner, so no save-scene dialog with a dirty scene).
// Skips parameterised tests ([TestCase]), [UnityTest] coroutines and fixture-level setup.
public static class RunTests
{
    public static string Fixtures(string names)
    {
        var sb = new StringBuilder();
        int passed = 0, failed = 0;
        Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "Aipocalypse.Tests.EditMode");
        // "*" = every fixture in the assembly.
        Type[] fixtures = names == "*"
            ? assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && t.GetMethods().Any(m => m.GetCustomAttribute<TestAttribute>() != null)).ToArray()
            : names.Split(',').Select(n => assembly.GetType("Aipocalypse.Tests." + n.Trim(), true)).ToArray();
        foreach (Type type in fixtures)
        {
            MethodInfo setUp = type.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<SetUpAttribute>() != null);
            MethodInfo tearDown = type.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<TearDownAttribute>() != null);
            foreach (MethodInfo test in type.GetMethods())
            {
                if (test.GetCustomAttribute<TestAttribute>() == null || test.GetParameters().Length > 0)
                    continue;
                object fixture = Activator.CreateInstance(type);
                try
                {
                    setUp?.Invoke(fixture, null);
                    test.Invoke(fixture, null);
                    passed++;
                }
                catch (TargetInvocationException e)
                {
                    failed++;
                    sb.AppendLine($"FAIL {type.Name}.{test.Name}: {e.InnerException?.Message}");
                }
                finally
                {
                    tearDown?.Invoke(fixture, null);
                }
            }
        }
        return $"{passed} passed, {failed} failed\n{sb}";
    }
}
