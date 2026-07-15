using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConGame.Bootstrap
{

    [DisallowMultipleComponent]
    public sealed class BootstrapSceneController : MonoBehaviour
    {
        [SerializeField] private string menuSceneName = "MainMenu";

        private static BootstrapSceneController _instance;

        public void Configure(string menuScene)
        {
            menuSceneName = menuScene;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (SceneManager.GetActiveScene().name != menuSceneName)
            {
                SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
            }
        }
    }
}
