using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 运行时控制面板 - 在实验运行中允许动态调整
/// </summary>
public class RuntimeControlPanel : MonoBehaviour
{
    #region 单例模式
    public static RuntimeControlPanel Instance { get; private set; }
    #endregion

    #region UI引用

    [Header("面板控制")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private GameObject overlayBackground;

    [Header("时钟调整")]
    [SerializeField] private TMP_InputField clockAdjustInput;
    [SerializeField] private Button addClocksButton;
    [SerializeField] private Button removeClocksButton;
    [SerializeField] private TextMeshProUGUI currentClockCountText;
    [SerializeField] private TextMeshProUGUI nextClockTimeText;

    [Header("快捷按钮")]
    [SerializeField] private Button add100Button;
    [SerializeField] private Button add500Button;
    [SerializeField] private Button add1000Button;
    [SerializeField] private Button remove100Button;
    [SerializeField] private Button remove500Button;

    [Header("观测者调整")]
    [SerializeField] private TMP_InputField observerAdjustInput;
    [SerializeField] private Button addObserversButton;
    [SerializeField] private Button removeObserversButton;
    [SerializeField] private TextMeshProUGUI currentObserverCountText;

    [Header("控制按钮")]
    [SerializeField] private Button continueExperimentButton;
    [SerializeField] private Button applyChangesButton;
    [SerializeField] private Button cancelButton;

    [Header("状态显示")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI pendingChangesText;

    #endregion

    #region 系统引用

    [Header("系统引用")]
    [SerializeField] private ConfigurationManager configurationManager;
    [SerializeField] private ObjectManager objectManager;
    [SerializeField] private ObserverManager observerManager;
    [SerializeField] private ExperimentController experimentController;
    [SerializeField] private ExperimentStateMachine stateMachine;
    [SerializeField] private UIManager uiManager;

    #endregion

    #region 内部状态

    private int pendingClockChange = 0;
    private int pendingObserverChange = 0;
    private bool hasUnappliedChanges = false;

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
    }

    private void Start()
    {
        // 初始化UI
        SetupUI();

        // 验证引用
        ValidateReferences();

        // 默认隐藏
        Hide();
    }

    private void SetupUI()
    {
        // 时钟调整按钮
        if (addClocksButton) addClocksButton.onClick.AddListener(OnAddClocksClick);
        if (removeClocksButton) removeClocksButton.onClick.AddListener(OnRemoveClocksClick);

        // 快捷按钮
        if (add100Button) add100Button.onClick.AddListener(() => SetClockAdjustment(100));
        if (add500Button) add500Button.onClick.AddListener(() => SetClockAdjustment(500));
        if (add1000Button) add1000Button.onClick.AddListener(() => SetClockAdjustment(1000));
        if (remove100Button) remove100Button.onClick.AddListener(() => SetClockAdjustment(-100));
        if (remove500Button) remove500Button.onClick.AddListener(() => SetClockAdjustment(-500));

        // 观测者调整按钮
        if (addObserversButton) addObserversButton.onClick.AddListener(OnAddObserversClick);
        if (removeObserversButton) removeObserversButton.onClick.AddListener(OnRemoveObserversClick);

        // 控制按钮
        if (continueExperimentButton) continueExperimentButton.onClick.AddListener(OnContinueExperiment);
        if (applyChangesButton) applyChangesButton.onClick.AddListener(OnApplyChanges);
        if (cancelButton) cancelButton.onClick.AddListener(OnCancel);

        // 输入框验证
        if (clockAdjustInput) clockAdjustInput.onValueChanged.AddListener(ValidateClockInput);
        if (observerAdjustInput) observerAdjustInput.onValueChanged.AddListener(ValidateObserverInput);
    }

    private void ValidateReferences()
    {
        // 自动获取缺失的引用
        if (configurationManager == null) configurationManager = ConfigurationManager.Instance;
        if (objectManager == null) objectManager = ObjectManager.Instance;
        if (experimentController == null) experimentController = ExperimentController.Instance;
        if (stateMachine == null) stateMachine = ExperimentStateMachine.Instance;
        if (uiManager == null) uiManager = UIManager.Instance;

        // 验证必要引用
        if (configurationManager == null) Debug.LogWarning("[RuntimeControlPanel] 缺少ConfigurationManager引用");
        if (objectManager == null) Debug.LogWarning("[RuntimeControlPanel] 缺少ObjectManager引用");
    }

    #endregion

    #region 显示控制

    /// <summary>
    /// 显示控制面板
    /// </summary>
    public void Show()
    {
        if (panelRoot) panelRoot.SetActive(true);
        if (overlayBackground) overlayBackground.SetActive(true);

        // 暂停实验（如果正在运行）
        if (stateMachine != null && stateMachine.GetCurrentState() == ExperimentStateMachine.ExperimentState.Running)
        {
            stateMachine.TogglePause();
        }

        // 更新显示
        UpdateDisplay();

        // 重置待定更改
        ResetPendingChanges();

        Debug.Log("[RuntimeControlPanel] 控制面板已显示");
    }

    /// <summary>
    /// 隐藏控制面板
    /// </summary>
    public void Hide()
    {
        if (panelRoot) panelRoot.SetActive(false);
        if (overlayBackground) overlayBackground.SetActive(false);

        Debug.Log("[RuntimeControlPanel] 控制面板已隐藏");
    }

    #endregion

    #region 更新显示

    /// <summary>
    /// 更新所有显示信息
    /// </summary>
    private void UpdateDisplay()
    {
        // 获取当前状态
        if (configurationManager != null)
        {
            var state = configurationManager.GetCurrentState();

            // 显示当前时钟数
            if (currentClockCountText)
                currentClockCountText.text = $"当前时钟数: {state.totalClocksGenerated}";

            // 计算并显示下一个时钟时间
            string nextTime = FormatTime(state.nextClockStartTime);
            if (nextClockTimeText)
                nextClockTimeText.text = $"下一个时钟时间: {nextTime}";
        }

        // 显示当前观测者数（从ConfigurationManager获取）
        if (configurationManager != null && currentObserverCountText)
        {
            var state = configurationManager.GetCurrentState();
            currentObserverCountText.text = $"当前观测者数: {state.totalObserversGenerated}";
        }

        // 更新按钮状态
        UpdateButtonStates();
    }

    /// <summary>
    /// 更新待定更改显示
    /// </summary>
    private void UpdatePendingChanges()
    {
        if (pendingChangesText == null) return;

        if (pendingClockChange == 0 && pendingObserverChange == 0)
        {
            pendingChangesText.text = "无待定更改";
            hasUnappliedChanges = false;
        }
        else
        {
            string changes = "待定更改:\n";

            if (pendingClockChange != 0)
            {
                string action = pendingClockChange > 0 ? "增加" : "减少";
                changes += $"- {action} {Mathf.Abs(pendingClockChange)} 个时钟\n";

                // 显示时间范围预览
                if (pendingClockChange > 0 && configurationManager != null)
                {
                    var state = configurationManager.GetCurrentState();
                    string startTime = FormatTime(state.nextClockStartTime);
                    string endTime = FormatTime(state.nextClockStartTime + pendingClockChange - 1);
                    changes += $"  时间范围: {startTime} - {endTime}\n";
                }
            }

            if (pendingObserverChange != 0)
            {
                string action = pendingObserverChange > 0 ? "增加" : "减少";
                changes += $"- {action} {Mathf.Abs(pendingObserverChange)} 个观测者\n";
            }

            pendingChangesText.text = changes;
            hasUnappliedChanges = true;
        }

        // 更新应用按钮状态
        if (applyChangesButton)
            applyChangesButton.interactable = hasUnappliedChanges;
    }

    /// <summary>
    /// 更新按钮状态
    /// </summary>
    private void UpdateButtonStates()
    {
        if (configurationManager == null) return;

        var state = configurationManager.GetCurrentState();

        // 检查是否可以继续添加时钟
        bool canAddClocks = state.totalClocksGenerated < 10000;
        bool canRemoveClocks = state.totalClocksGenerated > 100; // 保留至少100个时钟

        if (addClocksButton) addClocksButton.interactable = canAddClocks;
        if (add100Button) add100Button.interactable = canAddClocks;
        if (add500Button) add500Button.interactable = canAddClocks;
        if (add1000Button) add1000Button.interactable = canAddClocks;

        if (removeClocksButton) removeClocksButton.interactable = canRemoveClocks;
        if (remove100Button) remove100Button.interactable = canRemoveClocks;
        if (remove500Button) remove500Button.interactable = state.totalClocksGenerated >= 500;

        // 观测者按钮
        bool canAddObservers = state.totalObserversGenerated < 10;
        bool canRemoveObservers = state.totalObserversGenerated > 1;

        if (addObserversButton) addObserversButton.interactable = canAddObservers;
        if (removeObserversButton) removeObserversButton.interactable = canRemoveObservers;
    }

    #endregion

    #region 时钟调整

    /// <summary>
    /// 设置时钟调整值
    /// </summary>
    private void SetClockAdjustment(int value)
    {
        if (clockAdjustInput)
        {
            clockAdjustInput.text = Mathf.Abs(value).ToString();
            pendingClockChange = value;
            UpdatePendingChanges();
        }
    }

    /// <summary>
    /// 添加时钟按钮点击
    /// </summary>
    private void OnAddClocksClick()
    {
        if (int.TryParse(clockAdjustInput.text, out int count) && count > 0)
        {
            pendingClockChange = count;
            UpdatePendingChanges();
        }
    }

    /// <summary>
    /// 移除时钟按钮点击
    /// </summary>
    private void OnRemoveClocksClick()
    {
        if (int.TryParse(clockAdjustInput.text, out int count) && count > 0)
        {
            pendingClockChange = -count;
            UpdatePendingChanges();
        }
    }

    /// <summary>
    /// 验证时钟输入
    /// </summary>
    private void ValidateClockInput(string input)
    {
        if (!int.TryParse(input, out int value) || value < 0)
        {
            clockAdjustInput.text = "0";
        }
        else if (value > 5000)
        {
            clockAdjustInput.text = "5000";
        }
    }

    #endregion

    #region 观测者调整

    /// <summary>
    /// 添加观测者按钮点击
    /// </summary>
    private void OnAddObserversClick()
    {
        if (int.TryParse(observerAdjustInput.text, out int count) && count > 0)
        {
            pendingObserverChange = count;
            UpdatePendingChanges();
        }
    }

    /// <summary>
    /// 移除观测者按钮点击
    /// </summary>
    private void OnRemoveObserversClick()
    {
        if (int.TryParse(observerAdjustInput.text, out int count) && count > 0)
        {
            pendingObserverChange = -count;
            UpdatePendingChanges();
        }
    }

    /// <summary>
    /// 验证观测者输入
    /// </summary>
    private void ValidateObserverInput(string input)
    {
        if (!int.TryParse(input, out int value) || value < 0)
        {
            observerAdjustInput.text = "0";
        }
        else if (value > 10)
        {
            observerAdjustInput.text = "10";
        }
    }

    #endregion

    #region 控制操作

    /// <summary>
    /// 应用更改
    /// </summary>
    private void OnApplyChanges()
    {
        if (!hasUnappliedChanges) return;

        // 显示确认对话框
        if (uiManager != null)
        {
            // 计算起始时间
            int startTime = 0;
            if (pendingClockChange > 0 && configurationManager != null)
            {
                startTime = configurationManager.GetCurrentState().nextClockStartTime;
            }

            uiManager.ShowAddConfirmation(
                pendingClockChange > 0 ? pendingClockChange : 0,
                pendingObserverChange > 0 ? pendingObserverChange : 0,
                startTime
            );
        }
        else
        {
            // 直接应用
            StartCoroutine(ApplyChangesCoroutine());
        }
    }

    /// <summary>
    /// 应用更改协程
    /// </summary>
    private IEnumerator ApplyChangesCoroutine()
    {
        // 显示状态
        if (statusText) statusText.text = "正在应用更改...";

        // 禁用按钮
        SetButtonsInteractable(false);

        // 应用时钟更改
        if (pendingClockChange != 0 && configurationManager != null)
        {
            if (pendingClockChange > 0)
            {
                // 增加时钟
                configurationManager.ApplyCustomConfiguration(pendingClockChange, 0);

                if (statusText)
                    statusText.text = $"已添加 {pendingClockChange} 个时钟";
            }
            else
            {
                // 移除时钟
                int removeCount = Mathf.Abs(pendingClockChange);
                if (objectManager != null)
                {
                    objectManager.RemoveLastClocks(removeCount);

                    // 通知ConfigurationManager更新计数
                    // 注意：这需要ConfigurationManager有相应的方法
                    var state = configurationManager.GetCurrentState();
                    // 手动更新状态（如果ConfigurationManager没有UpdateAfterRemoval方法）
                    Debug.Log($"[RuntimeControlPanel] 通知ConfigurationManager移除了{removeCount}个时钟");
                }

                if (statusText)
                    statusText.text = $"已移除 {removeCount} 个时钟";
            }
        }

        yield return new WaitForSeconds(0.5f);

        // 应用观测者更改
        if (pendingObserverChange != 0 && configurationManager != null)
        {
            if (pendingObserverChange > 0)
            {
                // 增加观测者
                configurationManager.ApplyCustomConfiguration(0, pendingObserverChange);

                if (statusText)
                    statusText.text = $"已添加 {pendingObserverChange} 个观测者";
            }
            else
            {
                // 移除观测者（需要ObserverManager支持）
                int removeCount = Mathf.Abs(pendingObserverChange);

                if (statusText)
                    statusText.text = $"观测者移除功能需要ObserverManager支持";

                Debug.LogWarning("[RuntimeControlPanel] 观测者移除功能需要ObserverManager.RemoveObservers方法");
            }
        }

        yield return new WaitForSeconds(0.5f);

        // 更新显示
        UpdateDisplay();
        ResetPendingChanges();

        // 重新启用按钮
        SetButtonsInteractable(true);

        if (statusText) statusText.text = "更改已应用";

        yield return new WaitForSeconds(1f);
        if (statusText) statusText.text = "";
    }

    /// <summary>
    /// 继续实验
    /// </summary>
    private void OnContinueExperiment()
    {
        // 检查是否有未应用的更改
        if (hasUnappliedChanges)
        {
            if (statusText)
            {
                statusText.text = "请先应用或取消待定更改";
                StartCoroutine(ClearStatusAfterDelay(2f));
            }
            return;
        }

        // 隐藏面板
        Hide();

        // 恢复实验
        if (stateMachine != null && stateMachine.GetCurrentState() == ExperimentStateMachine.ExperimentState.Paused)
        {
            stateMachine.TogglePause();
        }
    }

    /// <summary>
    /// 取消操作
    /// </summary>
    private void OnCancel()
    {
        // 询问是否确定取消
        if (hasUnappliedChanges)
        {
            // 可以添加确认对话框
            Debug.Log("[RuntimeControlPanel] 取消待定更改");
        }

        // 重置待定更改
        ResetPendingChanges();

        // 隐藏面板
        Hide();

        // 恢复实验
        if (stateMachine != null && stateMachine.GetCurrentState() == ExperimentStateMachine.ExperimentState.Paused)
        {
            stateMachine.TogglePause();
        }
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 重置待定更改
    /// </summary>
    private void ResetPendingChanges()
    {
        pendingClockChange = 0;
        pendingObserverChange = 0;
        hasUnappliedChanges = false;

        if (clockAdjustInput) clockAdjustInput.text = "";
        if (observerAdjustInput) observerAdjustInput.text = "";

        UpdatePendingChanges();
    }

    /// <summary>
    /// 设置按钮可交互性
    /// </summary>
    private void SetButtonsInteractable(bool interactable)
    {
        if (addClocksButton) addClocksButton.interactable = interactable;
        if (removeClocksButton) removeClocksButton.interactable = interactable;
        if (addObserversButton) addObserversButton.interactable = interactable;
        if (removeObserversButton) removeObserversButton.interactable = interactable;
        if (applyChangesButton) applyChangesButton.interactable = interactable && hasUnappliedChanges;
        if (continueExperimentButton) continueExperimentButton.interactable = interactable;
        if (cancelButton) cancelButton.interactable = interactable;

        // 快捷按钮
        if (add100Button) add100Button.interactable = interactable;
        if (add500Button) add500Button.interactable = interactable;
        if (add1000Button) add1000Button.interactable = interactable;
        if (remove100Button) remove100Button.interactable = interactable;
        if (remove500Button) remove500Button.interactable = interactable;
    }

    /// <summary>
    /// 格式化时间显示
    /// </summary>
    private string FormatTime(int totalSeconds)
    {
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
    }

    /// <summary>
    /// 延迟清除状态文本
    /// </summary>
    private IEnumerator ClearStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (statusText) statusText.text = "";
    }

    #endregion

    #region 公开接口

    /// <summary>
    /// 获取是否有待定更改
    /// </summary>
    public bool HasPendingChanges => hasUnappliedChanges;

    /// <summary>
    /// 获取面板是否显示
    /// </summary>
    public bool IsShowing => panelRoot != null && panelRoot.activeSelf;

    #endregion
}