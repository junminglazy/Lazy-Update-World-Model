using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 时间管理器 - 管理整个实验的时间系统
/// 核心功能：
/// 1. 维护主时间轴（实验总时间）
/// 2. 分别记录两种模式的累计时长
/// 3. 记录模式切换历史
/// </summary>
public class TimeManager : MonoBehaviour
{
    #region 单例模式
    public static TimeManager Instance { get; private set; }
    #endregion

    #region 时间状态

    [Header("=== 主时间轴 ===")]
    [Tooltip("实验是否正在运行")]
    [SerializeField] private bool isExperimentRunning = false;

    [Tooltip("实验是否已暂停")]
    [SerializeField] private bool isPaused = false;

    [Tooltip("主时间轴的当前时间（秒）")]
    [SerializeField] private float mainTime = 0f;

    [Tooltip("上一帧的真实时间（用于计算增量）")]
    private float lastFrameTime = 0f;

    [Header("=== 模式时间记录 ===")]
    [Tooltip("传统模式累计运行时长（秒）")]
    [SerializeField] private float traditionalModeTotalTime = 0f;

    [Tooltip("惰性模式累计运行时长（秒）")]
    [SerializeField] private float lazyModeTotalTime = 0f;

    [Tooltip("当前正在运行的模式")]
    [SerializeField] private ExperimentController.ExperimentMode? currentActiveMode = null;

    [Tooltip("当前模式开始时的主时间点")]
    [SerializeField] private float currentModeStartTime = 0f;

    [Header("=== 模式切换历史 ===")]
    [Tooltip("模式切换记录")]
    [SerializeField] private List<ModeSwitchRecord> modeSwitchHistory = new List<ModeSwitchRecord>();

    // 事件：用于通知其他系统时间状态的变化
    public event Action<float> OnMainTimeUpdate;
    public event Action<bool> OnPauseStateChanged;
    public event Action<ExperimentController.ExperimentMode, float> OnModeSwitch;

    #endregion

    #region Unity生命周期

    private void Awake()
    {
        // 单例初始化
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    private void Update()
    {
        // 只有当实验运行中且未暂停时才更新
        if (isExperimentRunning && !isPaused)
        {
            // 计算时间增量
            float currentTime = Time.time;
            float deltaTime = currentTime - lastFrameTime;
            lastFrameTime = currentTime;

            // 更新主时间
            mainTime += deltaTime;

            // 如果有模式正在运行，更新该模式的累计时间
            if (currentActiveMode.HasValue)
            {
                if (currentActiveMode.Value == ExperimentController.ExperimentMode.Traditional)
                {
                    traditionalModeTotalTime += deltaTime;
                }
                else
                {
                    lazyModeTotalTime += deltaTime;
                }
            }

            // 触发时间更新事件
            OnMainTimeUpdate?.Invoke(mainTime);
        }
    }

    #endregion

    #region 公开接口

    /// <summary>
    /// 开始整个实验的计时
    /// </summary>
    public void StartExperimentTimer()
    {
        isExperimentRunning = true;
        isPaused = false;
        mainTime = 0f;
        lastFrameTime = Time.time;

        // 重置模式时间
        traditionalModeTotalTime = 0f;
        lazyModeTotalTime = 0f;
        currentActiveMode = null;
        currentModeStartTime = 0f;

        // 清空历史记录
        modeSwitchHistory.Clear();

        Debug.Log($"[TimeManager] 实验计时开始");
    }

    /// <summary>
    /// 启动传统模式计时
    /// </summary>
    public void StartTraditionalTimer()
    {
        if (!isExperimentRunning)
        {
            Debug.LogError("[TimeManager] 尝试启动传统模式计时，但实验尚未开始！");
            return;
        }

        // 如果当前已经在传统模式，不需要切换
        if (currentActiveMode == ExperimentController.ExperimentMode.Traditional)
        {
            Debug.Log("[TimeManager] 已经在传统模式中");
            return;
        }

        // 如果之前有其他模式在运行，先结算它
        if (currentActiveMode.HasValue)
        {
            float previousModeDuration = mainTime - currentModeStartTime;
            Debug.Log($"[TimeManager] {currentActiveMode.Value}模式结束 - 本次运行: {previousModeDuration:F2}秒");
        }

        // 记录模式切换
        RecordModeSwitch(ExperimentController.ExperimentMode.Traditional, mainTime);

        // 开始新模式计时
        currentActiveMode = ExperimentController.ExperimentMode.Traditional;
        currentModeStartTime = mainTime;

        Debug.Log($"[TimeManager] 传统模式开始 - 主时间: {mainTime:F2}秒，之前累计: {traditionalModeTotalTime:F2}秒");
    }

    /// <summary>
    /// 启动惰性更新模式计时
    /// </summary>
    public void StartLazyUpdateTimer()
    {
        if (!isExperimentRunning)
        {
            Debug.LogError("[TimeManager] 尝试启动惰性模式计时，但实验尚未开始！");
            return;
        }

        // 如果当前已经在惰性模式，不需要切换
        if (currentActiveMode == ExperimentController.ExperimentMode.LazyUpdate)
        {
            Debug.Log("[TimeManager] 已经在惰性模式中");
            return;
        }

        // 如果之前有其他模式在运行，先结算它
        if (currentActiveMode.HasValue)
        {
            float previousModeDuration = mainTime - currentModeStartTime;
            Debug.Log($"[TimeManager] {currentActiveMode.Value}模式结束 - 本次运行: {previousModeDuration:F2}秒");
        }

        // 记录模式切换
        RecordModeSwitch(ExperimentController.ExperimentMode.LazyUpdate, mainTime);

        // 开始新模式计时
        currentActiveMode = ExperimentController.ExperimentMode.LazyUpdate;
        currentModeStartTime = mainTime;

        Debug.Log($"[TimeManager] 惰性模式开始 - 主时间: {mainTime:F2}秒，之前累计: {lazyModeTotalTime:F2}秒");
    }

    /// <summary>
    /// 停止传统模式计时（公开接口，用于兼容）
    /// </summary>
    public void StopTraditionalTime()
    {
        // 这个方法现在只是为了接口兼容，实际的停止逻辑在Start新模式时自动处理
        if (currentActiveMode == ExperimentController.ExperimentMode.Traditional)
        {
            float duration = mainTime - currentModeStartTime;
            Debug.Log($"[TimeManager] 传统模式即将结束 - 本次运行: {duration:F2}秒");
        }
    }

    /// <summary>
    /// 停止惰性模式计时（公开接口，用于兼容）
    /// </summary>
    public void StopLazyUpdateTime()
    {
        // 这个方法现在只是为了接口兼容，实际的停止逻辑在Start新模式时自动处理
        if (currentActiveMode == ExperimentController.ExperimentMode.LazyUpdate)
        {
            float duration = mainTime - currentModeStartTime;
            Debug.Log($"[TimeManager] 惰性模式即将结束 - 本次运行: {duration:F2}秒");
        }
    }

    /// <summary>
    /// 获取当前主时间
    /// </summary>
    public float GetMainTime()
    {
        return mainTime;
    }

    /// <summary>
    /// 获取总实验时间（兼容旧接口）
    /// </summary>
    public float GetTotalExperimentTime()
    {
        return mainTime;
    }

    /// <summary>
    /// 获取指定模式的累计运行时间
    /// </summary>
    public float GetModeTotalTime(ExperimentController.ExperimentMode mode)
    {
        switch (mode)
        {
            case ExperimentController.ExperimentMode.Traditional:
                return traditionalModeTotalTime;
            case ExperimentController.ExperimentMode.LazyUpdate:
                return lazyModeTotalTime;
            default:
                return 0f;
        }
    }

    /// <summary>
    /// 获取当前模式的运行时间（兼容旧接口）
    /// </summary>
    public float GetCurrentTime(ExperimentController.ExperimentMode mode)
    {
        // 返回该模式的累计时间
        return GetModeTotalTime(mode);
    }

    /// <summary>
    /// 获取当前模式本次运行的时长
    /// </summary>
    public float GetCurrentModeSessionTime()
    {
        if (currentActiveMode.HasValue && isExperimentRunning)
        {
            return mainTime - currentModeStartTime;
        }
        return 0f;
    }

    /// <summary>
    /// 暂停计时
    /// </summary>
    public void Pause()
    {
        if (!isPaused && isExperimentRunning)
        {
            isPaused = true;
            OnPauseStateChanged?.Invoke(true);

            Debug.Log($"[TimeManager] 实验暂停 - 主时间: {mainTime:F2}秒");

            if (currentActiveMode.HasValue)
            {
                float sessionTime = mainTime - currentModeStartTime;
                Debug.Log($"- 当前模式: {currentActiveMode.Value} (本次已运行: {sessionTime:F2}秒)");
            }

            Debug.Log($"- 传统模式累计: {traditionalModeTotalTime:F2}秒");
            Debug.Log($"- 惰性模式累计: {lazyModeTotalTime:F2}秒");
        }
    }

    /// <summary>
    /// 恢复计时
    /// </summary>
    public void Resume()
    {
        if (isPaused && isExperimentRunning)
        {
            isPaused = false;
            lastFrameTime = Time.time; // 重置帧时间，避免跳变
            OnPauseStateChanged?.Invoke(false);

            Debug.Log($"[TimeManager] 实验恢复 - 从 {mainTime:F2}秒 继续");
        }
    }

    /// <summary>
    /// 切换暂停状态
    /// </summary>
    public void TogglePause()
    {
        if (isPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    /// <summary>
    /// 停止所有计时器
    /// </summary>
    public void StopAllTimers()
    {
        // 如果有模式在运行，先结算最后的时间
        if (currentActiveMode.HasValue)
        {
            float lastSessionTime = mainTime - currentModeStartTime;
            Debug.Log($"[TimeManager] {currentActiveMode.Value}模式结束 - 最后运行: {lastSessionTime:F2}秒");
        }

        isExperimentRunning = false;
        currentActiveMode = null;

        Debug.Log($"[TimeManager] 实验结束");
        Debug.Log($"- 总时长: {mainTime:F2}秒");
        Debug.Log($"- 传统模式: {traditionalModeTotalTime:F2}秒 ({(mainTime > 0 ? traditionalModeTotalTime / mainTime * 100 : 0):F1}%)");
        Debug.Log($"- 惰性模式: {lazyModeTotalTime:F2}秒 ({(mainTime > 0 ? lazyModeTotalTime / mainTime * 100 : 0):F1}%)");

        // 显示切换历史
        if (modeSwitchHistory.Count > 0)
        {
            Debug.Log($"- 模式切换: {modeSwitchHistory.Count}次");
        }
    }

    /// <summary>
    /// 重置所有计时器
    /// </summary>
    public void ResetAllTimers()
    {
        isExperimentRunning = false;
        isPaused = false;
        mainTime = 0f;
        traditionalModeTotalTime = 0f;
        lazyModeTotalTime = 0f;
        currentActiveMode = null;
        currentModeStartTime = 0f;
        modeSwitchHistory.Clear();

        Debug.Log("[TimeManager] 所有计时器已重置");
    }

    #endregion

    #region 模式切换记录

    /// <summary>
    /// 记录模式切换
    /// </summary>
    private void RecordModeSwitch(ExperimentController.ExperimentMode newMode, float switchMainTime)
    {
        ModeSwitchRecord record = new ModeSwitchRecord
        {
            toMode = newMode,
            mainTimeAtSwitch = switchMainTime,
            realTimeStamp = DateTime.Now
        };

        modeSwitchHistory.Add(record);

        // 触发模式切换事件
        OnModeSwitch?.Invoke(newMode, switchMainTime);
    }

    /// <summary>
    /// 获取模式切换历史
    /// </summary>
    public List<ModeSwitchRecord> GetModeSwitchHistory()
    {
        return new List<ModeSwitchRecord>(modeSwitchHistory);
    }

    #endregion

    #region 状态查询

    /// <summary>
    /// 获取实验统计信息
    /// </summary>
    public ExperimentTimeStats GetTimeStats()
    {
        ExperimentTimeStats stats = new ExperimentTimeStats();
        stats.mainTime = mainTime;
        stats.traditionalTime = traditionalModeTotalTime;
        stats.lazyTime = lazyModeTotalTime;
        stats.traditionalPercentage = mainTime > 0 ? (traditionalModeTotalTime / mainTime * 100) : 0;
        stats.lazyPercentage = mainTime > 0 ? (lazyModeTotalTime / mainTime * 100) : 0;
        stats.isPaused = isPaused;
        stats.currentMode = currentActiveMode ?? ExperimentController.ExperimentMode.Traditional;
        stats.hasActiveMode = currentActiveMode.HasValue;
        stats.currentSessionTime = GetCurrentModeSessionTime();
        stats.switchCount = modeSwitchHistory.Count;

        return stats;
    }

    /// <summary>
    /// 格式化时间显示（转换为 MM:SS 格式）
    /// </summary>
    public static string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        return $"{minutes:D2}:{secs:D2}";
    }

    #endregion

    #region 属性访问器

    public bool IsPaused => isPaused;
    public bool IsExperimentRunning => isExperimentRunning;
    public ExperimentController.ExperimentMode? ActiveMode => currentActiveMode;
    public float MainTime => mainTime;
    public float TraditionalTime => traditionalModeTotalTime;
    public float LazyTime => lazyModeTotalTime;

    #endregion

    #region 调试

    /// <summary>
    /// 在Inspector中显示当前状态
    /// </summary>
    [ContextMenu("显示时间状态")]
    private void ShowTimeStatus()
    {
        Debug.Log("=== TimeManager 状态 ===");
        Debug.Log($"实验运行: {isExperimentRunning}");
        Debug.Log($"是否暂停: {isPaused}");
        Debug.Log($"主时间: {mainTime:F2}秒");

        if (currentActiveMode.HasValue)
        {
            Debug.Log($"当前模式: {currentActiveMode.Value}");
            Debug.Log($"本次会话: {GetCurrentModeSessionTime():F2}秒");
        }
        else
        {
            Debug.Log("当前模式: 无");
        }

        Debug.Log($"传统模式累计: {traditionalModeTotalTime:F2}秒");
        Debug.Log($"惰性模式累计: {lazyModeTotalTime:F2}秒");
        Debug.Log($"切换次数: {modeSwitchHistory.Count}");
        Debug.Log("=====================");
    }

    #endregion
}

/// <summary>
/// 模式切换记录
/// </summary>
[System.Serializable]
public class ModeSwitchRecord
{
    public ExperimentController.ExperimentMode toMode;
    public float mainTimeAtSwitch;
    public DateTime realTimeStamp;
}

/// <summary>
/// 实验时间统计数据
/// </summary>
[System.Serializable]
public class ExperimentTimeStats
{
    public float mainTime;
    public float traditionalTime;
    public float lazyTime;
    public float traditionalPercentage;
    public float lazyPercentage;
    public bool isPaused;
    public ExperimentController.ExperimentMode currentMode;
    public bool hasActiveMode;
    public float currentSessionTime;
    public int switchCount;
}