using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SLG.Shell
{
    public sealed class UnitySceneLoader : ISceneLoader
    {
        public bool CanLoad(string sceneName)
        {
            return Application.CanStreamedLevelBeLoaded(sceneName);
        }

        public void Load(string sceneName, Action<bool, string> completed)
        {
            try
            {
                SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                completed?.Invoke(true, null);
            }
            catch (Exception ex)
            {
                completed?.Invoke(false, ex.Message);
            }
        }
    }
}
