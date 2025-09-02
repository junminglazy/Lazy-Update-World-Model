using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI管理器 - 负责所有UI显示和交互
/// 增强版：在左上角面板显示主时间、活跃时钟、观测者数量和当前模式
/// </summary>
public class UIManager : MonoBehaviour
{
    #region 单例模式
    public static UIManager Instance { get; private set; }
    #endregion

    #region 内部类

    private class PerformanceData
    {
        public float averageFPS = 0f;
        public float averageCPU = 0f;
        public float averageUpdateRate = 0f;
        public float averageActiveRatio = 0f;
        public int sampleCount = 0;

        public void UpdateData(float fps, float cpu, float updateRate, float activeRatio)
        {
            sampleCount++;
            averageFPS = ((averageFPS * (sampleCount - 1)) + fps) / sampleCount;
            averageCPU = ((averageCPU * (sampleCount - 1)) + cpu) / sampleCount;
            averageUpdateRate = ((averageUpdateRate * (sampleCount - 1)) + updateRate) / sampleCount;
            averageActiveRatio = ((averageActiveRatio * (sampleCount - 1)) + activeRatio) / sampleCount;
        }

        public void Reset()
        {
            averageFPS = 0f;
            averageCPU = 0f;
            averageUpdateRate = 0f;
            averageActiveRatio = 0f;
            sampleCount = 0;
        }
    }

    #endregion

    #region UI面板引用

    [Header("主要面板")]
    [SerializeField] private GameObject configurationPanel;      // 配置面板
    [SerializeField] private GameObject readyPanel;              // 准备就绪面板
    [SerializeField] private GameObject performancePanel;        // 性能监控面板
    [SerializeField] private GameObject comparisonPanel;         // 对比面板
    [SerializeField] private GameObject warningPanel;            // 警告面板
    [SerializeField] private GameObject controlPanel;            // 控制面板
    [SerializeField] private Canvas canvas;                      // 主画布

    [Header("运行时控制")]
    [SerializeField] private RuntimeControlPanel runtimeControlPanel;
    [SerializeField] private GameObject addConfirmationDialog;
    [SerializeField] private GameObject confirmationDialogPrefab;

    #endregion

    #region UI组件引用

    [Header("=== 左上角实验监控面板 ===")]
    [SerializeField] private GameObject experimentMonitorPanel;    // 实验监控面板（左上角）

    [Header("核心监控显示")]
    [SerializeField] private TextMeshProUGUI mainTimeText;        // 主时间显示
    [SerializeField] private TextMeshProUGUI activeClockText;     // 活跃时钟显示 
    [SerializeField] private TextMeshProUGUI observerCountText;   // 观测者数量显示
    [SerializeField] private TextMeshProUGUI currentModeText;     // 当前模式显示
    [SerializeField] private TextMeshProUGUI fpsDisplayText;      // FPS显示
    [SerializeField] private TextMeshProUGUI cpuDisplayText;      // CPU显示
    [SerializeField] private TextMeshProUGUI cameraModeText;      // 上帝视角模式

    [Header("模式切换控制")]
    [SerializeField] private Button modeSwitchButton;             // 模式切换按钮
    [SerializeField] private Button pauseResumeButton;            // 暂停/继续按钮
    [SerializeField] private Button resetExperimentButton;        // 重置按钮
    [SerializeField] private TextMeshProUGUI pauseButtonLabel;    // 暂停按钮文本

    [Header("配置面板组件")]
    [SerializeField] private Button[] presetButtons;             // 预设按钮组
    [SerializeField] private Slider clockCountSlider;            // 时钟数量滑块
    [SerializeField] private InputField clockCountInput;         // 时钟数量输入
    [SerializeField] private Slider observerCountSlider;         // 观测者数量滑块
    [SerializeField] private InputField observerCountInput;      // 观测者数量输入
    [SerializeField] private Button applyConfigButton;           // 应用配置按钮
    [SerializeField] private Button startExperimentButton;       // 开始实验按钮
    [SerializeField] private TextMeshProUGUI currentClockCountText;    // 当前时钟数显示
    [SerializeField] private TextMeshProUGUI nextStartTimeText;        // 下一个起始时间显示

    [Header("性能监控组件（旧版兼容）")]
    [SerializeField] private TextMeshProUGUI fpsText;
    [SerializeField] private TextMeshProUGUI framTimeText;
    [SerializeField] private TextMeshProUGUI activeCountText;
    [SerializeField] private TextMeshProUGUI cpuText;
    [SerializeField] private TextMeshProUGUI modeText;
    [SerializeField] private TextMeshProUGUI modeTimeText;
    [SerializeField] private Image fpsBar;
    [SerializeField] private Image cpuBar;

    [Header("对比面板组件")]
    [SerializeField] private TextMeshProUGUI traditionalFPSText;
    [SerializeField] private TextMeshProUGUI traditionalCPUText;
    [SerializeField] private TextMeshProUGUI traditionalUpdateRateText;
    [SerializeField] private TextMeshProUGUI lazyFPSText;
    [SerializeField] private TextMeshProUGUI lazyCPUText;
    [SerializeField] private TextMeshProUGUI lazyUpdateRateText;
    [SerializeField] private TextMeshProUGUI fpsGainText;
    [SerializeField] private TextMeshProUGUI cpuSavedText;
    [SerializeField] private TextMeshProUGUI efficiencyRatioText;

    [Header("控制面板组件")]
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button switchModeButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button saveDataButton;
    [SerializeField] private TextMeshProUGUI pauseButtonText;

    [Header("其他UI组件")]
    [SerializeField] private TextMeshProUGUI tooltipText;              // 工具提示文本
    [SerializeField] private TextMeshProUGUI confirmationMessageText;  // 确认消息文本
    [SerializeField] private Button confirmAddButton;                  // 确认添加按钮
    [SerializeField] private Button cancelAddButton;                   // 取消添加按钮

    [Header("实时统计显示")]
    [SerializeField] private TextMeshProUGUI objectsInViewText;
    [SerializeField] private TextMeshProUGUI pendingUpdatesText;

    #endregion

    #region 系统引用

    [Header("系统引用")]
    [SerializeField] private TimeManager timeManager;
    [SerializeField] private ObjectManager objectManager;
    [SerializeField] private ObserverManager observerManager;
    [SerializeField] private ExperimentController experimentController;
    [SerializeField] private PerformanceMonitor performanceMonitor;
    [SerializeField] private CameraController cameraController;

    #endregion

    #region 私有变量

    private Dictionary<ExperimentController.ExperimentMode, PerformanceData> modePerformanceData;
    private ExperimentController.ExperimentMode previousMode;
    private Coroutine updateCoroutine;

    #endregion

    #region Unity生命周期

    private void Awake()
    {
        // 单例设置
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        // 初始化数据结构
        modePerformanceData = new Dictionary<ExperimentController.ExperimentMode, PerformanceData>();
    }

    private void Start()
    {
        // 初始化UI
        InitializeUI();

        // 启动定时更新
        updateCoroutine = StartCoroutine(UpdateMonitorDisplay());
    }

    private void OnDestroy()
    {
        if (updateCoroutine != null)
        {
            StopCoroutine(updateCoroutine);
        }
    }

    #endregion

    #region 初始化

    private void InitializeUI()
    {
        // 隐藏所有面板
        HideAllPanels();

        // 设置按钮事件
        SetupButtonEvents();

        // 初始化性能数据
        modePerformanceData[ExperimentController.ExperimentMode.Traditional] = new PerformanceData();
        modePerformanceData[ExperimentController.ExperimentMode.LazyUpdate] = new PerformanceData();

        // 显示实验监控面板（始终可见）
        if (experimentMonitorPanel != null)
        {
            experimentMonitorPanel.SetActive(true);
        }

        Debug.Log("[UIManager] UI初始化完成");
    }

    private void SetupButtonEvents()
    {
        // 配置面板按钮
        if (applyConfigButton != null)
            applyConfigButton.onClick.AddListener(OnApplyConfiguration);

        if (startExperimentButton != null)
            startExperimentButton.onClick.AddListener(OnStartExperiment);

        // 预设按钮
        if (presetButtons != null)
        {
            for (int i = 0; i < presetButtons.Length; i++)
            {
                int presetIndex = i;
                presetButtons[i].onClick.AddListener(() => OnPresetSelected(presetIndex));
            }
        }

        // 控制面板按钮
        if (pauseButton != null)
            pauseButton.onClick.AddListener(OnPauseButtonClick);

        if (switchModeButton != null)
            switchModeButton.onClick.AddListener(OnSwitchMode);

        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetButtonClick);

        if (saveDataButton != null)
            saveDataButton.onClick.AddListener(OnSaveData);

        // 左上角监控面板按钮
        if (modeSwitchButton != null)
            modeSwitchButton.onClick.AddListener(OnSwitchMode);

        if (pauseResumeButton != null)
            pauseResumeButton.onClick.AddListener(OnPauseButtonClick);

        if (resetExperimentButton != null)
            resetExperimentButton.onClick.AddListener(OnResetButtonClick);

        // 确认对话框按钮
        if (confirmAddButton != null)
            confirmAddButton.onClick.AddListener(() => OnConfirmAdd(0, 0));

        if (cancelAddButton != null)
            cancelAddButton.onClick.AddListener(OnCancelAdd);
    }

    #endregion

    #region 实验监控面板更新（左上角）

    /// <summary>
    /// 定时更新监控显示
    /// </summary>
    private IEnumerator UpdateMonitorDisplay()
    {
        while (true)
        {
            // 更新主时间
            if (mainTimeText != null && timeManager != null)
            {
                float mainTime = timeManager.GetMainTime();
                mainTimeText.text = $"Main Time:\n{FormatTime(mainTime)}";
            }

            // 更新活跃时钟
            if (activeClockText != null && objectManager != null)
            {
                var stats = objectManager.GetStats();
                var mode = experimentController.CurrentMode;
                if (mode == ExperimentController.ExperimentMode.Traditional)
                {
                    activeClockText.text = $"ActiveUpdate:\n{stats.totalClocks}/{stats.totalClocks} ({stats.activeRatio:P1})";
                }
                else
                {
                    activeClockText.text = $"ActiveUpdate:\n{stats.activeClocks}/{stats.totalClocks} ({stats.activeRatio:P1})";
                }
            }

            // 更新观测者数量
            if (observerCountText != null && observerManager != null)
            {
                int observerCount = observerManager.GetObserverCount();
                observerCountText.text = $"ObserverCount:{observerCount}";
            }

            // 更新当前模式
            if (currentModeText != null && experimentController != null)
            {
                var mode = experimentController.CurrentMode;
                string modeStr = mode == ExperimentController.ExperimentMode.Traditional ? "Normal" : "Lasy";
                currentModeText.text = $"Mode:{modeStr}";
            }

            // 更新上帝视角当前模式
            if (cameraModeText != null && experimentController != null)
            {
                var mode = cameraController.CurrentMode;
                string modeStr = mode == CameraController.CameraMode.ExternalObserver ? "ExternalObserver" : "InternalObserver";
                cameraModeText.text = $"CameraMode:\n{modeStr}";
            }
            // 更新FPS
            if (fpsDisplayText != null && performanceMonitor != null)
            {
                float fps = performanceMonitor.GetCurrentFPS();
                fpsDisplayText.text = $"FPS:{fps:F0}";
            }

            // 更新CPU
            if (cpuDisplayText != null && performanceMonitor != null)
            {
                float cpu = performanceMonitor.GetCPUUsage();
                cpuDisplayText.text = $"CPU:{cpu:F0}";
            }

            // 更新暂停按钮文本
            if (pauseButtonLabel != null && timeManager != null)
            {
                pauseButtonLabel.text = timeManager.IsPaused ? "继续" : "暂停";
            }

            yield return new WaitForSeconds(0.1f); // 每0.1秒更新一次
        }
    }

    #endregion

    #region 面板管理

    public void ShowPanel(string panelName)
    {
        HideAllPanels();

        switch (panelName)
        {
            case "Configuration":
                if (configurationPanel != null) configurationPanel.SetActive(true);
                break;
            case "Ready":
                if (readyPanel != null) readyPanel.SetActive(true);
                break;
            case "Performance":
                if (performancePanel != null) performancePanel.SetActive(true);
                if (controlPanel != null) controlPanel.SetActive(true);
                break;
            case "Comparison":
                if (comparisonPanel != null) comparisonPanel.SetActive(true);
                break;
        }
    }

    private void HideAllPanels()
    {
        if (configurationPanel != null) configurationPanel.SetActive(false);
        if (readyPanel != null) readyPanel.SetActive(false);
        if (performancePanel != null) performancePanel.SetActive(false);
        if (comparisonPanel != null) comparisonPanel.SetActive(false);
        if (warningPanel != null) warningPanel.SetActive(false);

        // 注意：不隐藏experimentMonitorPanel，它应该始终显示
    }

    public void ShowConfigurationPanel()
    {
        ShowPanel("Configuration");
        Debug.Log("[UIManager] 显示配置面板");
    }

    public void HideConfigurationPanel()
    {
        if (configurationPanel != null)
            configurationPanel.SetActive(false);
    }

    public void ShowGenerationProgress()
    {
        ShowTooltip("正在生成场景对象...");
        Debug.Log("[UIManager] 显示生成进度");
    }

    public void ShowReadyPanel()
    {
        ShowPanel("Ready");
        Debug.Log("[UIManager] 显示准备就绪面板");
    }

    public void ShowControlPanel()
    {
        ShowPanel("Performance");
        Debug.Log("[UIManager] 显示控制面板");
    }

    public void ShowPauseOverlay()
    {
        if (warningPanel != null)
        {
            var warningText = warningPanel.GetComponentInChildren<TextMeshProUGUI>();
            if (warningText != null)
            {
                warningText.text = "实验已暂停";
            }
            warningPanel.SetActive(true);
        }

        Debug.Log("[UIManager] 显示暂停遮罩");
    }

    public void HidePauseOverlay()
    {
        if (warningPanel != null && warningPanel.activeSelf)
        {
            var warningText = warningPanel.GetComponentInChildren<TextMeshProUGUI>();
            if (warningText != null && warningText.text == "实验已暂停")
            {
                warningPanel.SetActive(false);
            }
        }
    }

    public void ShowResultsPanel()
    {
        ShowPanel("Comparison");
        ShowTooltip("实验完成，正在生成报告...");
        Debug.Log("[UIManager] 显示结果面板");
    }

    #endregion

    #region 配置面板功能

    private void OnPresetSelected(int presetIndex)
    {
        if (ConfigurationManager.Instance != null)
        {
            ConfigurationManager.Instance.ApplyPreset(presetIndex);
        }

        UpdateConfigurationUI();
    }

    private void OnApplyConfiguration()
    {
        int clockCount = (int)clockCountSlider.value;
        int observerCount = (int)observerCountSlider.value;

        if (ConfigurationManager.Instance != null)
        {
            ConfigurationManager.Instance.ApplyCustomConfiguration(clockCount, observerCount);
        }

        UpdateConfigurationUI();
    }

    private void OnStartExperiment()
    {
        if (ExperimentStateMachine.Instance != null)
        {
            ExperimentStateMachine.Instance.OnConfigurationCompleted();
        }
    }

    private void UpdateConfigurationUI()
    {
        if (ConfigurationManager.Instance != null)
        {
            var state = ConfigurationManager.Instance.GetCurrentState();

            if (currentClockCountText != null)
                currentClockCountText.text = $"已生成时钟: {state.totalClocksGenerated}";

            if (nextStartTimeText != null)
                nextStartTimeText.text = $"下一个起始时间: {FormatTime(state.nextClockStartTime)}";
        }
    }

    #endregion

    #region 性能监控更新

    public void UpdatePerformanceDisplay(PerformanceMonitor.PerformanceMetrics metrics)
    {
        // 更新旧版性能面板（如果存在）
        if (performancePanel != null && performancePanel.activeSelf)
        {
            if (fpsText != null)
                fpsText.text = $"FPS: {metrics.currentFPS:F0}";

            if (framTimeText != null)
                framTimeText.text = $"Frame: {metrics.frameTime:F1}ms";

            // 获取对象管理器统计
            ObjectManager.ObjectManagerStats stats = null;
            if (ObjectManager.Instance != null)
            {
                stats = ObjectManager.Instance.GetStats();
            }

            if (activeCountText != null && stats != null)
            {
                activeCountText.text = $"活跃: {stats.activeClocks}/{stats.totalClocks} ({stats.activeRatio:P1})";
            }

            if (cpuText != null)
                cpuText.text = $"CPU: {metrics.cpuUsage:F0}%";

            if (fpsBar != null)
                fpsBar.fillAmount = Mathf.Clamp01(metrics.currentFPS / 60f);

            if (cpuBar != null)
                cpuBar.fillAmount = Mathf.Clamp01(metrics.cpuUsage / 100f);
        }

        // 更新当前模式性能数据
        if (ExperimentController.Instance != null)
        {
            var currentMode = ExperimentController.Instance.CurrentMode;
            if (modePerformanceData.ContainsKey(currentMode))
            {
                var data = modePerformanceData[currentMode];
                ObjectManager.ObjectManagerStats stats = null;
                if (ObjectManager.Instance != null)
                {
                    stats = ObjectManager.Instance.GetStats();
                }
                float activeRatio = stats != null ? stats.activeRatio : 0f;
                data.UpdateData(metrics.currentFPS, metrics.cpuUsage, metrics.lazyUpdatesPerFrame, activeRatio);
            }
        }

        // 更新对比面板
        UpdateComparisonPanel();
    }

    public void SetModeDisplay(ExperimentController.ExperimentMode mode)
    {
        if (modeText != null)
            modeText.text = $"模式: {(mode == ExperimentController.ExperimentMode.Traditional ? "传统" : "惰性")}";
    }

    public void UpdateModeTime(float time)
    {
        if (modeTimeText != null)
            modeTimeText.text = $"时间: {FormatTime(time)}";
    }

    #endregion

    #region 对比面板更新

    private void UpdateComparisonPanel()
    {
        if (!comparisonPanel || !comparisonPanel.activeSelf) return;

        var tradData = modePerformanceData[ExperimentController.ExperimentMode.Traditional];
        var lazyData = modePerformanceData[ExperimentController.ExperimentMode.LazyUpdate];

        // 传统模式数据
        if (traditionalFPSText != null)
            traditionalFPSText.text = tradData.averageFPS.ToString("F1");
        if (traditionalCPUText != null)
            traditionalCPUText.text = tradData.averageCPU.ToString("F0") + "%";
        if (traditionalUpdateRateText != null)
            traditionalUpdateRateText.text = tradData.averageUpdateRate.ToString("F0") + "/frame";

        // 惰性模式数据
        if (lazyFPSText != null)
            lazyFPSText.text = lazyData.averageFPS.ToString("F1");
        if (lazyCPUText != null)
            lazyCPUText.text = lazyData.averageCPU.ToString("F0") + "%";
        if (lazyUpdateRateText != null)
            lazyUpdateRateText.text = lazyData.averageUpdateRate.ToString("F0") + "/frame";

        // 计算提升
        if (tradData.sampleCount > 0 && lazyData.sampleCount > 0)
        {
            float fpsGain = ((lazyData.averageFPS - tradData.averageFPS) / tradData.averageFPS) * 100;
            float cpuSaved = ((tradData.averageCPU - lazyData.averageCPU) / tradData.averageCPU) * 100;
            float efficiency = (1 - lazyData.averageActiveRatio) * 100;

            if (fpsGainText != null)
                fpsGainText.text = $"+{fpsGain:F0}%";
            if (cpuSavedText != null)
                cpuSavedText.text = $"-{cpuSaved:F0}%";
            if (efficiencyRatioText != null)
                efficiencyRatioText.text = $"{efficiency:F1}%";
        }
    }

    #endregion

    #region 模式切换

    public void RecordModeSwitch(ExperimentController.ExperimentMode fromMode)
    {
        previousMode = fromMode;
    }

    public void ShowModeSwitch(ExperimentController.ExperimentMode toMode)
    {
        string message = $"模式切换: {(previousMode == ExperimentController.ExperimentMode.Traditional ? "传统" : "惰性")} → {(toMode == ExperimentController.ExperimentMode.Traditional ? "传统" : "惰性")}";
        ShowTooltip(message);

        // 显示对比面板
        if (comparisonPanel != null && !comparisonPanel.activeSelf)
        {
            ShowPanel("Comparison");
            if (performancePanel != null) performancePanel.SetActive(true);
            if (controlPanel != null) controlPanel.SetActive(true);
        }
    }

    #endregion

    #region 控制面板功能

    private void OnPauseButtonClick()
    {
        if (ExperimentStateMachine.Instance != null)
        {
            ExperimentStateMachine.Instance.TogglePause();
            UpdatePauseButton();
        }
    }

    private void UpdatePauseButton()
    {
        bool isPaused = false;

        if (ExperimentStateMachine.Instance != null)
        {
            isPaused = ExperimentStateMachine.Instance.GetCurrentState() ==
                      ExperimentStateMachine.ExperimentState.Paused;
        }

        // 更新两个暂停按钮的文本
        if (pauseButtonText != null)
            pauseButtonText.text = isPaused ? "继续" : "暂停";

        if (pauseButtonLabel != null)
            pauseButtonLabel.text = isPaused ? "继续" : "暂停";
    }

    private void OnSwitchMode()
    {
        if (ExperimentController.Instance != null)
        {
            ExperimentController.Instance.SwitchMode();
        }
    }

    private void OnResetButtonClick()
    {
        ShowResetConfirmation();
    }

    private void OnSaveData()
    {
        ShowTooltip("数据已保存");
    }

    #endregion

    #region 运行时添加功能

    public void ShowAddConfirmation(int clockCount, int observerCount, int startTime)
    {
        if (addConfirmationDialog != null)
        {
            addConfirmationDialog.SetActive(true);

            string message = "确认添加:\n";
            if (clockCount > 0)
            {
                string timeString = FormatTime(startTime);
                message += $"• {clockCount} 个时钟（从 {timeString} 开始）\n";
            }
            if (observerCount > 0)
            {
                message += $"• {observerCount} 个观测者\n";
            }

            if (confirmationMessageText != null)
            {
                confirmationMessageText.text = message;
            }

            // 更新按钮事件
            if (confirmAddButton != null)
            {
                confirmAddButton.onClick.RemoveAllListeners();
                confirmAddButton.onClick.AddListener(() => OnConfirmAdd(clockCount, observerCount));
            }
        }
    }

    private void OnConfirmAdd(int clockCount, int observerCount)
    {
        if (ConfigurationManager.Instance != null)
        {
            ConfigurationManager.Instance.ApplyCustomConfiguration(clockCount, observerCount);
        }

        if (addConfirmationDialog != null)
            addConfirmationDialog.SetActive(false);
    }

    private void OnCancelAdd()
    {
        if (addConfirmationDialog != null)
            addConfirmationDialog.SetActive(false);
    }

    #endregion

    #region 相机模式显示

    public void UpdateObjectsInViewCount(int count)
    {
        if (objectsInViewText != null)
        {
            objectsInViewText.text = $"视野内时钟: {count}";
        }
    }

    #endregion

    #region 警告和提示

    public void ShowPerformanceWarning(string message)
    {
        if (warningPanel != null)
        {
            var warningText = warningPanel.GetComponentInChildren<TextMeshProUGUI>();
            if (warningText != null)
            {
                warningText.text = $"⚠️ 性能警告: {message}";
            }

            warningPanel.SetActive(true);
            StartCoroutine(AutoHideWarning(3f));
        }
    }

    private void ShowTooltip(string message)
    {
        if (tooltipText != null)
        {
            tooltipText.text = message;
            tooltipText.gameObject.SetActive(true);
            StartCoroutine(AutoHideTooltip(2f));
        }
    }

    public void ShowResetConfirmation()
    {
        if (confirmationDialogPrefab != null)
        {
            var dialog = Instantiate(confirmationDialogPrefab, canvas.transform);

            var messageText = dialog.GetComponentInChildren<TextMeshProUGUI>();
            if (messageText != null)
            {
                messageText.text = "确定要重置整个实验吗？\n所有数据将被清除！";
            }

            var buttons = dialog.GetComponentsInChildren<Button>();
            if (buttons.Length >= 2)
            {
                buttons[0].onClick.AddListener(() => {
                    if (ExperimentStateMachine.Instance != null)
                    {
                        ExperimentStateMachine.Instance.ResetExperiment();
                    }
                    Destroy(dialog);
                });

                buttons[1].onClick.AddListener(() => {
                    Destroy(dialog);
                });
            }
        }
    }

    #endregion

    #region 配置显示更新

    public void UpdateConfigurationDisplay(int totalClocks, int nextStartTime, int totalObservers, string nextTimeString)
    {
        if (currentClockCountText != null)
            currentClockCountText.text = $"已生成时钟: {totalClocks}";

        if (nextStartTimeText != null)
            nextStartTimeText.text = $"下一个起始时间: {nextTimeString}";

        if (runtimeControlPanel != null && runtimeControlPanel.gameObject.activeSelf)
        {
            // RuntimeControlPanel会自己更新显示
        }
    }

    public void SetConfigurationLockState(bool locked)
    {
        if (configurationPanel != null)
        {
            var buttons = configurationPanel.GetComponentsInChildren<Button>();
            foreach (var button in buttons)
            {
                if (button.name != "BackButton")
                {
                    button.interactable = !locked;
                }
            }

            var lockIndicator = configurationPanel.transform.Find("LockIndicator");
            if (lockIndicator != null)
            {
                lockIndicator.gameObject.SetActive(locked);
            }
        }
    }

    #endregion

    #region 工具方法

    private string FormatTime(float totalSeconds)
    {
        int hours = (int)(totalSeconds / 3600);
        int minutes = (int)((totalSeconds % 3600) / 60);
        int seconds = (int)(totalSeconds % 60);
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
    }

    private string FormatTime(int totalSeconds)
    {
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
    }

    private IEnumerator FlashIndicator(GameObject indicator)
    {
        if (indicator == null) yield break;

        float duration = 0.5f;
        float elapsed = 0f;

        CanvasGroup canvasGroup = indicator.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = indicator.AddComponent<CanvasGroup>();
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            canvasGroup.alpha = Mathf.PingPong(t * 4, 1f);
            yield return null;
        }

        canvasGroup.alpha = 1f;
    }

    private IEnumerator AutoHideWarning(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (warningPanel != null)
        {
            warningPanel.SetActive(false);
        }
    }

    private IEnumerator AutoHideTooltip(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (tooltipText != null)
        {
            tooltipText.gameObject.SetActive(false);
        }
    }

    #endregion

    #region 对象管理相关

    public void SetTotalObjectCount(int count)
    {
        // 更新所有显示总数的地方
        if (activeCountText != null)
        {
            string currentText = activeCountText.text;
            int activeCount = 0;

            if (currentText.Contains("/"))
            {
                string[] parts = currentText.Split('/');
                if (parts.Length > 0)
                {
                    string activeStr = parts[0].Replace("活跃: ", "").Trim();
                    int.TryParse(activeStr, out activeCount);
                }
            }

            activeCountText.text = $"活跃: {activeCount}/{count}";
        }

        if (currentClockCountText != null)
        {
            currentClockCountText.text = $"已生成时钟: {count}";
        }
    }

    public void UpdateActiveObjectCount(int activeCount, int totalCount)
    {
        if (activeCountText != null)
        {
            float ratio = totalCount > 0 ? (float)activeCount / totalCount : 0f;
            activeCountText.text = $"活跃: {activeCount}/{totalCount} ({ratio:P1})";
        }
    }

    #endregion

    #region 其他功能

    public void ClearDisplay()
    {
        // 清空所有显示
        if (fpsText != null) fpsText.text = "FPS: --";
        if (cpuText != null) cpuText.text = "CPU: --%";
        if (activeCountText != null) activeCountText.text = "活跃: --/--";
        if (mainTimeText != null) mainTimeText.text = "主时间：00:00:00";
        if (activeClockText != null) activeClockText.text = "活跃时钟：0/0 (0.0%)";
        if (observerCountText != null) observerCountText.text = "观测者数量：0";
        if (currentModeText != null) currentModeText.text = "当前模式：--";
        if (fpsDisplayText != null) fpsDisplayText.text = "FPS：--";
        if (cpuDisplayText != null) cpuDisplayText.text = "CPU：--%";

        // 重置性能数据
        foreach (var data in modePerformanceData.Values)
        {
            data.Reset();
        }
    }

    public void OnExperimentControlClick()
    {
        if (ExperimentController.Instance != null && ExperimentController.Instance.IsRunning)
        {
            if (runtimeControlPanel != null)
            {
                runtimeControlPanel.Show();
            }
        }
        else
        {
            ShowTooltip("请先开始实验");
        }
    }

    #endregion

    #region 惰性更新相关

    public void LogObjectUpdate()
    {
        if (ObjectManager.Instance != null)
        {
            ObjectManager.Instance.UpdateActiveCount(1);
        }
    }

    public void UpdateLazyUpdateStats(int updatesThisFrame, int totalUpdates)
    {
        if (pendingUpdatesText != null)
        {
            pendingUpdatesText.text = $"本帧更新: {updatesThisFrame}";
        }
    }

    #endregion
}