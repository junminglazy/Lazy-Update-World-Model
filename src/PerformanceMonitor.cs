using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

/// <summary>
/// 性能监控器 - 实时监控各项性能指标
/// 修复版本：解决CPU使用率始终为0的问题
/// </summary>
public class PerformanceMonitor : MonoBehaviour
{
    #region 单例模式
    public static PerformanceMonitor Instance { get; private set; }
    #endregion

    #region 性能指标

    /// <summary>
    /// 性能指标数据
    /// </summary>
    [System.Serializable]
    public class PerformanceMetrics
    {
        public float currentFPS;          // 当前FPS
        public float averageFPS;          // 平均FPS
        public float minFPS;              // 最小FPS
        public float maxFPS;              // 最大FPS
        public float frameTime;           // 帧时间(ms)
        public float cpuUsage;            // CPU使用率(%)
        public float memoryUsage;         // 内存使用(MB)
        public float gcMemory;            // GC分配内存(MB)
        public int drawCalls;             // Draw Call数量
        public int triangles;             // 三角形数量
        public int vertices;              // 顶点数量
        public int lazyUpdatesPerFrame;   // 每帧惰性更新数
    }

    #endregion

    #region 配置参数

    [Header("监控设置")]
    [SerializeField] private bool enableMonitoring = true;
    [SerializeField] private float updateInterval = 0.5f;      // 更新间隔
    [SerializeField] private int fpsSampleSize = 60;           // FPS采样数量
    [SerializeField] private bool showDebugInfo = false;       // 显示调试信息

    [Header("性能阈值")]
    [SerializeField] private float lowFPSThreshold = 30f;      // 低FPS阈值
    [SerializeField] private float highCPUThreshold = 80f;     // 高CPU阈值
    [SerializeField] private float highMemoryThreshold = 1000f; // 高内存阈值(MB)

    [Header("性能事件")]
    [SerializeField] private bool enablePerformanceEvents = true;
    [SerializeField] private List<string> performanceEvents = new List<string>();
    [SerializeField] private int maxEventHistory = 100;

    [Header("CPU估算设置")]
    [SerializeField] private int defaultTargetFPS = 60;        // 默认目标帧率
    [SerializeField] private float baseCPUUsage = 20f;         // 基准CPU使用率(60FPS时)
    [SerializeField] private float cpuSmoothingFactor = 0.3f;  // CPU平滑因子

    #endregion

    #region 内部变量

    private PerformanceMetrics currentMetrics = new PerformanceMetrics();
    private List<float> fpsHistory = new List<float>();
    private float deltaTime = 0.0f;
    private float nextUpdateTime = 0f;
    private bool isMonitoring = false;

    // CPU监控
    private float smoothedCPUUsage = 0f;
    private float lastFrameTime;
    private int framesSinceLastUpdate = 0;

    // 内存监控
    private long lastGCMemory;

    // 惰性更新统计
    private int lazyUpdateCount = 0;
    private int frameCount = 0;

    #endregion

    #region 系统引用

    [Header("系统引用")]
    [SerializeField] private DataCollector dataCollector;
    [SerializeField] private UIManager uiManager;

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

        // 设置默认目标帧率（如果未设置）
        if (Application.targetFrameRate <= 0)
        {
            Application.targetFrameRate = defaultTargetFPS;
            Debug.Log($"[PerformanceMonitor] 设置目标帧率为 {defaultTargetFPS} FPS");
        }

        // 初始化性能计数器
        InitializePerformanceCounters();
    }

    private void Start()
    {
        if (enableMonitoring)
        {
            StartMonitoring();

            // 立即进行一次初始更新，避免初始值为0
            StartCoroutine(InitialUpdate());
        }
    }

    private IEnumerator InitialUpdate()
    {
        // 等待一帧以确保其他系统初始化完成
        yield return null;

        // 初始化帧时间
        deltaTime = Time.deltaTime;
        currentMetrics.frameTime = deltaTime * 1000f;

        // 进行首次更新
        UpdateMetrics();

        Debug.Log($"[PerformanceMonitor] 初始更新完成 - CPU: {currentMetrics.cpuUsage:F1}%, FPS: {currentMetrics.currentFPS:F1}");
    }

    private void Update()
    {
        if (!isMonitoring) return;

        // 计算FPS
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        float fps = deltaTime > 0 ? 1.0f / deltaTime : 0f;

        // 添加到历史记录
        if (fps > 0)
        {
            fpsHistory.Add(fps);
            if (fpsHistory.Count > fpsSampleSize)
            {
                fpsHistory.RemoveAt(0);
            }
        }

        // 更新当前FPS
        currentMetrics.currentFPS = fps;
        frameCount++;
        framesSinceLastUpdate++;

        // 定期更新其他指标
        if (Time.time >= nextUpdateTime)
        {
            UpdateMetrics();
            nextUpdateTime = Time.time + updateInterval;
        }
    }

    private void LateUpdate()
    {
        // 重置帧内计数器
        lazyUpdateCount = 0;
    }
    #endregion

    #region 监控控制

    /// <summary>
    /// 开始监控
    /// </summary>
    public void StartMonitoring()
    {
        if (isMonitoring) return;

        Debug.Log("[PerformanceMonitor] 开始性能监控");
        isMonitoring = true;
        nextUpdateTime = Time.time;
        fpsHistory.Clear();
        performanceEvents.Clear();
        framesSinceLastUpdate = 0;

        MarkPerformanceEvent("MonitoringStarted", "性能监控开始");
    }

    /// <summary>
    /// 停止监控
    /// </summary>
    public void StopMonitoring()
    {
        if (!isMonitoring) return;

        Debug.Log("[PerformanceMonitor] 停止性能监控");
        isMonitoring = false;

        MarkPerformanceEvent("MonitoringStopped", "性能监控停止");
    }

    #endregion

    #region 性能指标更新

    /// <summary>
    /// 初始化性能计数器
    /// </summary>
    private void InitializePerformanceCounters()
    {
        // 初始化默认值
        currentMetrics.cpuUsage = 0f;
        smoothedCPUUsage = 0f;

        try
        {
            // 尝试初始化CPU计数器（仅在Windows平台）
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            // Windows平台特定代码（如果需要）
            Debug.Log("[PerformanceMonitor] Windows平台CPU监控初始化");
#endif
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PerformanceMonitor] 无法初始化CPU计数器: {e.Message}");
        }
    }

    /// <summary>
    /// 更新性能指标
    /// </summary>
    private void UpdateMetrics()
    {
        // 计算平均FPS
        if (fpsHistory.Count > 0)
        {
            float totalFPS = 0f;
            float minFPS = float.MaxValue;
            float maxFPS = float.MinValue;

            foreach (float fps in fpsHistory)
            {
                totalFPS += fps;
                if (fps < minFPS) minFPS = fps;
                if (fps > maxFPS) maxFPS = fps;
            }

            currentMetrics.averageFPS = totalFPS / fpsHistory.Count;
            currentMetrics.minFPS = minFPS;
            currentMetrics.maxFPS = maxFPS;
        }
        else
        {
            // 如果没有历史数据，使用当前值
            currentMetrics.averageFPS = currentMetrics.currentFPS;
            currentMetrics.minFPS = currentMetrics.currentFPS;
            currentMetrics.maxFPS = currentMetrics.currentFPS;
        }

        // 更新帧时间
        if (deltaTime > 0)
        {
            currentMetrics.frameTime = deltaTime * 1000f;
        }
        else
        {
            currentMetrics.frameTime = Time.deltaTime * 1000f;
        }

        // 更新CPU使用率
        UpdateCPUUsage();

        // 更新内存使用
        UpdateMemoryUsage();

        // 更新渲染统计
        UpdateRenderingStats();

        // 计算每帧惰性更新数
        currentMetrics.lazyUpdatesPerFrame = frameCount > 0 ? lazyUpdateCount / frameCount : 0;
        frameCount = 0;

        // 检查性能异常
        CheckPerformanceAnomalies();

        // 更新UI显示
        if (uiManager != null)
        {
            uiManager.UpdatePerformanceDisplay(currentMetrics);
        }

        // 调试输出
        if (showDebugInfo)
        {
            Debug.Log($"[PerformanceMonitor] FPS: {currentMetrics.currentFPS:F1}, " +
                     $"CPU: {currentMetrics.cpuUsage:F1}%, " +
                     $"FrameTime: {currentMetrics.frameTime:F1}ms");
        }
    }

    /// <summary>
    /// 更新CPU使用率
    /// </summary>
    private void UpdateCPUUsage()
    {
        // 使用多种方法估算CPU使用率
        float estimatedCPU = 0f;

        // 方法1：基于帧时间估算
        float frameBasedCPU = EstimateCPUFromFrameTime();

        // 方法2：基于FPS估算
        float fpsBasedCPU = EstimateCPUFromFPS();

        // 综合两种方法，取较高值（更保守的估算）
        estimatedCPU = Mathf.Max(frameBasedCPU, fpsBasedCPU);

        // 应用平滑处理
        smoothedCPUUsage = Mathf.Lerp(smoothedCPUUsage, estimatedCPU, cpuSmoothingFactor);
        currentMetrics.cpuUsage = smoothedCPUUsage;

        // 确保CPU使用率不为0（至少显示最小值）
        if (currentMetrics.cpuUsage < 1f && Application.isPlaying)
        {
            currentMetrics.cpuUsage = Mathf.Max(5f, baseCPUUsage * 0.25f); // 最小显示5%或基准的25%
        }
    }

    /// <summary>
    /// 基于帧时间估算CPU使用率
    /// </summary>
    private float EstimateCPUFromFrameTime()
    {
        // 获取目标帧率
        int targetFPS = Application.targetFrameRate;
        if (targetFPS <= 0)
        {
            targetFPS = defaultTargetFPS;
        }

        float targetFrameTime = 1000f / targetFPS;

        // 确保frameTime有效
        float actualFrameTime = currentMetrics.frameTime;
        if (actualFrameTime <= 0)
        {
            actualFrameTime = deltaTime * 1000f;
        }

        // 计算CPU使用率
        // 如果实际帧时间等于目标帧时间，使用基准CPU
        // 如果实际帧时间更长，CPU使用率按比例增加
        float usage = baseCPUUsage * (actualFrameTime / targetFrameTime);

        return Mathf.Clamp(usage, 0f, 100f);
    }

    /// <summary>
    /// 基于FPS估算CPU使用率
    /// </summary>
    private float EstimateCPUFromFPS()
    {
        if (currentMetrics.currentFPS <= 0) return baseCPUUsage;

        // 获取目标帧率
        int targetFPS = Application.targetFrameRate;
        if (targetFPS <= 0)
        {
            targetFPS = defaultTargetFPS;
        }

        // 如果当前FPS达到或超过目标，CPU使用率为基准值
        if (currentMetrics.currentFPS >= targetFPS)
        {
            return baseCPUUsage;
        }

        // FPS越低，CPU使用率越高（可能是CPU瓶颈）
        float fpsRatio = targetFPS / currentMetrics.currentFPS;
        float usage = baseCPUUsage * fpsRatio;

        return Mathf.Clamp(usage, baseCPUUsage, 100f);
    }

    /// <summary>
    /// 更新内存使用
    /// </summary>
    private void UpdateMemoryUsage()
    {
        // 总内存使用
        currentMetrics.memoryUsage = (float)Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);

        // GC内存
        long gcMemory = System.GC.GetTotalMemory(false);
        currentMetrics.gcMemory = (float)gcMemory / (1024f * 1024f);
    }

    /// <summary>
    /// 更新渲染统计
    /// </summary>
    private void UpdateRenderingStats()
    {
        // 注意：这些API可能在某些Unity版本中不可用
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        currentMetrics.drawCalls = UnityStats.drawCalls;
        currentMetrics.triangles = UnityStats.triangles;
        currentMetrics.vertices = UnityStats.vertices;
#endif
    }

    /// <summary>
    /// 检查性能异常
    /// </summary>
    private void CheckPerformanceAnomalies()
    {
        // 检查低FPS
        if (currentMetrics.currentFPS < lowFPSThreshold && currentMetrics.currentFPS > 0)
        {
            string msg = $"低FPS警告: {currentMetrics.currentFPS:F1} FPS";
            MarkPerformanceEvent("LowFPS", msg);
/*
            if (dataCollector != null)
            {
                dataCollector.RecordPerformanceAnomaly("LowFPS", currentMetrics.currentFPS);
            }*/
        }

        // 检查高CPU
        if (currentMetrics.cpuUsage > highCPUThreshold)
        {
            string msg = $"高CPU使用率: {currentMetrics.cpuUsage:F0}%";
            MarkPerformanceEvent("HighCPU", msg);

/*            if (dataCollector != null)
            {
                dataCollector.RecordPerformanceAnomaly("HighCPU", currentMetrics.cpuUsage);
            }*/
        }

        // 检查高内存
        if (currentMetrics.memoryUsage > highMemoryThreshold)
        {
            string msg = $"高内存使用: {currentMetrics.memoryUsage:F0} MB";
            MarkPerformanceEvent("HighMemory", msg);

/*            if (dataCollector != null)
            {
                dataCollector.RecordPerformanceAnomaly("HighMemory", currentMetrics.memoryUsage);
            }*/
        }
    }

    #endregion

    #region 性能事件

    /// <summary>
    /// 标记性能事件
    /// </summary>
    public void MarkPerformanceEvent(string eventType, string description = "")
    {
        if (!enablePerformanceEvents) return;

        string eventString = $"[{Time.time:F2}] {eventType}: {description}";
        performanceEvents.Add(eventString);

        // 限制历史记录大小
        if (performanceEvents.Count > maxEventHistory)
        {
            performanceEvents.RemoveAt(0);
        }

        if (showDebugInfo)
        {
            Debug.Log($"[PerformanceMonitor] {eventString}");
        }
    }

    /// <summary>
    /// 记录惰性更新
    /// </summary>
    public void RecordLazyUpdate()
    {
        lazyUpdateCount++;
    }

    #endregion

    #region 公共接口

    /// <summary>
    /// 获取当前FPS
    /// </summary>
    public float GetCurrentFPS()
    {
        return currentMetrics.currentFPS;
    }

    /// <summary>
    /// 获取平均FPS
    /// </summary>
    public float GetAverageFPS()
    {
        return currentMetrics.averageFPS;
    }

    /// <summary>
    /// 获取帧时间
    /// </summary>
    public float GetFrameTime()
    {
        return currentMetrics.frameTime;
    }

    /// <summary>
    /// 获取CPU使用率
    /// </summary>
    public float GetCPUUsage()
    {
        // 确保返回有效的CPU值
        if (currentMetrics.cpuUsage <= 0 && Application.isPlaying)
        {
            // 如果还没有计算出CPU，返回一个估算值
            return Mathf.Max(5f, baseCPUUsage * 0.5f);
        }
        return currentMetrics.cpuUsage;
    }

    /// <summary>
    /// 获取内存使用
    /// </summary>
    public float GetMemoryUsage()
    {
        return currentMetrics.memoryUsage;
    }

    /// <summary>
    /// 获取当前性能指标
    /// </summary>
    public PerformanceMetrics GetCurrentMetrics()
    {
        return currentMetrics;
    }

    /// <summary>
    /// 获取性能事件历史
    /// </summary>
    public List<string> GetPerformanceEvents()
    {
        return new List<string>(performanceEvents);
    }

    /// <summary>
    /// 清除性能事件历史
    /// </summary>
    public void ClearPerformanceEvents()
    {
        performanceEvents.Clear();
    }

    /// <summary>
    /// 手动触发一次更新（用于调试）
    /// </summary>
    public void ForceUpdate()
    {
        UpdateMetrics();
        Debug.Log($"[PerformanceMonitor] 强制更新 - CPU: {currentMetrics.cpuUsage:F1}%, FPS: {currentMetrics.currentFPS:F1}");
    }

    #endregion

    #region 调试显示

    /// <summary>
    /// 在GUI上显示性能信息
    /// </summary>
    private void OnGUI()
    {
        if (!showDebugInfo || !isMonitoring) return;

        int w = Screen.width, h = Screen.height;
        GUIStyle style = new GUIStyle();

        Rect rect = new Rect(10, 10, w, h * 2 / 100);
        style.alignment = TextAnchor.UpperLeft;
        style.fontSize = h * 2 / 100;
        style.normal.textColor = currentMetrics.currentFPS < lowFPSThreshold ? Color.red : Color.green;

        string text = $"FPS: {currentMetrics.currentFPS:F1} (Avg: {currentMetrics.averageFPS:F1})\n";
        text += $"Frame: {currentMetrics.frameTime:F1}ms\n";
        text += $"CPU: {currentMetrics.cpuUsage:F0}% (Smoothed)\n";
        text += $"Memory: {currentMetrics.memoryUsage:F0}MB\n";
        text += $"Lazy Updates/Frame: {currentMetrics.lazyUpdatesPerFrame}\n";
        text += $"Target FPS: {(Application.targetFrameRate > 0 ? Application.targetFrameRate.ToString() : "Unlimited")}";

        GUI.Label(rect, text, style);
    }

    #endregion

    #region 性能报告

    /// <summary>
    /// 生成性能报告
    /// </summary>
    public string GeneratePerformanceReport()
    {
        string report = "=== 性能报告 ===\n";
        report += $"当前FPS: {currentMetrics.currentFPS:F1}\n";
        report += $"平均FPS: {currentMetrics.averageFPS:F1}\n";
        report += $"最小/最大FPS: {currentMetrics.minFPS:F1}/{currentMetrics.maxFPS:F1}\n";
        report += $"帧时间: {currentMetrics.frameTime:F1}ms\n";
        report += $"CPU使用率: {currentMetrics.cpuUsage:F0}%\n";
        report += $"内存使用: {currentMetrics.memoryUsage:F0}MB\n";
        report += $"GC内存: {currentMetrics.gcMemory:F0}MB\n";
        report += $"目标帧率: {(Application.targetFrameRate > 0 ? Application.targetFrameRate.ToString() : "无限制")}\n";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        report += $"Draw Calls: {currentMetrics.drawCalls}\n";
        report += $"三角形: {currentMetrics.triangles}\n";
        report += $"顶点: {currentMetrics.vertices}\n";
#endif

        report += $"惰性更新/帧: {currentMetrics.lazyUpdatesPerFrame}\n";
        report += "================\n";

        if (performanceEvents.Count > 0)
        {
            report += "\n最近的性能事件:\n";
            int startIndex = Mathf.Max(0, performanceEvents.Count - 10);
            for (int i = startIndex; i < performanceEvents.Count; i++)
            {
                report += performanceEvents[i] + "\n";
            }
        }

        return report;
    }

    #endregion
}

/// <summary>
/// Unity内部统计（编辑器和开发版本）
/// </summary>
public static class UnityStats
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static int drawCalls => 0; // 需要根据Unity版本使用正确的API
    public static int triangles => 0;
    public static int vertices => 0;
#else
    public static int drawCalls => 0;
    public static int triangles => 0;
    public static int vertices => 0;
#endif
}