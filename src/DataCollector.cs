using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// 增强版数据收集器 - 单模式采集，双模式对比
/// 由ExperimentController调用，不处理热键
/// </summary>
public class DataCollector : MonoBehaviour
{
    #region 单例模式
    public static DataCollector Instance { get; private set; }
    #endregion

    #region 数据结构定义

    /// <summary>
    /// 时钟配置数据集 - 存储特定时钟数量下的双模式数据
    /// </summary>
    [System.Serializable]
    public class ClockConfigData
    {
        public int clockCount;                              // 时钟数量
        public ModeData traditionalData;                    // 传统模式数据
        public ModeData lazyData;                          // 惰性模式数据
        public bool hasTraditionalData;                    // 是否有传统模式数据
        public bool hasLazyData;                          // 是否有惰性模式数据
        public PerformanceComparison comparison;           // 性能对比

        public ClockConfigData(int count)
        {
            clockCount = count;
            traditionalData = new ModeData(ExperimentController.ExperimentMode.Traditional);
            lazyData = new ModeData(ExperimentController.ExperimentMode.LazyUpdate);
            hasTraditionalData = false;
            hasLazyData = false;
        }

        public bool HasBothModesData => hasTraditionalData && hasLazyData;
    }

    /// <summary>
    /// 单个模式的数据
    /// </summary>
    [System.Serializable]
    public class ModeData
    {
        public ExperimentController.ExperimentMode mode;
        public List<PerformanceSnapshot> snapshots;
        public DateTime recordTime;
        public float recordingDuration;
        public ModeStatistics statistics;

        public ModeData(ExperimentController.ExperimentMode m)
        {
            mode = m;
            snapshots = new List<PerformanceSnapshot>();
            statistics = new ModeStatistics();
        }
    }

    /// <summary>
    /// 性能快照
    /// </summary>
    [System.Serializable]
    public class PerformanceSnapshot
    {
        public float timestamp;
        public float fps;
        public float frameTime;
        public float cpuUsage;
        public float memoryUsage;
        public int activeClocks;
        public float activeRatio;
    }

    /// <summary>
    /// 模式统计
    /// </summary>
    [System.Serializable]
    public class ModeStatistics
    {
        public float averageFPS;
        public float minFPS;
        public float maxFPS;
        public float stdDevFPS;
        public float averageCPU;
        public float averageActiveRatio;
    }

    /// <summary>
    /// 性能对比
    /// </summary>
    [System.Serializable]
    public class PerformanceComparison
    {
        public float fpsGain;           // FPS提升百分比
        public float cpuReduction;      // CPU降低百分比
        public float efficiencyGain;    // 效率提升百分比
        public DateTime compareTime;
    }

    #endregion

    #region 配置参数

    [Header("=== 数据收集配置 ===")]
    [SerializeField] private float recordingDuration = 10f;    // 采集时长（秒）
    [SerializeField] private float snapshotInterval = 0.1f;    // 快照间隔

    [Header("=== 当前状态 ===")]
    [SerializeField] private bool isRecording = false;
    [SerializeField] private float recordingProgress = 0f;
    [SerializeField] private int currentClockCount = 0;
    [SerializeField] private ExperimentController.ExperimentMode currentRecordingMode;

    [Header("=== 数据存储 ===")]
    [SerializeField] private Dictionary<int, ClockConfigData> allConfigData = new Dictionary<int, ClockConfigData>();
    private ClockConfigData currentConfigData;
    private Coroutine recordingCoroutine;

    #endregion

    #region 系统引用

    [Header("=== 系统引用 ===")]
    [SerializeField] private ExperimentController experimentController;
    [SerializeField] private PerformanceMonitor performanceMonitor;
    [SerializeField] private ObjectManager objectManager;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private TimeManager timeManager;

    #endregion

    #region Unity生命周期

    private void Awake()
    {
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
        Debug.Log("[DataCollector] 数据收集器已初始化");
    }

    #endregion

    #region 公共接口 - 由ExperimentController调用

    /// <summary>
    /// 开始当前模式的数据记录
    /// </summary>
    public void StartCurrentModeRecording()
    {
        if (isRecording)
        {
            Debug.LogWarning("[DataCollector] 正在记录中，请等待完成");
            return;
        }

        if (experimentController == null)
        {
            Debug.LogError("[DataCollector] ExperimentController未设置！");
            return;
        }

        if (!experimentController.IsRunning)
        {
            Debug.LogWarning("[DataCollector] 请先启动实验（按X开始时间流动）");
            return;
        }

        // 获取当前时钟数量
        var stats = objectManager.GetStats();
        currentClockCount = stats.totalClocks;

        if (currentClockCount == 0)
        {
            Debug.LogWarning("[DataCollector] 没有时钟！请先按Z生成时钟");
            return;
        }

        // 获取或创建配置数据
        if (!allConfigData.ContainsKey(currentClockCount))
        {
            allConfigData[currentClockCount] = new ClockConfigData(currentClockCount);
        }
        currentConfigData = allConfigData[currentClockCount];

        // 获取当前模式
        currentRecordingMode = experimentController.CurrentMode;

        // 检查是否已经采集过当前模式
        if (currentRecordingMode == ExperimentController.ExperimentMode.Traditional && currentConfigData.hasTraditionalData)
        {
            Debug.LogWarning($"[DataCollector] {currentClockCount}个时钟的传统模式数据已存在，将覆盖旧数据");
        }
        else if (currentRecordingMode == ExperimentController.ExperimentMode.LazyUpdate && currentConfigData.hasLazyData)
        {
            Debug.LogWarning($"[DataCollector] {currentClockCount}个时钟的惰性模式数据已存在，将覆盖旧数据");
        }

        Debug.Log($"[DataCollector] 开始记录：{currentClockCount}个时钟，{currentRecordingMode}模式");
        recordingCoroutine = StartCoroutine(RecordModeData());
    }

    /// <summary>
    /// 检查当前时钟数量是否已有完整数据
    /// </summary>
    public bool HasCompleteDataForCurrentClockCount()
    {
        if (objectManager == null) return false;

        var stats = objectManager.GetStats();
        int currentCount = stats.totalClocks;

        if (!allConfigData.ContainsKey(currentCount))
        {
            return false;
        }

        return allConfigData[currentCount].HasBothModesData;
    }

    /// <summary>
    /// 获取当前时钟数量的数据状态
    /// </summary>
    public (bool hasTraditional, bool hasLazy) GetCurrentDataStatus()
    {
        if (objectManager == null) return (false, false);

        var stats = objectManager.GetStats();
        int currentCount = stats.totalClocks;

        if (!allConfigData.ContainsKey(currentCount))
        {
            return (false, false);
        }

        var data = allConfigData[currentCount];
        return (data.hasTraditionalData, data.hasLazyData);
    }

    /// <summary>
    /// 生成对比报告
    /// </summary>
    public void GenerateComparisonReport()
    {
        if (allConfigData.Count == 0)
        {
            Debug.LogWarning("[DataCollector] 没有任何数据");
            return;
        }

        Debug.Log("\n============ 性能对比报告 ============");
        Debug.Log($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Debug.Log($"数据组数: {allConfigData.Count}");
        Debug.Log("");

        // 按时钟数量排序
        var sortedConfigs = allConfigData.OrderBy(kvp => kvp.Key).ToList();

        Debug.Log("时钟数\t状态\t\t传统FPS\t惰性FPS\tFPS提升\tCPU节省");
        Debug.Log("------\t----\t\t-------\t-------\t-------\t-------");

        foreach (var kvp in sortedConfigs)
        {
            int clockCount = kvp.Key;
            var configData = kvp.Value;

            string status = configData.HasBothModesData ? "完整" : "不完整";

            if (configData.HasBothModesData)
            {
                Debug.Log($"{clockCount}\t{status}\t\t" +
                    $"{configData.traditionalData.statistics.averageFPS:F1}\t" +
                    $"{configData.lazyData.statistics.averageFPS:F1}\t" +
                    $"{configData.comparison.fpsGain:F1}%\t" +
                    $"{configData.comparison.cpuReduction:F1}%");
            }
            else
            {
                string tradFPS = configData.hasTraditionalData ?
                    configData.traditionalData.statistics.averageFPS.ToString("F1") : "--";
                string lazyFPS = configData.hasLazyData ?
                    configData.lazyData.statistics.averageFPS.ToString("F1") : "--";

                Debug.Log($"{clockCount}\t{status}\t\t{tradFPS}\t{lazyFPS}\t--\t--");
            }
        }

        // 生成详细报告
        GenerateDetailedReport(sortedConfigs);
    }

    /// <summary>
    /// 显示数据状态
    /// </summary>
    public void ShowDataStatus()
    {
        Debug.Log("\n=== 数据收集状态 ===");
        Debug.Log($"数据组数: {allConfigData.Count}");

        if (allConfigData.Count > 0)
        {
            foreach (var kvp in allConfigData.OrderBy(k => k.Key))
            {
                var config = kvp.Value;
                string status = "";
                if (config.hasTraditionalData) status += "传统✓ ";
                else status += "传统✗ ";
                if (config.hasLazyData) status += "惰性✓";
                else status += "惰性✗";

                Debug.Log($"{kvp.Key}个时钟: {status}");
            }
        }

        if (isRecording)
        {
            Debug.Log($"\n正在记录: {currentRecordingMode}模式");
            Debug.Log($"进度: {recordingProgress:P0}");
        }

        Debug.Log("==================");
    }

    /// <summary>
    /// 清除所有数据
    /// </summary>
    public void ClearAllData()
    {
        allConfigData.Clear();
        currentConfigData = null;
        isRecording = false;

        if (recordingCoroutine != null)
        {
            StopCoroutine(recordingCoroutine);
            recordingCoroutine = null;
        }

        Debug.Log("[DataCollector] 所有数据已清除");
    }

    /// <summary>
    /// 检查是否正在记录
    /// </summary>
    public bool IsRecording => isRecording;

    #endregion

    #region 内部方法

    /// <summary>
    /// 记录模式数据协程
    /// </summary>
    private IEnumerator RecordModeData()
    {
        isRecording = true;
        recordingProgress = 0f;

        // 获取对应模式的数据对象
        ModeData modeData = currentRecordingMode == ExperimentController.ExperimentMode.Traditional ?
            currentConfigData.traditionalData : currentConfigData.lazyData;

        // 清空旧数据
        modeData.snapshots.Clear();
        modeData.recordTime = DateTime.Now;
        modeData.recordingDuration = recordingDuration;

        float startTime = Time.time;
        float elapsedTime = 0f;


        // 采集数据
        while (elapsedTime < recordingDuration)
        {
            // 收集快照
            var snapshot = CollectSnapshot();
            modeData.snapshots.Add(snapshot);

            // 更新进度
            elapsedTime = Time.time - startTime;
            recordingProgress = elapsedTime / recordingDuration;


            yield return new WaitForSeconds(snapshotInterval);
        }

        // 计算统计数据
        CalculateStatistics(modeData);

        // 标记数据已采集
        if (currentRecordingMode == ExperimentController.ExperimentMode.Traditional)
        {
            currentConfigData.hasTraditionalData = true;
        }
        else
        {
            currentConfigData.hasLazyData = true;
        }

        // 如果两种模式都有数据，计算对比
        if (currentConfigData.HasBothModesData)
        {
            CalculateComparison(currentConfigData);
        }

        isRecording = false;
        recordingProgress = 0f;

        // 显示完成信息
        ShowRecordingComplete(modeData);
        CheckDataCompleteness();
    }

    /// <summary>
    /// 收集性能快照
    /// </summary>
    private PerformanceSnapshot CollectSnapshot()
    {
        var snapshot = new PerformanceSnapshot
        {
            timestamp = Time.time
        };

        if (performanceMonitor != null)
        {
            var metrics = performanceMonitor.GetCurrentMetrics();
            snapshot.fps = metrics.currentFPS;
            snapshot.frameTime = metrics.frameTime;
            snapshot.cpuUsage = metrics.cpuUsage;
            snapshot.memoryUsage = metrics.memoryUsage;
        }

        if (objectManager != null)
        {
            var stats = objectManager.GetStats();
            snapshot.activeClocks = stats.activeClocks;
            snapshot.activeRatio = stats.activeRatio;
        }

        return snapshot;
    }

    /// <summary>
    /// 计算统计数据
    /// </summary>
    private void CalculateStatistics(ModeData modeData)
    {
        if (modeData.snapshots.Count == 0) return;

        var stats = modeData.statistics;

        // 计算平均值
        float totalFPS = 0f;
        float totalCPU = 0f;
        float totalActiveRatio = 0f;
        stats.minFPS = float.MaxValue;
        stats.maxFPS = float.MinValue;

        foreach (var snapshot in modeData.snapshots)
        {
            totalFPS += snapshot.fps;
            totalCPU += snapshot.cpuUsage;
            totalActiveRatio += snapshot.activeRatio;

            if (snapshot.fps < stats.minFPS) stats.minFPS = snapshot.fps;
            if (snapshot.fps > stats.maxFPS) stats.maxFPS = snapshot.fps;
        }

        int count = modeData.snapshots.Count;
        stats.averageFPS = totalFPS / count;
        stats.averageCPU = totalCPU / count;
        stats.averageActiveRatio = totalActiveRatio / count;

        // 计算标准差
        float sumSquaredDiff = 0f;
        foreach (var snapshot in modeData.snapshots)
        {
            float diff = snapshot.fps - stats.averageFPS;
            sumSquaredDiff += diff * diff;
        }
        stats.stdDevFPS = Mathf.Sqrt(sumSquaredDiff / count);
    }

    /// <summary>
    /// 计算性能对比
    /// </summary>
    private void CalculateComparison(ClockConfigData configData)
    {
        var comparison = new PerformanceComparison
        {
            compareTime = DateTime.Now
        };

        var trad = configData.traditionalData.statistics;
        var lazy = configData.lazyData.statistics;

        // 计算提升百分比
        if (trad.averageFPS > 0)
        {
            comparison.fpsGain = ((lazy.averageFPS - trad.averageFPS) / trad.averageFPS) * 100f;
        }

        if (trad.averageCPU > 0)
        {
            comparison.cpuReduction = ((trad.averageCPU - lazy.averageCPU) / trad.averageCPU) * 100f;
        }

        comparison.efficiencyGain = (1f - lazy.averageActiveRatio) * 100f;

        configData.comparison = comparison;
    }

    /// <summary>
    /// 显示记录完成信息
    /// </summary>
    private void ShowRecordingComplete(ModeData modeData)
    {
        var stats = modeData.statistics;

        Debug.Log($"[DataCollector] {currentRecordingMode}模式记录完成");
        Debug.Log($"- 时钟数: {currentClockCount}");
        Debug.Log($"- 平均FPS: {stats.averageFPS:F1} (±{stats.stdDevFPS:F1})");
        Debug.Log($"- FPS范围: {stats.minFPS:F1} - {stats.maxFPS:F1}");
        Debug.Log($"- 平均CPU: {stats.averageCPU:F1}%");

        if (currentRecordingMode == ExperimentController.ExperimentMode.LazyUpdate)
        {
            Debug.Log($"- 活跃率: {stats.averageActiveRatio:P1}");
        }
    }

    /// <summary>
    /// 检查数据完整性
    /// </summary>
    private void CheckDataCompleteness()
    {
        if (currentConfigData.HasBothModesData)
        {
            Debug.Log($"[DataCollector] ✓ {currentClockCount}个时钟的两种模式数据已齐全！");
            Debug.Log($"- FPS提升: {currentConfigData.comparison.fpsGain:F1}%");
            Debug.Log($"- CPU节省: {currentConfigData.comparison.cpuReduction:F1}%");
            Debug.Log($"- 效率提升: {currentConfigData.comparison.efficiencyGain:F1}%");
            Debug.Log("现在可以按C追加时钟了！");

        }
        else
        {
            string missingMode = currentConfigData.hasTraditionalData ? "惰性" : "传统";
            Debug.Log($"[DataCollector] 还需要采集{missingMode}模式的数据");
            Debug.Log("请按B切换模式，然后按G采集");
        }
    }

    /// <summary>
    /// 生成详细报告
    /// </summary>
    private void GenerateDetailedReport(List<KeyValuePair<int, ClockConfigData>> sortedConfigs)
    {
        Debug.Log("\n======== 详细数据 ========");

        foreach (var kvp in sortedConfigs)
        {
            int clockCount = kvp.Key;
            var configData = kvp.Value;

            Debug.Log($"\n【{clockCount}个时钟】");

            if (configData.hasTraditionalData)
            {
                var trad = configData.traditionalData;
                Debug.Log($"传统模式 (记录时间: {trad.recordTime:HH:mm:ss}):");
                Debug.Log($"  FPS: {trad.statistics.averageFPS:F2} ± {trad.statistics.stdDevFPS:F2}");
                Debug.Log($"  范围: {trad.statistics.minFPS:F1} - {trad.statistics.maxFPS:F1}");
                Debug.Log($"  CPU: {trad.statistics.averageCPU:F1}%");
            }

            if (configData.hasLazyData)
            {
                var lazy = configData.lazyData;
                Debug.Log($"惰性模式 (记录时间: {lazy.recordTime:HH:mm:ss}):");
                Debug.Log($"  FPS: {lazy.statistics.averageFPS:F2} ± {lazy.statistics.stdDevFPS:F2}");
                Debug.Log($"  范围: {lazy.statistics.minFPS:F1} - {lazy.statistics.maxFPS:F1}");
                Debug.Log($"  CPU: {lazy.statistics.averageCPU:F1}%");
                Debug.Log($"  活跃率: {lazy.statistics.averageActiveRatio:P2}");
            }

            if (configData.HasBothModesData)
            {
                Debug.Log($"性能对比:");
                Debug.Log($"  FPS提升: {configData.comparison.fpsGain:F1}%");
                Debug.Log($"  CPU节省: {configData.comparison.cpuReduction:F1}%");
                Debug.Log($"  效率提升: {configData.comparison.efficiencyGain:F1}%");
            }
        }

        Debug.Log("\n=====================================");
    }

    #endregion
}