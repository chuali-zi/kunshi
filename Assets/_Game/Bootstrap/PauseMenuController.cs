using ConGame.Runtime.Input;
using ConGame.Runtime.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConGame.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private PlayerInputRouter input;
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private GameSettingsStore _settingsStore;
        private SettingsMenuModel _settingsModel;
        private bool _showSettings;
        private int _selected;
        private float _previousTimeScale = 1f;
        private bool _previousAudioPause;
        private bool _gameplayWasEnabled;

        public bool IsPaused { get; private set; }
        public GameSettingsV1 Settings => _settingsModel?.Settings;

        public void Configure(PlayerInputRouter inputRouter, string menuScene = "MainMenu")
        {
            Unsubscribe();
            input = inputRouter;
            mainMenuSceneName = menuScene;
            Subscribe();
            input?.ApplySettings(Settings);
        }

        private void Awake()
        {
            input = input == null ? GetComponent<PlayerInputRouter>() : input;
            _settingsStore = new GameSettingsStore();
            _settingsModel = new SettingsMenuModel(_settingsStore.Load());
            input?.ApplySettings(Settings);
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (IsPaused)
            {
                Resume();
            }
        }

        public void Pause()
        {
            if (IsPaused)
            {
                return;
            }

            IsPaused = true;
            _showSettings = false;
            _selected = 0;
            _previousTimeScale = Time.timeScale;
            _previousAudioPause = AudioListener.pause;
            _gameplayWasEnabled = input != null && input.GameplayEnabled;
            input?.SetGameplayEnabled(false);
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        public void Resume()
        {
            if (!IsPaused)
            {
                return;
            }

            IsPaused = false;
            _showSettings = false;
            Time.timeScale = _previousTimeScale;
            AudioListener.pause = _previousAudioPause;
            input?.SetGameplayEnabled(_gameplayWasEnabled);
        }

        public void ReturnToMainMenu()
        {
            Resume();
            SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
        }

        private void TogglePause()
        {
            if (IsPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }

        private void OnNavigate(Vector2 direction)
        {
            if (!IsPaused)
            {
                return;
            }

            if (_showSettings)
            {
                if (Mathf.Abs(direction.y) >= Mathf.Abs(direction.x) && Mathf.Abs(direction.y) > 0.5f)
                {
                    _settingsModel.MoveSelection(direction.y > 0f ? -1 : 1);
                }
                else if (Mathf.Abs(direction.x) > 0.5f && _settingsModel.AdjustSelected(direction.x > 0f ? 1 : -1))
                {
                    SaveSettings();
                }

                return;
            }

            if (Mathf.Abs(direction.y) > 0.5f)
            {
                _selected = (_selected + (direction.y > 0f ? 2 : 1)) % 3;
            }
        }

        private void OnSubmit()
        {
            if (!IsPaused)
            {
                return;
            }

            if (_showSettings)
            {
                if (_settingsModel.SelectedRow == SettingsRow.Back)
                {
                    _showSettings = false;
                    return;
                }

                if (_settingsModel.ActivateSelected())
                {
                    SaveSettings();
                }

                return;
            }

            if (_selected == 0)
            {
                Resume();
            }
            else if (_selected == 1)
            {
                _showSettings = true;
            }
            else
            {
                ReturnToMainMenu();
            }
        }

        private void OnCancel()
        {
            if (!IsPaused)
            {
                return;
            }

            if (_showSettings)
            {
                _showSettings = false;
            }
            else
            {
                Resume();
            }
        }

        private void SaveSettings()
        {
            _settingsStore.SaveAtomic(Settings);
            input?.ApplySettings(Settings);
        }

        private void Subscribe()
        {
            if (input == null)
            {
                return;
            }

            input.PausePressed -= TogglePause;
            input.MenuNavigate -= OnNavigate;
            input.MenuSubmitPressed -= OnSubmit;
            input.MenuCancelPressed -= OnCancel;
            input.PausePressed += TogglePause;
            input.MenuNavigate += OnNavigate;
            input.MenuSubmitPressed += OnSubmit;
            input.MenuCancelPressed += OnCancel;
        }

        private void Unsubscribe()
        {
            if (input == null)
            {
                return;
            }

            input.PausePressed -= TogglePause;
            input.MenuNavigate -= OnNavigate;
            input.MenuSubmitPressed -= OnSubmit;
            input.MenuCancelPressed -= OnCancel;
        }

        private void OnGUI()
        {
            if (!IsPaused)
            {
                return;
            }

            const float width = 360f;
            const float height = 40f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height * 0.25f;

            GUI.Box(new Rect(x - 30f, y - 70f, width + 60f, 430f), string.Empty);
            if (_showSettings)
            {
                if (SettingsOnGuiPanel.Draw(_settingsModel, x, y, width, height, out bool back))
                {
                    SaveSettings();
                }

                if (back)
                {
                    _showSettings = false;
                }

                return;
            }

            GUI.Label(new Rect(x, y - 52f, width, height), "暂停 / Paused");
            if (GUI.Button(new Rect(x, y, width, height), Label(0, "继续 / Resume")))
            {
                Resume();
            }

            if (GUI.Button(new Rect(x, y + 52f, width, height), Label(1, "设置 / Settings")))
            {
                _selected = 1;
                _showSettings = true;
            }

            if (GUI.Button(new Rect(x, y + 104f, width, height), Label(2, "返回主菜单 / Main Menu")))
            {
                ReturnToMainMenu();
            }
        }

        private string Label(int index, string text)
        {
            return _selected == index ? $"> {text} <" : text;
        }
    }
}
