using UnityEngine;
using UnityEngine.SceneManagement;

namespace Veilward.Presentation
{
    public sealed class BootstrapLoader : MonoBehaviour
    {
        private const string MainMenuSceneName = "MainMenu";

        private void Start()
        {
            if (!Application.CanStreamedLevelBeLoaded(MainMenuSceneName))
            {
                Debug.LogError($"Bootstrap cannot load scene '{MainMenuSceneName}'. Check Build Settings.");
                return;
            }

            SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
        }
    }
}
