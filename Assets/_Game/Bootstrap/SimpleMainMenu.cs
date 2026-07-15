using ConGame.Domain.Persistence;
using ConGame.Runtime.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConGame.Bootstrap
{

    [DisallowMultipleComponent]
    public sealed class SimpleMainMenu : MonoBehaviour
    {
        [SerializeField] private string roomSceneName = "Room_M1";

        private readonly JsonSaveStore _saveStore = new JsonSaveStore();
        private PersistentProgressV1 _progress;

        public void Configure(string roomScene)
        {
            roomSceneName = roomScene;
        }

        private void Awake()
        {
            _progress = _saveStore.Load();
        }

        private void OnGUI()
        {
            const float width = 240f;
            const float height = 44f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height * 0.55f;

            GUIStyle title = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 32
            };
            GUI.Label(new Rect(x, y - 100f, width, 60f), "困室 / ConGame", title);

            if (GUI.Button(new Rect(x, y, width, height), "新游戏 / New Game"))
            {
                _saveStore.Delete();
                SceneManager.LoadScene(roomSceneName, LoadSceneMode.Single);
            }

            GUI.enabled = _progress != null && !_progress.endingCompleted && _progress.loopIndex > 1;
            if (GUI.Button(new Rect(x, y + 56f, width, height), "继续 / Continue"))
            {
                SceneManager.LoadScene(roomSceneName, LoadSceneMode.Single);
            }

            GUI.enabled = true;
            if (GUI.Button(new Rect(x, y + 112f, width, height), "退出 / Quit"))
            {
                Application.Quit();
            }
        }
    }
}
