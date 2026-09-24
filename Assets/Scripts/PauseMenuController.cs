using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    private const string SensitivityPreferenceKey = "MouseSensitivity";
    private static PauseMenuController activeInstance;

    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private Button tutorialButton;
    [SerializeField] private Button roomButton;
    [SerializeField] private Toggle easyToggle;
    [SerializeField] private Toggle mediumToggle;
    [SerializeField] private Toggle hardToggle;
    [SerializeField] private PlayerController playerController;

    private bool isOpen;
    private Coroutine cursorCaptureRoutine;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = GetComponentInParent<PlayerController>();
        }

        if (pauseMenuPanel == null)
        {
            Transform panelTransform = transform.Find("PauseMenuPanel");
            pauseMenuPanel = panelTransform != null ? panelTransform.gameObject : null;
        }

        if (sensitivitySlider == null && pauseMenuPanel != null)
        {
            sensitivitySlider = pauseMenuPanel.GetComponentInChildren<Slider>(true);
        }

        if (tutorialButton == null)
        {
            tutorialButton = FindButton("TutorialButton");
        }

        if (roomButton == null)
        {
            roomButton = FindButton("RoomButton");
        }

        if (easyToggle == null)
        {
            easyToggle = FindToggle("Toggle-Easy");
        }

        if (mediumToggle == null)
        {
            mediumToggle = FindToggle("Toggle-Medium");
        }

        if (hardToggle == null)
        {
            hardToggle = FindToggle("Toggle-Hard");
        }

        if (playerController == null || pauseMenuPanel == null || sensitivitySlider == null)
        {
            Debug.LogError("Pause menu is missing its panel, sensitivity slider, or PlayerController reference.", this);
            enabled = false;
            return;
        }

        if (activeInstance != null && activeInstance != this)
        {
            if (HasSceneButtons && !activeInstance.HasSceneButtons)
            {
                activeInstance.pauseMenuPanel.SetActive(false);
                activeInstance.enabled = false;
                activeInstance = this;
            }
            else
            {
                pauseMenuPanel.SetActive(false);
                enabled = false;
                return;
            }
        }
        else
        {
            activeInstance = this;
        }

        float savedSensitivity = PlayerPrefs.GetFloat(
            SensitivityPreferenceKey,
            playerController.sensitivity);
        float initialSensitivity = Mathf.Clamp(
            savedSensitivity,
            sensitivitySlider.minValue,
            sensitivitySlider.maxValue);

        sensitivitySlider.SetValueWithoutNotify(initialSensitivity);
        playerController.sensitivity = initialSensitivity;
        sensitivitySlider.onValueChanged.AddListener(SetSensitivity);

        InitializeDifficultyToggles();

        if (tutorialButton != null)
        {
            tutorialButton.onClick.AddListener(LoadTutorial);
        }
        else
        {
            Debug.LogWarning("Pause menu could not find TutorialButton.", this);
        }

        if (roomButton != null)
        {
            roomButton.onClick.AddListener(LoadRoomTest);
        }
        else
        {
            Debug.LogWarning("Pause menu could not find RoomButton.", this);
        }

        pauseMenuPanel.SetActive(false);
    }

    private bool HasSceneButtons => tutorialButton != null && roomButton != null;

    private Button FindButton(string objectName)
    {
        if (pauseMenuPanel == null)
        {
            return null;
        }

        Button[] buttons = pauseMenuPanel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].name == objectName)
            {
                return buttons[i];
            }
        }

        return null;
    }

    private Toggle FindToggle(string objectName)
    {
        if (pauseMenuPanel == null)
        {
            return null;
        }

        Toggle[] toggles = pauseMenuPanel.GetComponentsInChildren<Toggle>(true);
        for (int i = 0; i < toggles.Length; i++)
        {
            if (toggles[i].name == objectName)
            {
                return toggles[i];
            }
        }

        return null;
    }

    private void InitializeDifficultyToggles()
    {
        RoomDifficulty selectedDifficulty = RoomDifficultySelection.Get();

        if (easyToggle != null)
        {
            easyToggle.SetIsOnWithoutNotify(selectedDifficulty == RoomDifficulty.Easy);
            easyToggle.onValueChanged.AddListener(OnEasyToggleChanged);
        }

        if (mediumToggle != null)
        {
            mediumToggle.SetIsOnWithoutNotify(selectedDifficulty == RoomDifficulty.Medium);
            mediumToggle.onValueChanged.AddListener(OnMediumToggleChanged);
        }

        if (hardToggle != null)
        {
            hardToggle.SetIsOnWithoutNotify(selectedDifficulty == RoomDifficulty.Hard);
            hardToggle.onValueChanged.AddListener(OnHardToggleChanged);
        }

        if (easyToggle == null || mediumToggle == null || hardToggle == null)
        {
            Debug.LogWarning("Pause menu could not find all three difficulty toggles.", this);
        }
    }

    private void OnEasyToggleChanged(bool isOn)
    {
        if (isOn)
        {
            RoomDifficultySelection.Set(RoomDifficulty.Easy);
        }
    }

    private void OnMediumToggleChanged(bool isOn)
    {
        if (isOn)
        {
            RoomDifficultySelection.Set(RoomDifficulty.Medium);
        }
    }

    private void OnHardToggleChanged(bool isOn)
    {
        if (isOn)
        {
            RoomDifficultySelection.Set(RoomDifficulty.Hard);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMenu();
        }
    }

    public void ToggleMenu()
    {
        if (isOpen)
        {
            CloseMenu();
        }
        else
        {
            OpenMenu();
        }
    }

    public void OpenMenu()
    {
        if (isOpen)
        {
            return;
        }

        if (cursorCaptureRoutine != null)
        {
            StopCoroutine(cursorCaptureRoutine);
            cursorCaptureRoutine = null;
        }

        isOpen = true;

        playerController.SetGameplayInputEnabled(false);
        pauseMenuPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseMenu()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        pauseMenuPanel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Input.ResetInputAxes();
        cursorCaptureRoutine = StartCoroutine(CaptureCursorNextFrame());
        PlayerPrefs.Save();
    }

    private IEnumerator CaptureCursorNextFrame()
    {
        yield return null;

        if (!isOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Input.ResetInputAxes();
            playerController.SetGameplayInputEnabled(true);
        }

        cursorCaptureRoutine = null;
    }

    public void SetSensitivity(float value)
    {
        playerController.sensitivity = value;
        PlayerPrefs.SetFloat(SensitivityPreferenceKey, value);
    }

    public void LoadTutorial()
    {
        LoadScene("Tutorial1");
    }

    public void LoadRoomTest()
    {
        LoadScene("RoomTest1");
    }

    private void LoadScene(string sceneName)
    {
        PlayerPrefs.Save();
        playerController.SetGameplayInputEnabled(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Input.ResetInputAxes();
        SceneManager.LoadScene(sceneName);
    }

    private void OnDestroy()
    {
        bool shouldRestoreGameplay = activeInstance == this
            && (isOpen || cursorCaptureRoutine != null);

        if (cursorCaptureRoutine != null)
        {
            StopCoroutine(cursorCaptureRoutine);
            cursorCaptureRoutine = null;
        }

        if (sensitivitySlider != null)
        {
            sensitivitySlider.onValueChanged.RemoveListener(SetSensitivity);
        }

        if (tutorialButton != null)
        {
            tutorialButton.onClick.RemoveListener(LoadTutorial);
        }

        if (roomButton != null)
        {
            roomButton.onClick.RemoveListener(LoadRoomTest);
        }

        if (easyToggle != null)
        {
            easyToggle.onValueChanged.RemoveListener(OnEasyToggleChanged);
        }

        if (mediumToggle != null)
        {
            mediumToggle.onValueChanged.RemoveListener(OnMediumToggleChanged);
        }

        if (hardToggle != null)
        {
            hardToggle.onValueChanged.RemoveListener(OnHardToggleChanged);
        }

        if (shouldRestoreGameplay)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (playerController != null)
            {
                playerController.SetGameplayInputEnabled(true);
            }
        }

        if (activeInstance == this)
        {
            activeInstance = null;
        }
    }

}
