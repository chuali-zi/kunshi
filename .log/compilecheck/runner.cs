using System;
using System.Linq;
using System.Reflection;

public static class MiniTestRunner
{
    public static int Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom(args[0]);
        int passed = 0, failed = 0;
        foreach (Type type in asm.GetTypes())
        {
            MethodInfo[] tests = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name == "TestAttribute"))
                .ToArray();
            if (tests.Length == 0)
            {
                continue;
            }

            foreach (MethodInfo test in tests)
            {
                object instance = Activator.CreateInstance(type);
                try
                {
                    test.Invoke(instance, null);
                    passed++;
                    Console.WriteLine($"PASS {type.Name}.{test.Name}");
                }
                catch (TargetInvocationException ex)
                {
                    failed++;
                    Exception inner = ex.InnerException ?? ex;
                    Console.WriteLine($"FAIL {type.Name}.{test.Name}: {inner.Message}");
                }
            }
        }

        Console.WriteLine($"== {passed} passed, {failed} failed ==");
        return failed == 0 ? 0 : 1;
    }
}
