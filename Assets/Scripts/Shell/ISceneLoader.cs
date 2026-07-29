using System;

namespace SLG.Shell
{
    public interface ISceneLoader
    {
        bool CanLoad(string sceneName);
        void Load(string sceneName, Action<bool, string> completed);
    }
}
