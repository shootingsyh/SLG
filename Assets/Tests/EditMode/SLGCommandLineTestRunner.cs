using System;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace SLG.Tests
{
    public static class SLGCommandLineTestRunner
    {
        private static int exitCode = 1;
        private static bool finished;

        public static void Run()
        {
            string[] args = Environment.GetCommandLineArgs();
            string platform = GetArgument(args, "-slgTestPlatform", "EditMode");
            string results = GetArgument(args, "-slgTestResults", "TestResults/results.xml");
            TestMode mode = string.Equals(platform, "PlayMode", StringComparison.OrdinalIgnoreCase) ? TestMode.PlayMode : TestMode.EditMode;

            Debug.Log($"SLG command-line test run starting: {mode}, results={results}");
            TestRunnerApi api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks(results));
            api.Execute(new ExecutionSettings(new Filter { testMode = mode }));
            EditorApplication.update += ExitWhenFinished;
        }

        private static void ExitWhenFinished()
        {
            if (!finished)
            {
                return;
            }

            EditorApplication.update -= ExitWhenFinished;
            EditorApplication.Exit(exitCode);
        }

        private static string GetArgument(string[] args, string name, string fallback)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return fallback;
        }

        private sealed class Callbacks : ICallbacks
        {
            private readonly string resultsPath;

            public Callbacks(string resultsPath)
            {
                this.resultsPath = resultsPath;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
                Debug.Log($"SLG command-line test tree loaded: {testsToRun?.TestCaseCount ?? 0} test cases.");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result, resultsPath);
                exitCode = result.FailCount > 0 || result.InconclusiveCount > 0 ? 1 : 0;
                finished = true;
                int total = result.PassCount + result.FailCount + result.SkipCount + result.InconclusiveCount;
                Debug.Log($"SLG command-line test run finished: total={total}, passed={result.PassCount}, failed={result.FailCount}, skipped={result.SkipCount}, inconclusive={result.InconclusiveCount}.");
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }
        }
    }
}
