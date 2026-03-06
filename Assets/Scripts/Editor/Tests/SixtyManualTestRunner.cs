#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Sixty.EditorTools
{
    public static class SixtyManualTestRunner
    {
        [MenuItem("Tools/Sixty/Run Unit Tests")]
        public static void RunAllEditModeUnitTests()
        {
            int passed = 0;
            int failed = 0;
            List<string> failures = new List<string>();

            Assembly editorAssembly = typeof(Sixty.Tests.EditMode.TimeManagerTests).Assembly;
            Type[] testTypes = editorAssembly
                .GetTypes()
                .Where(type => type.Namespace == "Sixty.Tests.EditMode")
                .ToArray();

            for (int i = 0; i < testTypes.Length; i++)
            {
                Type type = testTypes[i];
                object instance = Activator.CreateInstance(type);
                MethodInfo setUp = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.GetCustomAttribute<NUnit.Framework.SetUpAttribute>() != null);
                MethodInfo tearDown = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(m => m.GetCustomAttribute<NUnit.Framework.TearDownAttribute>() != null);

                MethodInfo[] testMethods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null)
                    .ToArray();

                for (int j = 0; j < testMethods.Length; j++)
                {
                    MethodInfo testMethod = testMethods[j];
                    string testName = $"{type.Name}.{testMethod.Name}";

                    try
                    {
                        setUp?.Invoke(instance, null);
                        testMethod.Invoke(instance, null);
                        passed++;
                        Debug.Log($"[SixtyTests][PASS] {testName}");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Exception root = Unwrap(ex);
                        failures.Add($"{testName}: {root.Message}");
                        Debug.LogError($"[SixtyTests][FAIL] {testName}\n{root}");
                    }
                    finally
                    {
                        try
                        {
                            tearDown?.Invoke(instance, null);
                        }
                        catch (Exception tearDownException)
                        {
                            Exception root = Unwrap(tearDownException);
                            Debug.LogError($"[SixtyTests][TEARDOWN-FAIL] {testName}\n{root}");
                        }
                    }
                }
            }

            string summary = $"[SixtyTests] Completed. Passed: {passed}, Failed: {failed}";
            if (failed == 0)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError(summary + "\n" + string.Join("\n", failures));
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(failed == 0 ? 0 : 1);
            }
        }

        private static Exception Unwrap(Exception ex)
        {
            Exception current = ex;
            while (current is TargetInvocationException tie && tie.InnerException != null)
            {
                current = tie.InnerException;
            }

            return current;
        }
    }
}
#endif
