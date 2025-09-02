using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 配置管理器 - 管理实验参数配置和批次生成
/// 确保时钟时间的连续性，支持多次配置累加
/// </summary>
public class ConfigurationManager : MonoBehaviour
{
    #region 单例模式
    public static ConfigurationManager Instance { get; private set; }
    #endregion

    #region 配置状态

    /// <summary>
    /// 当前配置状态
    /// </summary>
    [System.Serializable]
    public class ConfigurationState
    {
        public int totalClocksGenerated = 0;      // 已生成的时钟总数
        public int nextClockStartTime = 0;        // 下一个时钟的起始时间（秒）
        public int totalObserversGenerated = 0;   // 已生成的观测者总数
        public bool isLocked = false;             // 配置是否锁定（实验开始后锁定）
        public List<GenerationBatch> batches = new List<GenerationBatch>(); // 生成批次历史
    }

    /// <summary>
    /// 生成批次记录
    /// </summary>
    [System.Serializable]
    public class GenerationBatch
    {
        public int batchId;                   // 批次ID
        public float timestamp;               // 生成时间戳
        public int clockCount;                // 本批次时钟数量
        public int observerCount;             // 本批次观测者数量
        public int startTimeIndex;            // 起始时间索引（秒）
        public int endTimeIndex;              // 结束时间索引（秒）
        public string timeRange;              // 时间范围字符串（用于显示）
    }

    /// <summary>
    /// 预设配置
    /// </summary>
    [System.Serializable]
    public class PresetConfiguration
    {
        public string name;
        public int clockCount;
        public int observerCount;
        public string description;
    }

    #endregion

    #region 配置数据

    [Header("当前配置状态")]
    [SerializeField] private ConfigurationState currentState = new ConfigurationState();

    [Header("预设配置")]
    [SerializeField]
    private List<PresetConfiguration> presets = new List<PresetConfiguration>
    {
        new PresetConfiguration { name = "轻量级", clockCount = 100, observerCount = 1, description = "适合低端PC" },
        new PresetConfiguration { name = "标准", clockCount = 1000, observerCount = 1, description = "推荐配置" },
        new PresetConfiguration { name = "压力测试", clockCount = 5000, observerCount = 3, description = "适合高端PC" }
    };

    [Header("配置限制")]
    [SerializeField] private int maxClockCount = 10000;
    [SerializeField] private int maxObserverCount = 10;
    [SerializeField] private float defaultSpacing = 2.5f;

    [Header("系统引用")]
    [SerializeField] private ObjectManager objectManager;
    [SerializeField] private ObserverManager observerManager;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private ExperimentStateMachine stateMachine;

    #endregion

    #region 内部变量

    private int nextBatchId = 0;
    private bool isGenerating = false;

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
        // 初始化显示
        UpdateUIDisplay();
    }

    #endregion

    #region 配置操作

    /// <summary>
    /// 应用预设配置
    /// </summary>
    public void ApplyPreset(int presetIndex)
    {
        if (currentState.isLocked)
        {
            Debug.LogWarning("[ConfigurationManager] 实验运行中，无法修改配置");
            return;
        }

        if (presetIndex >= 0 && presetIndex < presets.Count)
        {
            var preset = presets[presetIndex];

            // 如果是首次配置，直接应用
            if (currentState.totalClocksGenerated == 0)
            {
                ApplyConfiguration(preset.clockCount, preset.observerCount);
            }
            else
            {
                // 否则显示追加确认
                if (uiManager != null)
                {
                    uiManager.ShowAddConfirmation(preset.clockCount, preset.observerCount, currentState.nextClockStartTime);
                }
            }
        }
    }

    /// <summary>
    /// 应用自定义配置
    /// </summary>
    public void ApplyCustomConfiguration(int clockCount, int observerCount)
    {
        if (currentState.isLocked)
        {
            Debug.LogWarning("[ConfigurationManager] 实验运行中，无法修改配置");
            return;
        }

        // 验证参数
        clockCount = Mathf.Clamp(clockCount, 1, maxClockCount - currentState.totalClocksGenerated);
        observerCount = Mathf.Clamp(observerCount, 0, maxObserverCount - currentState.totalObserversGenerated);

        ApplyConfiguration(clockCount, observerCount);
    }

    /// <summary>
    /// 应用配置（核心方法）
    /// </summary>
    private void ApplyConfiguration(int clockCount, int observerCount)
    {
        if (isGenerating) return;

        isGenerating = true;

        // 创建批次记录
        GenerationBatch batch = new GenerationBatch
        {
            batchId = nextBatchId++,
            timestamp = Time.time,
            clockCount = clockCount,
            observerCount = observerCount,
            startTimeIndex = currentState.nextClockStartTime,
            endTimeIndex = currentState.nextClockStartTime + clockCount - 1
        };

        // 计算时间范围
        batch.timeRange = $"{FormatTime(batch.startTimeIndex)} - {FormatTime(batch.endTimeIndex)}";

        Debug.Log($"[ConfigurationManager] 生成批次 #{batch.batchId}: {clockCount}个时钟, 时间范围: {batch.timeRange}");

        // 通知ObjectManager生成时钟
        if (objectManager != null)
        {
            objectManager.GenerateClocksWithTimeOffset(clockCount, currentState.nextClockStartTime);
        }

        // 通知ObserverManager生成观测者
        if (observerManager != null && observerCount > 0)
        {
            observerManager.AddObservers(observerCount);
        }

        // 更新状态
        currentState.totalClocksGenerated += clockCount;
        currentState.nextClockStartTime += clockCount;
        currentState.totalObserversGenerated += observerCount;
        currentState.batches.Add(batch);

        // 更新UI显示
        UpdateUIDisplay();

        // 转换到Ready状态
        if (stateMachine != null)
        {
            stateMachine.TransitionTo(ExperimentStateMachine.ExperimentState.Ready);
        }

        isGenerating = false;
    }

    /// <summary>
    /// 清空配置（重置）
    /// </summary>
    public void ResetConfiguration()
    {
        if (currentState.isLocked)
        {
            Debug.LogWarning("[ConfigurationManager] 实验运行中，无法重置配置");
            return;
        }

        // 清空所有对象
        if (objectManager != null)
        {
            objectManager.DestroyAllObjects();
        }

        if (observerManager != null)
        {
            observerManager.DestroyAllObservers();
        }

        // 重置状态
        currentState = new ConfigurationState();
        nextBatchId = 0;

        // 更新UI
        UpdateUIDisplay();

        Debug.Log("[ConfigurationManager] 配置已重置");
    }

    #endregion

    #region 状态查询

    /// <summary>
    /// 获取当前配置状态
    /// </summary>
    public ConfigurationState GetCurrentState()
    {
        return currentState;
    }

    /// <summary>
    /// 获取下一个时钟的起始时间
    /// </summary>
    public int GetNextClockStartTime()
    {
        return currentState.nextClockStartTime;
    }

    /// <summary>
    /// 获取已生成的时钟总数
    /// </summary>
    public int GetTotalClocksGenerated()
    {
        return currentState.totalClocksGenerated;
    }

    /// <summary>
    /// 检查是否可以继续添加
    /// </summary>
    public bool CanAddMoreClocks(int count)
    {
        return !currentState.isLocked && (currentState.totalClocksGenerated + count <= maxClockCount);
    }

    /// <summary>
    /// 锁定/解锁配置（实验开始/结束时调用）
    /// </summary>
    public void SetConfigurationLock(bool locked)
    {
        currentState.isLocked = locked;

        if (uiManager != null)
        {
            uiManager.SetConfigurationLockState(locked);
        }
    }

    /// <summary>
    /// 运行时添加时钟（在实验暂停时调用）
    /// </summary>
    public void ApplyRuntimeAddition(int clockCount, int observerCount)
    {
        Debug.Log($"[ConfigurationManager] 运行时添加 - 时钟: {clockCount}, 观测者: {observerCount}");

        // 创建批次记录
        GenerationBatch batch = new GenerationBatch
        {
            batchId = nextBatchId++,
            timestamp = Time.time,
            clockCount = clockCount,
            observerCount = observerCount,
            startTimeIndex = currentState.nextClockStartTime,
            endTimeIndex = currentState.nextClockStartTime + clockCount - 1
        };

        // 计算时间范围
        batch.timeRange = $"{FormatTime(batch.startTimeIndex)} - {FormatTime(batch.endTimeIndex)}";

        Debug.Log($"[ConfigurationManager] 运行时批次 #{batch.batchId}: 时间范围 {batch.timeRange}");

        // 通知ObjectManager生成时钟
        if (objectManager != null)
        {
            objectManager.GenerateClocksWithTimeOffset(clockCount, currentState.nextClockStartTime);
        }

        // 通知ObserverManager生成观测者
        if (observerManager != null && observerCount > 0)
        {
            observerManager.AddObservers(observerCount);
        }

        // 更新状态
        currentState.totalClocksGenerated += clockCount;
        currentState.nextClockStartTime += clockCount;
        currentState.totalObserversGenerated += observerCount;
        currentState.batches.Add(batch);

        // 更新UI显示
        UpdateUIDisplay();
    }

    /// <summary>
    /// 更新移除后的状态
    /// </summary>
    public void UpdateAfterRemoval(int removedClockCount)
    {
        currentState.totalClocksGenerated -= removedClockCount;
        currentState.nextClockStartTime -= removedClockCount;

        // 更新UI显示
        UpdateUIDisplay();

        Debug.Log($"[ConfigurationManager] 移除后更新 - 当前总数: {currentState.totalClocksGenerated}");
    }

    #endregion

    #region UI更新

    /// <summary>
    /// 更新UI显示
    /// </summary>
    private void UpdateUIDisplay()
    {
        if (uiManager != null)
        {
            uiManager.UpdateConfigurationDisplay(
                currentState.totalClocksGenerated,
                currentState.nextClockStartTime,
                currentState.totalObserversGenerated,
                FormatTime(currentState.nextClockStartTime)
            );
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

    #region 批次管理

    /// <summary>
    /// 获取所有批次信息
    /// </summary>
    public List<GenerationBatch> GetAllBatches()
    {
        return new List<GenerationBatch>(currentState.batches);
    }

    /// <summary>
    /// 获取最后一个批次
    /// </summary>
    public GenerationBatch GetLastBatch()
    {
        if (currentState.batches.Count > 0)
        {
            return currentState.batches[currentState.batches.Count - 1];
        }
        return null;
    }

    /// <summary>
    /// 获取批次摘要信息
    /// </summary>
    public string GetBatchSummary()
    {
        if (currentState.batches.Count == 0) return "尚未生成任何对象";

        string summary = $"共{currentState.batches.Count}个批次:\n";
        foreach (var batch in currentState.batches)
        {
            summary += $"批次{batch.batchId}: {batch.clockCount}个时钟, {batch.timeRange}\n";
        }
        return summary;
    }

    #endregion

    #region 调试

    /// <summary>
    /// 打印配置状态
    /// </summary>
    [ContextMenu("Log Configuration State")]
    public void LogConfigurationState()
    {
        Debug.Log("[ConfigurationManager] 当前配置状态:");
        Debug.Log($"- 已生成时钟: {currentState.totalClocksGenerated}");
        Debug.Log($"- 下一个起始时间: {currentState.nextClockStartTime}秒 ({FormatTime(currentState.nextClockStartTime)})");
        Debug.Log($"- 已生成观测者: {currentState.totalObserversGenerated}");
        Debug.Log($"- 配置锁定: {currentState.isLocked}");
        Debug.Log($"- 批次数量: {currentState.batches.Count}");

        if (currentState.batches.Count > 0)
        {
            Debug.Log("批次详情:");
            foreach (var batch in currentState.batches)
            {
                Debug.Log($"  批次{batch.batchId}: {batch.clockCount}个时钟, {batch.timeRange}");
            }
        }
    }

    #endregion
}