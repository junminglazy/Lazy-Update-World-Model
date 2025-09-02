using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 实验总控制器 - 增强版（带调试功能）
/// 支持正式实验流程和独立调试模式
/// </summary>
public class ExperimentController : MonoBehaviour
{
    #region 核心变量与属性

    // 单例模式
    public static ExperimentController Instance { get; private set; }

    // 系统管理器引用
    [Header("=== 系统管理器引用 ===")]
    [SerializeField] private TimeManager timeManager;
    [SerializeField] private ObjectManager objectManager;
    [SerializeField] private ObserverManager observerManager;
    [SerializeField] private ObservableManager observableManager;
    [SerializeField] private ConfigurationManager configurationManager;
    [SerializeField] private ExperimentStateMachine stateMachine;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private CameraController cameraController;
    [SerializeField] private PerformanceMonitor performanceMonitor;
    [SerializeField] private RuntimeControlPanel runtimeControlPanel;
    [SerializeField] private DataCollector dataCollector;

    // 实验模式
    public enum ExperimentMode
    {
        Traditional,  // 传统模式
        LazyUpdate    // 惰性更新模式
    }

    [Header("=== 实验状态 ===")]
    [SerializeField] private ExperimentMode currentMode = ExperimentMode.Traditional;
    [SerializeField] private bool isExperimentRunning = false;

    [Header("=== 调试模式设置 ===")]
    [Tooltip("是否启用调试模式")]
    [SerializeField] private bool debugModeEnabled = true;

    [Tooltip("调试模式下的时钟数量")]
    [SerializeField] private int debugClockCount = 1000;

    [Tooltip("调试模式下追加的时钟数量")]
    [SerializeField] private int debugAddClockCount = 500;

    [Tooltip("时钟间距")]
    [SerializeField] private float clockSpacing = 2.5f;

    // 调试状态
    private bool debugClocksGenerated = false;
    private bool debugTimeStarted = false;
    private int debugNextClockStartTime = 0;  // 下一个时钟的起始时间（秒）

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
        // 验证所有引用
        ValidateReferences();

        // 显示调试模式状态
        if (debugModeEnabled)
        {
            Debug.Log("=== ExperimentController 调试模式已启用 ===");
            Debug.Log("调试快捷键:");
            Debug.Log("- Z: 生成时钟（不开始计时）");
            Debug.Log("- X: 开始时间流动 / 暂停/恢复");
            Debug.Log("- C: 追加时钟");
            Debug.Log("- V: 清除所有时钟");
            Debug.Log("- B: 切换更新模式");
            Debug.Log("- N: 显示当前状态");
            Debug.Log("- M: 生成观测者");
            Debug.Log("=====================================");
        }
    }

    private void Update()
    {
        // 处理快捷键
        if (isExperimentRunning)
        {
            HandleExperimentHotkeys();
        }

        // 调试模式快捷键（始终可用）
        if (debugModeEnabled)
        {
            HandleDebugHotkeys();
        }
    }

    #endregion

    #region 调试模式功能

    /// <summary>
    /// 处理调试快捷键
    /// </summary>
    private void HandleDebugHotkeys()
    {
        // Z - 生成时钟（不开始计时）
        if (Input.GetKeyDown(KeyCode.Z))
        {
            Debug_GenerateClocks();
        }

        // X - 开始/停止时间流动
        if (Input.GetKeyDown(KeyCode.X))
        {
            Debug_ToggleTimeFlow();
        }

        // C - 追加时钟
        if (Input.GetKeyDown(KeyCode.C))
        {
            Debug_AddMoreClocks();
        }

        // V - 清除所有时钟
        if (Input.GetKeyDown(KeyCode.V))
        {
            Debug_ClearAllClocks();
        }

        // B - 切换更新模式
        if (Input.GetKeyDown(KeyCode.B))
        {
            Debug_ToggleUpdateMode();
        }

        // N - 显示当前状态
        if (Input.GetKeyDown(KeyCode.N))
        {
            Debug_ShowCurrentStatus();
        }

        // M - 生成观测者
        if (Input.GetKeyDown(KeyCode.M))
        {
            Debug_GenerateObservers();
        }
        // G - 开始数据采集（当前模式10秒）
        if (Input.GetKeyDown(KeyCode.G))
        {
            Debug_StartDataCollection();
        }

        // F2 - 生成对比报告
        if (Input.GetKeyDown(KeyCode.F2))
        {
            if (dataCollector != null)
            {
                dataCollector.GenerateComparisonReport();
            }
        }

        // F3 - 清除所有数据
        if (Input.GetKeyDown(KeyCode.F3))
        {
            if (dataCollector != null)
            {
                dataCollector.ClearAllData();
            }
        }

        // F4 - 显示数据状态
        if (Input.GetKeyDown(KeyCode.F4))
        {
            if (dataCollector != null)
            {
                dataCollector.ShowDataStatus();
            }
        }
    }
    private void Debug_StartDataCollection()
    {
        if (!debugClocksGenerated)
        {
            Debug.LogWarning("[调试] 请先按Z生成时钟！");
            return;
        }

        if (!debugTimeStarted)
        {
            Debug.LogWarning("[调试] 请先按X开始时间流动！");
            return;
        }

        if (dataCollector == null)
        {
            Debug.LogError("[调试] DataCollector未设置！");
            return;
        }

        if (dataCollector.IsRecording)
        {
            Debug.LogWarning("[调试] 正在记录中，请等待完成");
            return;
        }

        Debug.Log($"\n[调试] 开始采集{currentMode}模式数据（10秒）...");
        dataCollector.StartCurrentModeRecording();
    }
    /// <summary>
    /// 调试：生成时钟（不开始计时）
    /// </summary>
    private void Debug_GenerateClocks()
    {
        Debug.Log($"\n[调试] 生成{debugClockCount}个时钟...");

        // 确保ObjectManager存在
        if (objectManager == null)
        {
            Debug.LogError("[调试] ObjectManager未设置！");
            return;
        }

        // 确保ObjectManager知道当前模式
        objectManager.SwitchMode(currentMode);

        // 生成时钟，从0秒开始
        objectManager.GenerateClocksWithTimeOffset(debugClockCount, 0);

        // 更新下一个时钟的起始时间
        debugNextClockStartTime = debugClockCount;
        debugClocksGenerated = true;

        // 获取统计信息
        var stats = objectManager.GetStats();

        Debug.Log($"[调试] 时钟生成完成！");
        Debug.Log($"- 总数: {stats.totalClocks}");
        Debug.Log($"- 时间范围: 00:00:00 到 {FormatTime(debugClockCount - 1)}");
        Debug.Log($"- 下一个时钟起始时间: {FormatTime(debugNextClockStartTime)}");
        Debug.Log($"- 当前模式: {currentMode}");
        Debug.Log($"- 时间流动: {(debugTimeStarted ? "已开始" : "未开始（等待按X）")}");
        Debug.Log($"注意：时钟已生成但处于待机状态，按X开始时间流动");
    }

    /// <summary>
    /// 调试：开始/停止时间流动
    /// </summary>
    private void Debug_ToggleTimeFlow()
    {
        if (!debugClocksGenerated)
        {
            Debug.LogWarning("[调试] 请先按Z生成时钟！");
            return;
        }

        if (!debugTimeStarted)
        {
            Debug.Log("\n[调试] 开始时间流动...");

            // 确保TimeManager存在
            if (timeManager == null)
            {
                Debug.LogError("[调试] TimeManager未设置！");
                return;
            }

            // 开始实验计时（这会设置主时间轴）
            timeManager.StartExperimentTimer();

            // 根据当前模式启动计时器
            if (currentMode == ExperimentMode.Traditional)
            {
                timeManager.StartTraditionalTimer();
                Debug.Log("[调试] 传统模式计时器已启动");
            }
            else
            {
                timeManager.StartLazyUpdateTimer();
                Debug.Log("[调试] 惰性模式计时器已启动");
            }

            // 通知所有时钟实验已开始
            NotifyAllClocksExperimentStarted();

            debugTimeStarted = true;
            isExperimentRunning = true;

            Debug.Log("[调试] 时间开始流动！所有时钟将根据主时间轴计时");
        }
        else
        {
            // 检查是否暂停或停止
            if (timeManager != null && timeManager.IsPaused)
            {
                Debug.Log("\n[调试] 恢复时间流动...");
                timeManager.Resume();
                Debug.Log("[调试] 时间已恢复！");
            }
            else
            {
                Debug.Log("\n[调试] 暂停时间流动...");

                // 暂停时间（不是停止）
                if (timeManager != null)
                {
                    timeManager.Pause();
                }

                Debug.Log("[调试] 时间已暂停！再次按X恢复");
            }
        }
    }

    /// <summary>
    /// 调试：追加更多时钟
    /// </summary>
    private void Debug_AddMoreClocks()
    {
        if (!debugClocksGenerated)
        {
            Debug.LogWarning("[调试] 请先按Z生成初始时钟！");
            return;
        }

        Debug.Log($"\n[调试] 追加{debugAddClockCount}个时钟...");
        Debug.Log($"- 起始时间: {FormatTime(debugNextClockStartTime)}");

        // 生成追加的时钟
        objectManager.GenerateClocksWithTimeOffset(debugAddClockCount, debugNextClockStartTime);

        // 如果实验正在运行，通知新时钟
        if (debugTimeStarted && timeManager != null && !timeManager.IsPaused)
        {
            // 获取新添加的时钟并通知它们
            var allClocks = objectManager.GetAllClockComponents();
            int startIndex = allClocks.Count - debugAddClockCount;

            for (int i = startIndex; i < allClocks.Count; i++)
            {
                if (i >= 0 && i < allClocks.Count && allClocks[i] != null)
                {
                    allClocks[i].ForceSetRunning(true);
                }
            }

            Debug.Log($"[调试] 已通知新时钟实验正在运行");
        }

        // 更新下一个时钟的起始时间
        debugNextClockStartTime += debugAddClockCount;

        // 获取统计信息
        var stats = objectManager.GetStats();

        Debug.Log($"[调试] 追加完成！");
        Debug.Log($"- 当前总数: {stats.totalClocks}");
        Debug.Log($"- 新时钟时间范围: {FormatTime(debugNextClockStartTime - debugAddClockCount)} 到 {FormatTime(debugNextClockStartTime - 1)}");
        Debug.Log($"- 下一个时钟起始时间: {FormatTime(debugNextClockStartTime)}");
    }

    /// <summary>
    /// 调试：清除所有时钟
    /// </summary>
    private void Debug_ClearAllClocks()
    {
        Debug.Log("\n[调试] 清除所有时钟...");

        // 如果时间正在流动，先停止
        if (debugTimeStarted)
        {
            // 停止时间
            if (timeManager != null)
            {
                timeManager.StopAllTimers();
                timeManager.ResetAllTimers();
            }

            debugTimeStarted = false;
            isExperimentRunning = false;
        }

        // 清除时钟
        if (objectManager != null)
        {
            objectManager.DestroyAllObjects();
        }

        // 重置状态
        debugClocksGenerated = false;
        debugNextClockStartTime = 0;

        Debug.Log("[调试] 所有时钟已清除！");
    }

    /// <summary>
    /// 调试：切换更新模式
    /// </summary>
    private void Debug_ToggleUpdateMode()
    {
        ExperimentMode newMode = (currentMode == ExperimentMode.Traditional) ?
            ExperimentMode.LazyUpdate : ExperimentMode.Traditional;

        Debug.Log($"\n[调试] 切换模式: {currentMode} → {newMode}");

        currentMode = newMode;

        // 通知ObjectManager切换模式
        if (objectManager != null)
        {
            objectManager.SwitchMode(currentMode);
        }

        // 如果时间正在流动，需要切换计时器
        if (debugTimeStarted && timeManager != null)
        {
            if (currentMode == ExperimentMode.Traditional)
            {
                timeManager.StopLazyUpdateTime();
                timeManager.StartTraditionalTimer();

                // 传统模式下，如果实验正在运行，通知时钟开始更新
                if (!timeManager.IsPaused)
                {
                    NotifyAllClocksExperimentStarted();
                }
            }
            else
            {
                timeManager.StopTraditionalTime();
                timeManager.StartLazyUpdateTimer();

                // 惰性模式下，时钟将等待观测
                Debug.Log("[调试] 切换到惰性模式，时钟等待观测");
            }
        }

        Debug.Log($"[调试] 当前模式: {currentMode}");
    }

    /// <summary>
    /// 调试：生成观测者
    /// </summary>
    private void Debug_GenerateObservers()
    {
        Debug.Log("\n[调试] 生成3个观测者...");

        if (observerManager == null)
        {
            Debug.LogError("[调试] ObserverManager未设置！");
            return;
        }
        int observersCount = observerManager.GetAllObservers().Count;
        if (observersCount > 0)
        {

            observerManager.AddObservers(3);
        }
        else
        {
            // 生成观测者
            observerManager.GenerateObservers(3, currentMode);

            Debug.Log("[调试] 观测者生成完成！");
        }
    }

    /// <summary>
    /// 调试：显示当前状态
    /// </summary>
    private void Debug_ShowCurrentStatus()
    {
        Debug.Log("\n========== 调试状态信息 ==========");
        Debug.Log($"时钟生成: {(debugClocksGenerated ? "是" : "否")}");
        Debug.Log($"时间流动: {(debugTimeStarted ? "是" : "否")}");

        if (timeManager != null && debugTimeStarted)
        {
            Debug.Log($"时间状态: {(timeManager.IsPaused ? "暂停" : "运行中")}");
        }

        Debug.Log($"当前模式: {currentMode}");
        Debug.Log($"下一个时钟起始时间: {FormatTime(debugNextClockStartTime)}");

        if (objectManager != null)
        {
            var stats = objectManager.GetStats();
            Debug.Log($"\n--- ObjectManager ---");
            Debug.Log($"总时钟数: {stats.totalClocks}");
            Debug.Log($"活跃时钟: {stats.activeClocks} ({stats.activeRatio:P1})");
        }

        if (timeManager != null && debugTimeStarted)
        {
            Debug.Log($"\n--- TimeManager ---");
            Debug.Log($"主时间: {timeManager.GetMainTime():F1}秒");
            Debug.Log($"传统模式累计: {timeManager.GetModeTotalTime(ExperimentMode.Traditional):F1}秒");
            Debug.Log($"惰性模式累计: {timeManager.GetModeTotalTime(ExperimentMode.LazyUpdate):F1}秒");
            Debug.Log($"是否暂停: {timeManager.IsPaused}");

            // 显示模式切换历史
            var switchHistory = timeManager.GetModeSwitchHistory();
            if (switchHistory != null && switchHistory.Count > 0)
            {
                Debug.Log($"模式切换记录: {switchHistory.Count}次");
                foreach (var record in switchHistory)
                {
                    Debug.Log($"  - 主时间 {record.mainTimeAtSwitch:F1}秒 → {record.toMode}");
                }
            }
        }

        if (observerManager != null)
        {
            Debug.Log($"\n--- ObserverManager ---");
            Debug.Log($"观测者数量: {observerManager.GetObserverCount()}");
        }

        if (performanceMonitor != null)
        {
            var metrics = performanceMonitor.GetCurrentMetrics();
            Debug.Log($"\n--- Performance ---");
            Debug.Log($"FPS: {metrics.currentFPS:F1}");
            Debug.Log($"帧时间: {metrics.frameTime:F1}ms");
        }

        Debug.Log("===================================\n");
    }

    #endregion

    #region 正式实验控制（由StateMachine调用）

    /// <summary>
    /// 启动实验（由StateMachine在进入Running状态时调用）
    /// </summary>
    public void StartExperiment()
    {
        if (isExperimentRunning) return;

        Debug.Log($"[ExperimentController] 启动正式实验 - 模式: {currentMode}");

        // 如果在调试模式下已经生成了时钟和开始了时间，需要先停止
        if (debugModeEnabled && debugTimeStarted)
        {
            Debug.LogWarning("[ExperimentController] 检测到调试模式正在运行，将先停止调试...");
            Debug_ToggleTimeFlow();
        }

        isExperimentRunning = true;

        // 启动时间管理
        timeManager.StartExperimentTimer();

        if (currentMode == ExperimentMode.Traditional)
        {
            timeManager.StartTraditionalTimer();
        }
        else
        {
            timeManager.StartLazyUpdateTimer();
        }

        // 根据当前模式配置对象
        var config = configurationManager.GetCurrentState();
        objectManager.SwitchMode(currentMode);

        // 在传统模式下，激活所有时钟的自主更新
        if (currentMode == ExperimentMode.Traditional)
        {
            EnableAllClocksUpdate();
        }

        // 重置相机
        if (cameraController)
        {
            cameraController.ResetCamera();
            cameraController.SetCameraMode(CameraController.CameraMode.ExternalObserver);
        }

        // 开始性能监控
        if (performanceMonitor)
        {
            performanceMonitor.StartMonitoring();
            performanceMonitor.MarkPerformanceEvent($"正式实验开始 - 模式: {currentMode}");
        }

        Debug.Log($"[ExperimentController] 正式实验启动完成");
    }

    /// <summary>
    /// 停止实验（由StateMachine在退出Running状态时调用）
    /// </summary>
    public void StopExperiment()
    {
        if (!isExperimentRunning) return;

        Debug.Log("[ExperimentController] 停止正式实验");
        isExperimentRunning = false;

        // 停止时间
        if (timeManager)
        {
            timeManager.StopAllTimers();
        }

        // 停止性能监控
        if (performanceMonitor)
        {
            performanceMonitor.StopMonitoring();
            performanceMonitor.MarkPerformanceEvent("正式实验结束");
        }

        // 禁用所有时钟更新
        DisableAllClocksUpdate();
    }

    /// <summary>
    /// 暂停实验
    /// </summary>
    public void PauseExperiment()
    {
        if (!isExperimentRunning) return;

        if (timeManager)
        {
            timeManager.Pause();
        }

        Debug.Log("[ExperimentController] 实验已暂停");
    }

    /// <summary>
    /// 恢复实验
    /// </summary>
    public void ResumeExperiment()
    {
        if (!isExperimentRunning) return;

        if (timeManager)
        {
            timeManager.Resume();
        }

        Debug.Log("[ExperimentController] 实验已恢复");
    }

    #endregion

    #region 模式切换

    /// <summary>
    /// 切换实验模式（正式实验中使用）
    /// </summary>
    public void SwitchMode()
    {
        if (!isExperimentRunning) return;

        // 记录切换前的数据
        if (uiManager)
        {
            uiManager.RecordModeSwitch(currentMode);
        }

        // 切换模式
        ExperimentMode newMode = (currentMode == ExperimentMode.Traditional) ?
            ExperimentMode.LazyUpdate : ExperimentMode.Traditional;

        Debug.Log($"--- 正式实验模式切换: {currentMode} → {newMode} ---");

        // 标记性能事件
        if (performanceMonitor)
        {
            performanceMonitor.MarkPerformanceEvent($"模式切换开始: {currentMode} → {newMode}");
        }

        // 停止当前模式的计时器
        if (currentMode == ExperimentMode.Traditional)
        {
            timeManager.StopTraditionalTime();
        }
        else
        {
            timeManager.StopLazyUpdateTime();
        }

        // 更新当前模式
        currentMode = newMode;

        // 启动新模式的计时器
        if (currentMode == ExperimentMode.Traditional)
        {
            timeManager.StartTraditionalTimer();
        }
        else
        {
            timeManager.StartLazyUpdateTimer();
        }

        objectManager.SwitchMode(currentMode);

        // 更新UI
        if (uiManager)
        {
            uiManager.SetModeDisplay(currentMode);
            uiManager.ShowModeSwitch(currentMode);
        }

        // 标记性能事件
        if (performanceMonitor)
        {
            performanceMonitor.MarkPerformanceEvent($"模式切换完成: 当前模式 {newMode}");
        }
    }

    /// <summary>
    /// 重置整个实验场景
    /// </summary>
    public void ResetExperiment()
    {
        if (isExperimentRunning)
        {
            StopExperiment();
        }

        Debug.Log("--- 实验重置 ---");

        // 销毁所有对象
        objectManager.DestroyAllObjects();
        observerManager.DestroyAllObservers();

        // 重置时间
        if (timeManager)
        {
            timeManager.ResetAllTimers();
        }

        // 清空UI
        if (uiManager)
        {
            uiManager.ClearDisplay();
        }

        // 重置调试状态
        debugClocksGenerated = false;
        debugTimeStarted = false;
        debugNextClockStartTime = 0;
    }

    #endregion

    #region 惰性更新核心逻辑

    /// <summary>
    /// 请求更新物体状态（由观测者调用）
    /// </summary>
    public void RequestStateUpdate(GameObject observedObject)
    {
        // 只在惰性模式下处理
        if (currentMode != ExperimentMode.LazyUpdate)
        {
            return;
        }

        if (observedObject == null) return;

        // 委托给ObservableManager处理
        if (observableManager != null)
        {
            float currentTime = timeManager.GetMainTime();
            observableManager.UpdateStateOnObserve(observedObject, currentTime);
        }
    }

    /// <summary>
    /// 批量请求更新（相机观测者模式）
    /// </summary>
    public void RequestBatchStateUpdate(List<GameObject> observedObjects)
    {
        if (currentMode != ExperimentMode.LazyUpdate)
        {
            return;
        }

        if (observableManager != null)
        {
            float currentTime = timeManager.GetMainTime();
            observableManager.BatchUpdateStateOnObserve(observedObjects, currentTime);
        }
    }

    #endregion

    #region 辅助功能

    /// <summary>
    /// 启用所有时钟的自主更新（传统模式）
    /// </summary>
    private void EnableAllClocksUpdate()
    {
        var stats = objectManager.GetStats();
        Debug.Log($"[ExperimentController] 启用{stats.totalClocks}个时钟的自主更新");

        // 通知所有时钟开始自主更新
        var allClocks = objectManager.GetAllClockComponents();
        foreach (var clock in allClocks)
        {
            if (clock != null)
            {
                clock.SetUpdateMode(Clock.UpdateMode.Traditional);
            }
        }
    }

    /// <summary>
    /// 禁用所有时钟的自主更新
    /// </summary>
    private void DisableAllClocksUpdate()
    {
        Debug.Log("[ExperimentController] 禁用所有时钟的自主更新");

        // 通知所有时钟停止自主更新
        var allClocks = objectManager.GetAllClockComponents();
        foreach (var clock in allClocks)
        {
            if (clock != null)
            {
                clock.SetUpdateMode(Clock.UpdateMode.Lazy);
            }
        }
    }

    /// <summary>
    /// 通知所有时钟实验已开始（调试用）
    /// </summary>
    private void NotifyAllClocksExperimentStarted()
    {
        if (objectManager == null) return;

        var allClocks = objectManager.GetAllClockComponents();
        Debug.Log($"[ExperimentController] 通知{allClocks.Count}个时钟实验已开始");

        foreach (var clock in allClocks)
        {
            if (clock != null)
            {
                // 强制设置时钟运行状态
                clock.ForceSetRunning(true);
            }
        }
    }

    /// <summary>
    /// 验证所有引用
    /// </summary>
    private void ValidateReferences()
    {
        List<string> missingReferences = new List<string>();

        if (timeManager == null) missingReferences.Add("TimeManager");
        if (objectManager == null) missingReferences.Add("ObjectManager");
        if (observerManager == null) missingReferences.Add("ObserverManager");

        if (missingReferences.Count > 0)
        {
            Debug.LogWarning($"[ExperimentController] 缺少以下引用: {string.Join(", ", missingReferences)}");
            Debug.LogWarning("某些功能可能无法正常工作，但调试模式仍可使用");
        }
    }

    /// <summary>
    /// 处理实验快捷键（正式实验中使用）
    /// </summary>
    private void HandleExperimentHotkeys()
    {
        // Tab - 切换模式
        if (Input.GetKeyDown(KeyCode.CapsLock))
        {
            SwitchMode();
        }

        // Space - 暂停/继续
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (stateMachine != null)
            {
                stateMachine.TogglePause();
            }
        }

        // F1 - 显示实验状态
        if (Input.GetKeyDown(KeyCode.F1))
        {
            LogExperimentStatus();
        }
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

    #endregion

    #region 属性访问器

    public bool IsRunning => isExperimentRunning;
    public ExperimentMode CurrentMode => currentMode;
    public bool IsDebugMode => debugModeEnabled;

    #endregion

    #region 调试

    /// <summary>
    /// 显示实验状态（正式实验）
    /// </summary>
    [ContextMenu("Log Experiment Status")]
    public void LogExperimentStatus()
    {
        Debug.Log("=== 正式实验状态 ===");
        Debug.Log($"运行状态: {(isExperimentRunning ? "运行中" : "未运行")}");
        Debug.Log($"当前模式: {currentMode}");
        if (timeManager != null)
        {
            var timeStats = timeManager.GetTimeStats();
            Debug.Log($"\n时间统计:");
            Debug.Log($"- 主时间: {timeStats.mainTime:F1}秒");
            Debug.Log($"- 传统模式: {timeStats.traditionalTime:F1}秒 ({timeStats.traditionalPercentage:F1}%)");
            Debug.Log($"- 惰性模式: {timeStats.lazyTime:F1}秒 ({timeStats.lazyPercentage:F1}%)");
            Debug.Log($"- 模式切换: {timeStats.switchCount}次");
        }
        Debug.Log($"调试模式: {(debugModeEnabled ? "启用" : "禁用")}");

        if (stateMachine != null)
        {
            Debug.Log($"状态机状态: {stateMachine.GetCurrentState()}");
        }

        if (configurationManager != null)
        {
            var config = configurationManager.GetCurrentState();
            Debug.Log($"已生成时钟: {config.totalClocksGenerated}");
            Debug.Log($"已生成观测者: {config.totalObserversGenerated}");
        }

        if (objectManager != null)
        {
            var stats = objectManager.GetStats();
            Debug.Log($"活跃时钟: {stats.activeClocks}/{stats.totalClocks} ({stats.activeRatio:P1})");
        }

        if (performanceMonitor != null)
        {
            var metrics = performanceMonitor.GetCurrentMetrics();
            Debug.Log($"当前FPS: {metrics.currentFPS:F1}");
            Debug.Log($"CPU使用率: {metrics.cpuUsage:F0}%");
        }

        Debug.Log("====================");
    }

    #endregion
}