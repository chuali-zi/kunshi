using ConGame.Domain.Persistence;
using ConGame.Runtime.Input;
using ConGame.Runtime.Persistence;
using ConGame.Runtime.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConGame.Bootstrap
{
    public enum MainMenuPage
    {
        Main = 0,
        ConfirmNewGame = 1,
        Settings = 2
    }

    [DisallowMultipleComponent]
    public sealed class SimpleMainMenu : MonoBehaviour
    {
        [SerializeField] private string roomSceneName = "Room_M1";

        private readonly JsonSaveStore _saveStore = new JsonSaveStore();
        private PersistentProgressV1 _progress;
        private PlayerInputRouter _input;
        private GameSettingsStore _settingsStore;
        private SettingsMenuModel _settingsModel;
        private int _selected;

        public MainMenuPage Page { get; private set; }
        public bool CanContinue => CanContinueProgress(_progress);

        public static bool CanContinueProgress(PersistentProgressV1 progress)
        {
            return progress != null && !progress.endingCompleted && progress.loopIndex > 1;
        }

        public void Configure(string roomScene)
        {
            roomSceneName = roomScene;
        }

        public static bool RequiresNewGameConfirmation(PersistentProgressV1 progress)
        {
            return progress != null &&
                   (progress.loopIndex > 1 || progress.darkAdaptationXp > 0 || progress.d21Seen ||
                    progress.tvCrackEchoPending || progress.endingCompleted);
        }

        private void Awake()
        {
            _progress = _saveStore.Load();
            _settingsStore = new GameSettingsStore();
            _settingsModel = new SettingsMenuModel(_settingsStore.Load());
            _input = GetComponent<PlayerInputRouter>();
            if (_input == null)
            {
                _input = gameObject.AddComponent<PlayerInputRouter>();
            }

            _input.ApplySettings(_settingsModel.Settings);
            _input.SetGameplayEnabled(false);
            _selected = 0;
            Page = MainMenuPage.Main;
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Start()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_input == null)
            {
                return;
            }

            Unsubscribe();
            _input.MenuNavigate += OnNavigate;
            _input.MenuSubmitPressed += OnSubmit;
            _input.MenuCancelPressed += OnCancel;
            _input.PausePressed += OnCancel;
        }

        private void Unsubscribe()
        {
            if (_input == null)
            {
                return;
            }

            _input.MenuNavigate -= OnNavigate;
            _input.MenuSubmitPressed -= OnSubmit;
            _input.MenuCancelPressed -= OnCancel;
            _input.PausePressed -= OnCancel;
        }

        private void OnNavigate(Vector2 direction)
        {
            if (Page == MainMenuPage.Settings)
            {
                if (Mathf.Abs(direction.y) >= Mathf.Abs(direction.x) && Mathf.Abs(direction.y) > 0.5f)
                {
                    _settingsModel.MoveSelection(direction.y > 0f ? -1 : 1);
                }
                else if (Mathf.Abs(direction.x) > 0.5f &&
                         _settingsModel.AdjustSelected(direction.x > 0f ? 1 : -1))
                {
                    SaveSettings();
                }

                return;
            }

            if (Mathf.Abs(direction.y) <= 0.5f)
            {
                return;
            }

            if (Page == MainMenuPage.ConfirmNewGame)
            {
                _selected = (_selected + 1) % 2;
                return;
            }

            int step = direction.y > 0f ? -1 : 1;
            do
            {
                _selected = (_selected + step + 4) % 4;
            }
            while (_selected == 1 && !CanContinue);
        }

        private void OnSubmit()
        {
            if (Page == MainMenuPage.Settings)
            {
                if (_settingsModel.SelectedRow == SettingsRow.Back)
                {
                    ReturnToMain();
                }
                else if (_settingsModel.ActivateSelected())
                {
                    SaveSettings();
                }

                return;
            }

            if (Page == MainMenuPage.ConfirmNewGame)
            {
                if (_selected == 0)
                {
                    ReturnToMain();
                }
                else
                {
                    StartNewGame();
                }

                return;
            }

            switch (_selected)
            {
                case 0:
                    RequestNewGame();
                    break;
                case 1:
                    ContinueGame();
                    break;
                case 2:
                    Page = MainMenuPage.Settings;
                    break;
                case 3:
                    Application.Quit();
                    break;
            }
        }

        private void OnCancel()
        {
            if (Page != MainMenuPage.Main)
            {
                ReturnToMain();
            }
        }

        private void RequestNewGame()
        {
            if (RequiresNewGameConfirmation(_progress))
            {
                Page = MainMenuPage.ConfirmNewGame;
                _selected = 0;
                return;
            }

            StartNewGame();
        }

        private void StartNewGame()
        {
            _saveStore.Delete();
            SceneManager.LoadScene(roomSceneName, LoadSceneMode.Single);
        }

        private void ContinueGame()
        {
            if (CanContinue)
            {
                SceneManager.LoadScene(roomSceneName, LoadSceneMode.Single);
            }
        }

        private void ReturnToMain()
        {
            Page = MainMenuPage.Main;
            _selected = 0;
        }

        private void SaveSettings()
        {
            _settingsStore.SaveAtomic(_settingsModel.Settings);
            _input.ApplySettings(_settingsModel.Settings);
        }

        private void OnGUI()
        {
            const float width = 360f;
            const float height = 44f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height * 0.42f;

            GUIStyle title = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 32
            };
            GUI.Label(new Rect(x, y - 100f, width, 60f), "困室 / ConGame", title);

            if (Page == MainMenuPage.Settings)
            {
                if (SettingsOnGuiPanel.Draw(_settingsModel, x, y, width, height, out bool back))
                {
                    SaveSettings();
                }

                if (back)
                {
                    ReturnToMain();
                }

                return;
            }

            if (Page == MainMenuPage.ConfirmNewGame)
            {
                GUI.Label(new Rect(x, y - 44f, width, height),
                    "新游戏会删除现有进度，确定吗？ / Delete current progress?");
                if (GUI.Button(new Rect(x, y + 16f, width, height), Label(0, "取消 / Cancel")))
                {
                    ReturnToMain();
                }

                if (GUI.Button(new Rect(x, y + 72f, width, height), Label(1, "删除并开始 / Delete & Start")))
                {
                    StartNewGame();
                }

                return;
            }

            if (GUI.Button(new Rect(x, y, width, height), Label(0, "新游戏 / New Game")))
            {
                _selected = 0;
                RequestNewGame();
            }

            GUI.enabled = CanContinue;
            if (GUI.Button(new Rect(x, y + 56f, width, height), Label(1, "继续 / Continue")))
            {
                _selected = 1;
                ContinueGame();
            }

            GUI.enabled = true;
            if (GUI.Button(new Rect(x, y + 112f, width, height), Label(2, "设置 / Settings")))
            {
                _selected = 2;
                Page = MainMenuPage.Settings;
            }

            if (GUI.Button(new Rect(x, y + 168f, width, height), Label(3, "退出 / Quit")))
            {
                _selected = 3;
                Application.Quit();
            }
        }

        private string Label(int index, string text)
        {
            return _selected == index ? $"> {text} <" : text;
        }
    }
}
