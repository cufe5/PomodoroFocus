using System;
using System.Collections.Generic;
using System.Reflection;

namespace PomodoroFocus.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            Assembly asm = typeof(Program).Assembly;
            Type[] types = asm.GetTypes();
            Array.Sort(types, delegate(Type a, Type b) { return string.CompareOrdinal(a.Name, b.Name); });

            int passed = 0;
            int failed = 0;
            var failures = new List<string>();

            for (int ti = 0; ti < types.Length; ti++)
            {
                Type t = types[ti];
                if (!t.IsPublic || t.IsAbstract || !t.Name.EndsWith("Tests")) continue;

                MethodInfo[] methods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                Array.Sort(methods, delegate(MethodInfo a, MethodInfo b) { return string.CompareOrdinal(a.Name, b.Name); });

                for (int mi = 0; mi < methods.Length; mi++)
                {
                    MethodInfo m = methods[mi];
                    if (m.GetParameters().Length != 0 || m.ReturnType != typeof(void)) continue;

                    string fullName = t.Name + "." + m.Name;
                    try
                    {
                        object instance = Activator.CreateInstance(t);
                        m.Invoke(instance, null);
                        passed++;
                        Console.WriteLine("[PASS] " + fullName);
                    }
                    catch (TargetInvocationException tie)
                    {
                        failed++;
                        Exception inner = tie.InnerException ?? tie;
                        string msg = "[FAIL] " + fullName + " -> " + inner.GetType().Name + ": " + inner.Message + "\n" + inner.StackTrace;
                        Console.WriteLine(msg);
                        failures.Add(msg);
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        string msg = "[FAIL] " + fullName + " -> " + ex.Message;
                        Console.WriteLine(msg);
                        failures.Add(msg);
                    }
                }
            }

            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine(string.Format("Total: {0} passed, {1} failed.", passed, failed));
            if (failed > 0)
            {
                for (int i = 0; i < failures.Count; i++)
                {
                    Console.WriteLine(failures[i]);
                }
                return 1;
            }
            return 0;
        }
    }
}
