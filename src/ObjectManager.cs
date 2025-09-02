using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 对象管理器 - 负责所有时钟对象的生成、管理和查询
/// 支持批次生成和时间连续性
/// </summary>
public class ObjectManager : MonoBehaviour
{
    #region 单例模式
    public static ObjectManager Instance { get; private set; }
    #endregion

    #region 配置与引用

    [Header("=== 基础配置 ===")]
    [SerializeField] private GameObject clockPrefab;
    [SerializeField] private Transform clockContainer;
    [SerializeField] private ClockNumberDatabase numberDatabase;

    [Header("生成配置")]
    [Tooltip("时钟之间的间距")]
    [SerializeField] private float spacing = 4f;

    [Header("=== 统计信息（只读）===")]
    [SerializeField] private int totalClockCount = 0;
    [SerializeField] private int activeClockCount = 0;
    [SerializeField] private int nextClockStartTime = 0;  // 下一个时钟的起始时间（秒）

    [Header("=== 系统引用（可选）===")]
    [Tooltip("可选：不设置则使用调试模式")]
    [SerializeField] private TimeManager timeManager;
    [SerializeField] private ConfigurationManager configurationManager;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private ObserverManager observerManager;
    [SerializeField] private ViewFieldDetector viewFieldDetector;

    #endregion

    #region 内部数据

    // 所有时钟的列表
    // 时钟GameObject到其ObservableRecordState的映射 - 【核心数据结构】
    private List<Clock> allClocks = new List<Clock>();
    private List<ObservableRecordState> observableMap = new List<ObservableRecordState>();


    // 当前的实验模式
    private ExperimentController.ExperimentMode currentMode = ExperimentController.ExperimentMode.Traditional;

    // 是否已初始化
    private bool isInitialized = false;

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
        // 验证预制体
        if (clockPrefab == null)
        {
            Debug.LogError("[ObjectManager] 时钟预制体未设置！");
            return;
        }

        // 创建容器
        if (clockContainer == null)
        {
            GameObject container = new GameObject("ClockContainer");
            container.transform.SetParent(transform);
            clockContainer = container.transform;
        }

        // 检查数字数据库
        if (numberDatabase == null)
        {
            Debug.LogWarning("[ObjectManager] 数字数据库未设置，时钟可能无法正确显示数字！");
        }

        isInitialized = true;
    }

    #endregion

    #region 核心功能：时钟生成

    /// <summary>
    /// 生成指定数量的时钟，从指定的起始时间开始
    /// 这是核心方法，被ConfigurationManager和调试模式调用
    /// </summary>
    public void GenerateClocksWithTimeOffset(int count, int startTimeInSeconds)
    {
        if (!isInitialized)
        {
            Debug.LogError("[ObjectManager] 尚未初始化！");
            return;
        }

        // 计算网格尺寸（正方形网格）
        int totalCount = totalClockCount + count;
        int dimension = Mathf.CeilToInt(Mathf.Sqrt(totalCount));

        // 批次信息
        Debug.Log($"[ObjectManager] ========== 开始生成时钟批次 ==========");
        Debug.Log($"[ObjectManager] 批次信息:");
        Debug.Log($"  - 本批数量: {count}个");
        Debug.Log($"  - 时钟ID范围: #{totalClockCount} - #{totalClockCount + count - 1}");
        Debug.Log($"  - 起始时间: {FormatTime(startTimeInSeconds)} (第{startTimeInSeconds}秒)");
        Debug.Log($"  - 结束时间: {FormatTime(startTimeInSeconds + count - 1)} (第{startTimeInSeconds + count - 1}秒)");
        Debug.Log($"  - 网格尺寸: {dimension}x{dimension}");
        Debug.Log($"[ObjectManager] 生成说明:");
        Debug.Log($"  - 两种模式的数据集都会初始化");
        Debug.Log($"  - 默认进入传统模式");
        Debug.Log($"  - ObservableRecordState会创建并存储（供惰性模式使用）");

        // 开始生成
        int startIndex = totalClockCount;
        int generatedInBatch = 0;

        // 从当前总数开始，继续生成
        for (int index = startIndex; index < startIndex + count; index++)
        {
            int initialTime = startTimeInSeconds + generatedInBatch;

            // 计算位置（从左上角开始，向右下扩展）
            Vector3 position = CalculateClockPosition(index, dimension, spacing);

            // 创建时钟
            CreateSingleClock(index, position, initialTime);

            generatedInBatch++;

            // 进度报告（每100个或最后一个）
            if (generatedInBatch % 100 == 0 || generatedInBatch == count)
            {
                Debug.Log($"[ObjectManager] 生成进度: {generatedInBatch}/{count}");
            }
        }

        // 更新下一个时钟的起始时间
        nextClockStartTime = startTimeInSeconds + count;

        // 验证ObservableState映射表
        Debug.Log($"[ObjectManager] ========== 批次生成完成 ==========");
        Debug.Log($"[ObjectManager] 统计信息:");
        Debug.Log($"  - 时钟总数: {totalClockCount}");
        Debug.Log($"  - 下一批起始时间: {FormatTime(nextClockStartTime)}");
        Debug.Log($"  - 当前模式: {currentMode}");

        // 更新UI（如果有）
        UpdateUIDisplay();
    }

    /// <summary>
    /// 计算时钟位置（从左上角开始排列）
    /// </summary>
    private Vector3 CalculateClockPosition(int index, int dimension, float spacing)
    {
        // 计算行列位置
        int row = index / dimension;    // y方向（向下）
        int col = index % dimension;    // x方向（向右）

        // 从(0,0,0)开始，向右下方扩展
        // x向右为负，y向下为负
        float x = -col * spacing;
        float y = -row * spacing;

        return new Vector3(x, y, 0);
    }

    /// <summary>
    /// 创建单个时钟
    /// 生成时初始化两种模式的数据，但不偏向任何模式
    /// </summary>
    private void CreateSingleClock(int clockId, Vector3 position, int initialTimeInSeconds)
    {
        // 生成GameObject
        GameObject clockObj = Instantiate(clockPrefab, position, Quaternion.identity, clockContainer);
        clockObj.name = $"Clock_{clockId:D4}";

        // 获取Clock组件（可能在子对象中）
        Clock clock = clockObj.GetComponentInChildren<Clock>();
        if (clock == null)
        {
            Debug.LogError($"[ObjectManager] 时钟预制体缺少Clock组件！");
            Destroy(clockObj);
            return;
        }

        // 获取时间管理器（调试模式下可能为null）
        TimeManager tm = timeManager;
        if (tm == null && TimeManager.Instance != null)
        {
            tm = TimeManager.Instance;
        }

        float initialTime = Mathf.Floor(initialTimeInSeconds);
        // 【核心】初始化时钟
        // 生成时初始化两种模式的数据集，不偏向任何模式
        // 默认会进入传统模式，但惰性模式的数据也准备好了
        clock.Initialize(
            clockId,                    // 时钟ID
            initialTime,       // 初始时间（秒）
            tm,                        // 时间管理器
            numberDatabase             // 数字数据库
        );

        // 【重要】获取ObservableRecordState并存储
        // 即使当前是传统模式，也要存储它，为将来可能的模式切换做准备
        ObservableRecordState observableState = clock.GetObservableState();

        if (observableState != null)
        {
            // 将GameObject和其ObservableRecordState存储到映射表
            // 注意：这里使用时钟所在的根GameObject作为key
            allClocks.Add(clock);
            observableMap.Add(observableState);
        }
        else
        {
            Debug.LogError($"[ObjectManager] Clock_{clockId} 的ObservableState为空！");
        }

        // 添加到列表
        totalClockCount++;

        // 不在这里设置activeClockCount，由Clock自己管理
    }

    #endregion

    #region 模式切换

    /// <summary>
    /// 切换所有时钟的更新模式
    /// </summary>
    public void SwitchMode(ExperimentController.ExperimentMode mode)
    {
        currentMode = mode;

        Clock.UpdateMode clockMode = (mode == ExperimentController.ExperimentMode.Traditional)
            ? Clock.UpdateMode.Traditional
            : Clock.UpdateMode.Lazy;

        Debug.Log($"[ObjectManager] ========== 模式切换 ==========");
        Debug.Log($"[ObjectManager] 目标模式: {mode}");

        if (mode == ExperimentController.ExperimentMode.Traditional)
        {
            Debug.Log($"[ObjectManager] 传统模式：");
            Debug.Log($"  - 时钟自主更新");
            Debug.Log($"  - 不使用ObservableRecordState");
        }
        else
        {
            Debug.Log($"[ObjectManager] 惰性模式：");
            Debug.Log($"  - 时钟被观测时更新");
            Debug.Log($"  - 使用ObservableRecordState");
            Debug.Log($"  - ObservableManager将直接从ObjectManager获取状态");
        }

        // 切换所有时钟的模式
        int switchedCount = 0;
        foreach (Clock clock in allClocks)
        {
            if (clock != null)
            {
                clock.SetUpdateMode(clockMode);
                switchedCount++;
            }
        }

        // 更新活跃计数
        activeClockCount = (mode == ExperimentController.ExperimentMode.Traditional) ? totalClockCount : 0;

        // 更新UI
        UpdateUIDisplay();

        Debug.Log($"[ObjectManager] 切换完成:");
        Debug.Log($"  - 切换时钟数: {switchedCount}/{totalClockCount}");
        Debug.Log($"  - 活跃时钟: {activeClockCount}");
        Debug.Log($"[ObjectManager] =============================");
    }


    #endregion

    #region 清理功能

    /// <summary>
    /// 销毁所有时钟对象
    /// </summary>
    public void DestroyAllObjects()
    {
        foreach (Clock clock in allClocks)
        {
            if (clock != null && clock.gameObject != null)
            {
                // 获取时钟的根GameObject
                GameObject rootObj = clock.transform.root.gameObject;
                Destroy(rootObj);
            }
        }

        allClocks.Clear();
        observableMap.Clear();  // 清空映射表
        totalClockCount = 0;
        activeClockCount = 0;
        nextClockStartTime = 0;

        UpdateUIDisplay();

        Debug.Log("[ObjectManager] 所有对象已清除");
    }

    /// <summary>
    /// 移除最后N个时钟
    /// </summary>
    public void RemoveLastClocks(int count)
    {
        if (count <= 0 || totalClockCount == 0) return;

        count = Mathf.Min(count, totalClockCount);

        Debug.Log($"[ObjectManager] 移除最后{count}个时钟");

        for (int i = 0; i < count; i++)
        {
            if (allClocks.Count > 0)
            {
                int lastIndex = allClocks.Count;
                observableMap.RemoveAt(lastIndex);
                allClocks.RemoveAt(lastIndex);
                totalClockCount--;
            }
        }

        // 更新下一个起始时间
        nextClockStartTime = Mathf.Max(0, nextClockStartTime - count);

        // 重新计算活跃数
        if (currentMode == ExperimentController.ExperimentMode.Traditional)
        {
            activeClockCount = totalClockCount;
        }

        UpdateUIDisplay();
    }

    #endregion

    #region 查询接口
    public int GetObserveClockID(GameObject gameObject)
    {
        Clock target = gameObject.GetComponentInChildren<Clock>();
        return target.ClockId;
    }
    public ObservableRecordState GetObservableRecordState(int searchIndex)
    {
        ObservableRecordState target = observableMap[searchIndex];
        return target;
    }
    /// <summary>
    /// 获取所有时钟组件
    /// </summary>
    public List<Clock> GetAllClockComponents()
    {
        return new List<Clock>(allClocks);
    }

    /// <summary>
    /// 获取所有时钟GameObject（返回根GameObject）
    /// </summary>
    public List<GameObject> GetAllClocks()
    {
        List<GameObject> clockObjects = new List<GameObject>();
        foreach (Clock clock in allClocks)
        {
            if (clock != null && clock.gameObject != null)
            {
                // 返回时钟的根GameObject
                clockObjects.Add(clock.transform.root.gameObject);
            }
        }
        return clockObjects;
    }

    /// <summary>
    /// 获取指定范围内的时钟
    /// </summary>
    public List<GameObject> GetClocksInBounds(Bounds bounds)
    {
        List<GameObject> clocksInBounds = new List<GameObject>();

        foreach (Clock clock in allClocks)
        {
            if (clock != null && bounds.Contains(clock.transform.position))
            {
                clocksInBounds.Add(clock.transform.root.gameObject);
            }
        }

        return clocksInBounds;
    }

    /// <summary>
    /// 通过GameObject获取Clock组件
    /// </summary>
    public Clock GetClockComponent(GameObject clockObj)
    {
        return clockObj?.GetComponentInChildren<Clock>();
    }

    #endregion

    #region 统计与监控

    /// <summary>
    /// 更新活跃时钟计数
    /// </summary>
    public void UpdateActiveCount(int delta)
    {
        activeClockCount = Mathf.Clamp(activeClockCount + delta, 0, totalClockCount);
        UpdateUIDisplay();
    }

    /// <summary>
    /// 获取统计信息
    /// </summary>
    public ObjectManagerStats GetStats()
    {
        activeClockCount = 0;
        List<Clock> clockObseList = new List<Clock>();
        clockObseList = observerManager.GetObserverOfHitClocks();
        List<Clock> clockOCamList = new List<Clock>();
        clockOCamList = viewFieldDetector.GetHitClocks();
        foreach (Clock clock in clockObseList)
        {
            activeClockCount++;
        }
        foreach (Clock clock in clockOCamList)
        {
           bool hasfoundList = false;
            foreach (Clock target in clockObseList)
            {
                if(target == clock)
                {
                    hasfoundList = true;
                    break;
                }
            }
            if(!hasfoundList)
            {
                activeClockCount++;
            }
        }
        return new ObjectManagerStats
        {
            totalClocks = totalClockCount,
            activeClocks = activeClockCount,
            activeRatio = totalClockCount > 0 ? (float)activeClockCount / totalClockCount : 0f,
            currentMode = currentMode,
            nextStartTime = nextClockStartTime,
        };
    }

    #endregion

    #region UI更新

    /// <summary>
    /// 更新UI显示（如果有UIManager）
    /// </summary>
    private void UpdateUIDisplay()
    {
        // 尝试获取UIManager
        if (uiManager == null && UIManager.Instance != null)
        {
            uiManager = UIManager.Instance;
        }

        if (uiManager != null)
        {
            uiManager.SetTotalObjectCount(totalClockCount);
            uiManager.UpdateActiveObjectCount(activeClockCount, totalClockCount);
        }

        // 更新ConfigurationManager（如果有）
        if (configurationManager == null && ConfigurationManager.Instance != null)
        {
            configurationManager = ConfigurationManager.Instance;
        }

        if (configurationManager != null)
        {
            // 通知ConfigurationManager更新状态
            // 这里可能需要ConfigurationManager提供相应接口
        }
    }

    #endregion

    #region 数据结构

    /// <summary>
    /// 对象管理器统计信息
    /// </summary>
    [System.Serializable]
    public class ObjectManagerStats
    {
        public int totalClocks;
        public int activeClocks;
        public float activeRatio;
        public ExperimentController.ExperimentMode currentMode;
        public int nextStartTime;
        public int observableStatesCount;  // ObservableRecordState数量
    }

    #endregion

    #region 工具方法

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

    #region 编辑器扩展

    /// <summary>
    /// 在Inspector中添加测试按钮
    /// </summary>
    [ContextMenu("生成100个时钟")]
    private void Editor_Generate100Clocks()
    {
        GenerateClocksWithTimeOffset(100, nextClockStartTime);
    }

    [ContextMenu("追加50个时钟")]
    private void Editor_Add50Clocks()
    {
        GenerateClocksWithTimeOffset(50, nextClockStartTime);
    }

    [ContextMenu("切换到传统模式")]
    private void Editor_SwitchToTraditional()
    {
        SwitchMode(ExperimentController.ExperimentMode.Traditional);
    }

    [ContextMenu("切换到惰性模式")]
    private void Editor_SwitchToLazy()
    {
        SwitchMode(ExperimentController.ExperimentMode.LazyUpdate);
    }

    [ContextMenu("清除所有时钟")]
    private void Editor_ClearAll()
    {
        DestroyAllObjects();
    }

    [ContextMenu("打印ObservableState状态")]
    private void Editor_LogObservableStates()
    {
        Debug.Log($"[ObjectManager] ObservableRecordState映射表状态:");
    }

    #endregion

    #region Gizmos

    /// <summary>
    /// 在Scene视图中绘制调试信息
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // 绘制活跃时钟（绿色）
        Gizmos.color = Color.green;
        foreach (Clock clock in allClocks)
        {
            if (clock != null && clock.IsActive)
            {
                Gizmos.DrawWireSphere(clock.transform.position, 0.5f);
            }
        }

        // 绘制边界
        if (allClocks.Count > 0)
        {
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool boundsInitialized = false;

            foreach (Clock clock in allClocks)
            {
                if (clock != null)
                {
                    if (!boundsInitialized)
                    {
                        bounds = new Bounds(clock.transform.position, Vector3.zero);
                        boundsInitialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(clock.transform.position);
                    }
                }
            }

            if (boundsInitialized)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(bounds.center, bounds.size + Vector3.one * 2);
            }
        }

        // 绘制网格原点
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(Vector3.zero, 0.3f);
    }

    #endregion
}