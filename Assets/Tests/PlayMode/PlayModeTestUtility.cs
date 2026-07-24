using System;
using System.Collections;
using NUnit.Framework;
using SLG.Tests.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SLG.Tests.PlayMode
{
    public static class PlayModeTestUtility
    {
        public static IEnumerator LoadFixture(string sceneName, Action<BattleTestFixture> loaded)
        {
            yield return SceneManager.LoadSceneAsync($"Assets/Scenes/Tests/{sceneName}.unity", LoadSceneMode.Single);

            BattleTestFixture fixture = null;
            yield return WaitUntilOrFail(
                () =>
                {
                    fixture = UnityEngine.Object.FindAnyObjectByType<BattleTestFixture>();
                    return fixture != null && fixture.IsReady;
                },
                5f,
                () => fixture != null ? fixture.DumpState() : $"Fixture not found in scene {sceneName}.");

            Assert.That(fixture.ValidateReferences(out string error), Is.True, error + "\n" + fixture.DumpState());
            loaded(fixture);
        }

        public static IEnumerator WaitUntilOrFail(Func<bool> predicate, float timeoutSeconds, Func<string> diagnostics)
        {
            float start = Time.realtimeSinceStartup;
            while (!predicate())
            {
                if (Time.realtimeSinceStartup - start > timeoutSeconds)
                {
                    Assert.Fail(diagnostics());
                }

                yield return null;
            }
        }
    }
}
