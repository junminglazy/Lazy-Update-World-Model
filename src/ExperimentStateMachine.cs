using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 实验状态机 - 管理整个实验的状态流转
/// 确保实验按照正确的流程进行
/// </summary>
public class ExperimentStateMachine : MonoBehaviour
{
    #region 单例模式
    public static ExperimentStateMachine Instance { get; private set; }
    #endregion

    #region 状态定义

    /// <summary>
    /// 实验状态枚举
    /// </summary>
    public enum ExperimentState
    {
        Initialization,     // 初始化
        Configuration,      // 参数配置
        SceneGeneration,    // 场景生成
        Ready,              // 准备就绪
        Running,            // 实验运行中
        Paused,             // 暂停
        DataCollection      // 数据收集
    }

    #endregion

    #region 状态转换规则

    /// <summary>
    /// 状态转换规则
    /// </summary>
    private readonly Dictionary<ExperimentState, List<ExperimentState>> validTransitions = new Dictionary<ExperimentState, List<ExperimentState>>
    {
        { ExperimentState.Initialization, new List<ExperimentState> { ExperimentState.Configuration } },
        { ExperimentState.Configuration, new List<ExperimentState> { ExperimentState.SceneGeneration } },
        { ExperimentState.SceneGeneration, new List<ExperimentState> { ExperimentState.Ready } },
        { ExperimentState.Ready, new List<ExperimentState> { ExperimentState.Configuration, ExperimentState.Running } },
        { ExperimentState.Running, new List<ExperimentState> { ExperimentState.Paused, ExperimentState.DataCollection } },
        { ExperimentState.Paused, new List<ExperimentState> { ExperimentState.Running, ExperimentState.DataCollection } },
        { ExperimentState.DataCollection, new List<ExperimentState> { ExperimentState.Configuration } }
    };

    #endregion

    #region 状态与事件

    [Header("当前状态")]
    [SerializeField] private ExperimentState currentState = ExperimentState.Initialization;
    [SerializeField] private ExperimentState previousState = ExperimentState.Initialization;
    [SerializeField] private float stateEnterTime = 0f;

    [Header("状态历史")]
    [SerializeField] private List<StateTransition> stateHistory = new List<StateTransition>();

    /// <summary>
    /// 状态转换记录
    /// </summary>
    [System.Serializable]
    public class StateTransition
    {
        public ExperimentState fromState;
        public ExperimentState toState;
        public float timestamp;
        public string reason;
    }

    [Header("状态事件")]
    public UnityEvent<ExperimentState, ExperimentState> OnStateChanged;
    public UnityEvent OnConfigurationEntered;
    public UnityEvent OnReadyEntered;
    public UnityEvent OnRunningEntered;
    public UnityEvent OnPausedEntered;
    public UnityEvent OnDataCollectionEntered;

    #endregion

    #region 系统引用

    [Header("系统引用")]
    [SerializeField] private UIManager uiManager;
    [SerializeField] private ConfigurationManager configurationManager;
    [SerializeField] private ExperimentController experimentController;
    [SerializeField] private PerformanceMonitor performanceMonitor;

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
        // 自动开始初始化
        StartCoroutine(InitializationSequence());
    }

    #endregion

    #region 状态转换

    /// <summary>
    /// 尝试转换到指定状态
    /// </summary>
    public bool TransitionTo(ExperimentState newState, string reason = "")
    {
        // 检查转换是否有效
        if (!IsTransitionValid(currentState, newState))
        {
            Debug.LogWarning($"[StateMachine] 无效的状态转换: {currentState} → {newState}");
            return false;
        }

        // 记录转换
        StateTransition transition = new StateTransition
        {
            fromState = currentState,
            toState = newState,
            timestamp = Time.time,
            reason = reason
        };
        stateHistory.Add(transition);

        // 执行退出逻辑
        OnStateExit(currentState);

        // 更新状态
        previousState = currentState;
        currentState = newState;
        stateEnterTime = Time.time;

        Debug.Log($"[StateMachine] 状态转换: {previousState} → {currentState} ({reason})");

        // 触发状态改变事件
        OnStateChanged?.Invoke(previousState, currentState);

        // 执行进入逻辑
        OnStateEnter(currentState);

        return true;
    }

    /// <summary>
    /// 检查状态转换是否有效
    /// </summary>
    private bool IsTransitionValid(ExperimentState from, ExperimentState to)
    {
        if (validTransitions.TryGetValue(from, out List<ExperimentState> validStates))
        {
            return validStates.Contains(to);
        }
        return false;
    }

    #endregion

    #region 状态逻辑

    /// <summary>
    /// 状态进入逻辑
    /// </summary>
    private void OnStateEnter(ExperimentState state)
    {
        switch (state)
        {
            case ExperimentState.Configuration:
                EnterConfiguration();
                break;

            case ExperimentState.SceneGeneration:
                EnterSceneGeneration();
                break;

            case ExperimentState.Ready:
                EnterReady();
                break;

            case ExperimentState.Running:
                EnterRunning();
                break;

            case ExperimentState.Paused:
                EnterPaused();
                break;

            case ExperimentState.DataCollection:
                EnterDataCollection();
                break;
        }
    }

    /// <summary>
    /// 状态退出逻辑
    /// </summary>
    private void OnStateExit(ExperimentState state)
    {
        switch (state)
        {
            case ExperimentState.Configuration:
                ExitConfiguration();
                break;

            case ExperimentState.Running:
                ExitRunning();
                break;
        }
    }

    #endregion

    #region 各状态的具体逻辑

    /// <summary>
    /// 初始化序列
    /// </summary>
    private IEnumerator InitializationSequence()
    {
        Debug.Log("[StateMachine] 开始初始化序列");

        // 等待所有管理器准备就绪
        yield return new WaitForSeconds(0.5f);

        // 自动转到配置状态
        TransitionTo(ExperimentState.Configuration, "初始化完成");
    }

    /// <summary>
    /// 进入配置状态
    /// </summary>
    private void EnterConfiguration()
    {
        Debug.Log("[StateMachine] 进入配置状态");

        // 解锁配置
        if (configurationManager != null)
        {
            configurationManager.SetConfigurationLock(false);
        }

        // 显示配置UI
        if (uiManager != null)
        {
            uiManager.ShowConfigurationPanel();
        }

        OnConfigurationEntered?.Invoke();
    }

    /// <summary>
    /// 退出配置状态
    /// </summary>
    private void ExitConfiguration()
    {
        // 隐藏配置UI
        if (uiManager != null)
        {
            uiManager.HideConfigurationPanel();
        }
    }

    /// <summary>
    /// 进入场景生成状态
    /// </summary>
    private void EnterSceneGeneration()
    {
        Debug.Log("[StateMachine] 进入场景生成状态");

        // 显示生成进度
        if (uiManager != null)
        {
            uiManager.ShowGenerationProgress();
        }

        // 生成完成后自动转到Ready
        StartCoroutine(GenerationComplete());
    }

    /// <summary>
    /// 生成完成协程
    /// </summary>
    private IEnumerator GenerationComplete()
    {
        // 等待生成完成
        yield return new WaitForSeconds(0.5f);

        // 转到准备就绪状态
        TransitionTo(ExperimentState.Ready, "场景生成完成");
    }

    /// <summary>
    /// 进入准备就绪状态
    /// </summary>
    private void EnterReady()
    {
        Debug.Log("[StateMachine] 进入准备就绪状态");

        // 显示Ready面板
        if (uiManager != null)
        {
            uiManager.ShowReadyPanel();
        }

        OnReadyEntered?.Invoke();
    }

    /// <summary>
    /// 进入运行状态
    /// </summary>
    private void EnterRunning()
    {
        Debug.Log("[StateMachine] 进入运行状态");

        // 锁定配置
        if (configurationManager != null)
        {
            configurationManager.SetConfigurationLock(true);
        }

        // 开始实验
        if (experimentController != null)
        {
            experimentController.StartExperiment();
        }

        // 显示控制面板
        if (uiManager != null)
        {
            uiManager.ShowControlPanel();
        }

        OnRunningEntered?.Invoke();
    }

    /// <summary>
    /// 退出运行状态
    /// </summary>
    private void ExitRunning()
    {
        // 停止性能监控
        if (performanceMonitor != null)
        {
            performanceMonitor.StopMonitoring();
        }
    }

    /// <summary>
    /// 进入暂停状态
    /// </summary>
    private void EnterPaused()
    {
        Debug.Log("[StateMachine] 进入暂停状态");

        // 暂停时间
        Time.timeScale = 0f;

        // 显示暂停UI
        if (uiManager != null)
        {
            uiManager.ShowPauseOverlay();
        }

        OnPausedEntered?.Invoke();
    }

    /// <summary>
    /// 进入数据收集状态
    /// </summary>
    private void EnterDataCollection()
    {
        Debug.Log("[StateMachine] 进入数据收集状态");

        // 停止实验
        if (experimentController != null)
        {
            experimentController.StopExperiment();
        }

        // 显示结果面板
        if (uiManager != null)
        {
            uiManager.ShowResultsPanel();
        }

        OnDataCollectionEntered?.Invoke();
    }

    #endregion

    #region 公共接口

    /// <summary>
    /// 获取当前状态
    /// </summary>
    public ExperimentState GetCurrentState()
    {
        return currentState;
    }

    /// <summary>
    /// 获取状态持续时间
    /// </summary>
    public float GetStateElapsedTime()
    {
        return Time.time - stateEnterTime;
    }

    /// <summary>
    /// 检查是否可以转换到指定状态
    /// </summary>
    public bool CanTransitionTo(ExperimentState state)
    {
        return IsTransitionValid(currentState, state);
    }

    /// <summary>
    /// 获取状态历史
    /// </summary>
    public List<StateTransition> GetStateHistory()
    {
        return new List<StateTransition>(stateHistory);
    }

    #endregion

    #region 便捷方法

    /// <summary>
    /// 配置完成（由ConfigurationManager调用）
    /// </summary>
    public void OnConfigurationCompleted()
    {
        if (currentState == ExperimentState.Configuration)
        {
            TransitionTo(ExperimentState.SceneGeneration, "配置完成");
        }
    }

    /// <summary>
    /// 开始实验（由UI调用）
    /// </summary>
    public void StartExperiment()
    {
        if (currentState == ExperimentState.Ready)
        {
            TransitionTo(ExperimentState.Running, "用户开始实验");
        }
    }

    /// <summary>
    /// 暂停/继续实验
    /// </summary>
    public void TogglePause()
    {
        if (currentState == ExperimentState.Running)
        {
            TransitionTo(ExperimentState.Paused, "用户暂停");
        }
        else if (currentState == ExperimentState.Paused)
        {
            Time.timeScale = 1f;  // 恢复时间
            TransitionTo(ExperimentState.Running, "用户继续");
        }
    }

    /// <summary>
    /// 结束实验
    /// </summary>
    public void EndExperiment()
    {
        if (currentState == ExperimentState.Running || currentState == ExperimentState.Paused)
        {
            if (currentState == ExperimentState.Paused)
            {
                Time.timeScale = 1f;  // 恢复时间
            }
            TransitionTo(ExperimentState.DataCollection, "用户结束实验");
        }
    }

    /// <summary>
    /// 重新配置
    /// </summary>
    public void Reconfigure()
    {
        if (currentState == ExperimentState.Ready)
        {
            TransitionTo(ExperimentState.Configuration, "用户重新配置");
        }
    }

    /// <summary>
    /// 重置整个实验
    /// </summary>
    public void ResetExperiment()
    {
        if (currentState == ExperimentState.DataCollection)
        {
            // 清理数据
            if (configurationManager != null)
            {
                configurationManager.ResetConfiguration();
            }

            // 返回配置状态
            TransitionTo(ExperimentState.Configuration, "实验重置");
        }
    }

    #endregion

    #region 调试

    /// <summary>
    /// 打印状态信息
    /// </summary>
    [ContextMenu("Log State Info")]
    public void LogStateInfo()
    {
        Debug.Log($"[StateMachine] 当前状态: {currentState}");
        Debug.Log($"[StateMachine] 前一状态: {previousState}");
        Debug.Log($"[StateMachine] 状态持续时间: {GetStateElapsedTime():F1}秒");
        Debug.Log($"[StateMachine] 状态历史记录: {stateHistory.Count}条");

        if (stateHistory.Count > 0)
        {
            Debug.Log("最近的状态转换:");
            int startIndex = Mathf.Max(0, stateHistory.Count - 5);
            for (int i = startIndex; i < stateHistory.Count; i++)
            {
                var transition = stateHistory[i];
                Debug.Log($"  {transition.fromState} → {transition.toState} ({transition.reason}) at {transition.timestamp:F1}s");
            }
        }
    }

    #endregion
}