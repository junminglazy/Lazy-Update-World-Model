using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 可观测状态管理器 - 负责所有惰性更新逻辑的核心组件
/// 管理ObservableRecordState并执行历史重构算法
/// </summary>
public class ObservableManager : MonoBehaviour
{
    #region 单例模式
    public static ObservableManager Instance { get; private set; }
    #endregion

    #region 统计数据

    [Header("更新统计")]
    [SerializeField] private int frameUpdateCount = 0;          // 当前帧更新数
    [SerializeField] private int totalUpdateCount = 0;          // 总更新次数
    [SerializeField] private float lastUpdateTime = 0f;         // 最后更新时间
    [SerializeField] private int registeredObjectCount = 0;     // 注册对象数

    [Header("性能设置")]
    [SerializeField] private int maxUpdatesPerFrame = 100;      // 每帧最大更新数
    [SerializeField] private bool enableUpdateThrottling = true; // 启用更新节流

    #endregion

    #region 系统引用

    [Header("系统引用")]
    [SerializeField] private ObjectManager objectManager;
    [SerializeField] private UIManager uiManager;
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

    #endregion

    #region 惰性更新核心逻辑

    /// <summary>
    /// 尝试更新被观测的对象（单个）
    /// </summary>
    public void UpdateStateOnObserve(GameObject targetObject, float currentTime)
    {
        // 1. 从ObjectManager获取该游戏对象对应的"数据档案"
        int searchIndex = objectManager.GetObserveClockID(targetObject);
        ObservableRecordState state = objectManager.GetObservableRecordState(searchIndex);
        if (state == null)
        {
            return; // 如果没有档案，直接返回
        }

        // 执行更新
        PerformUpdateState(targetObject, state, currentTime);
    }

    /// <summary>
    /// 批量更新被观测的对象
    /// </summary>
    public void BatchUpdateStateOnObserve(List<GameObject> objects, float currentTime)
    {
        foreach (GameObject obj in objects)
        {
            //print(obj.name);
            if (obj != null)
            {
                UpdateStateOnObserve(obj, currentTime);
            }
        }
    }

    /// <summary>
    /// 【核心算法】执行惰性更新和历史重构的精确算法。
    /// 这个函数是通用的，它不关心物体是"时钟"还是别的什么。
    /// </summary>
    /// <param name="targetObject">需要被更新的游戏对象</param>
    /// <param name="currentTime">当前的全局时间</param>
    private void PerformUpdateState(GameObject targetObject, ObservableRecordState state, float currentTime)
    {
        // 2. 【核心规则】防止在同一帧内被重复更新
        if (currentTime == state.lastObserveTime)
        {
            return; // 已被更新，直接跳出，不重复计算
        }

        // 3. 计算自上次观测以来经过的时间
        float timeElapsed = currentTime - state.lastObserveTime;

        // 调用演化函数
        if (state.evolution != null)
        {
            try
            {
                object oldState = state.currentState;
                // 4. 【历史重构】调用该物体自己的演化函数e()，直接计算出最终状态
                object newState = state.evolution(oldState, timeElapsed);

                // 6. 【应用更新】将计算出的新状态应用到实际的GameObject上
                state.applyStateAction?.Invoke(newState);

                // 5. 记录重构后的状态
                state.currentState = newState;
                //print(state.currentState);

                // 7. 更新观测时间戳，为下一次惰性更新做准备
                state.lastObserveTime = currentTime;

                // 记录更新
                frameUpdateCount++;
                totalUpdateCount++;
                lastUpdateTime = currentTime;

                // 8. 通知UI管理器
                if (uiManager != null)
                {
                    uiManager.LogObjectUpdate();
                }

                // 性能监控
                if (performanceMonitor != null)
                {
                    performanceMonitor.RecordLazyUpdate();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ObservableManager] 更新对象时出错: {targetObject.name}\n{e}");
            }
        }
    }

    #endregion

    #region 统计与监控

    /// <summary>
    /// 获取更新统计信息
    /// </summary>
    public UpdateStatistics GetUpdateStatistics()
    {
        return new UpdateStatistics
        {
            frameUpdateCount = frameUpdateCount,
            totalUpdateCount = totalUpdateCount,
            registeredObjectCount = registeredObjectCount,
            lastUpdateTime = lastUpdateTime
        };
    }

    /// <summary>
    /// 重置统计数据
    /// </summary>
    public void ResetStatistics()
    {
        totalUpdateCount = 0;
        frameUpdateCount = 0;
        lastUpdateTime = 0f;
        Debug.Log("[ObservableManager] 统计数据已重置");
    }

    #endregion

    #region 性能优化

    /// <summary>
    /// 设置每帧最大更新数
    /// </summary>
    public void SetMaxUpdatesPerFrame(int max)
    {
        maxUpdatesPerFrame = Mathf.Max(1, max);
        Debug.Log($"[ObservableManager] 每帧最大更新数设置为: {maxUpdatesPerFrame}");
    }

    /// <summary>
    /// 启用/禁用更新节流
    /// </summary>
    public void SetUpdateThrottling(bool enable)
    {
        enableUpdateThrottling = enable;
        Debug.Log($"[ObservableManager] 更新节流: {(enable ? "启用" : "禁用")}");
    }

    #endregion

    #region 清理

    /// <summary>
    /// 清空所有注册
    /// </summary>
    public void ClearAll()
    {
        registeredObjectCount = 0;
        ResetStatistics();

        Debug.Log("[ObservableManager] 所有注册已清空");
    }

    #endregion

    #region 数据结构

    /// <summary>
    /// 更新统计信息
    /// </summary>
    [System.Serializable]
    public class UpdateStatistics
    {
        public int frameUpdateCount;      // 当前帧更新数
        public int totalUpdateCount;      // 总更新次数
        public int registeredObjectCount; // 注册对象数
        public int queuedUpdates;         // 队列中的更新数
        public float lastUpdateTime;      // 最后更新时间
    }

    #endregion

    #region 调试

    /// <summary>
    /// 打印管理器状态
    /// </summary>
    [ContextMenu("Log Manager State")]
    public void LogManagerState()
    {
        Debug.Log("[ObservableManager] 状态报告:");
        Debug.Log($"- 注册对象: {registeredObjectCount}");
        Debug.Log($"- 总更新次数: {totalUpdateCount}");
        Debug.Log($"- 当前帧更新: {frameUpdateCount}");
        Debug.Log($"- 更新节流: {(enableUpdateThrottling ? $"启用 (最大{maxUpdatesPerFrame}/帧)" : "禁用")}");
        Debug.Log($"- 最后更新时间: {lastUpdateTime:F2}秒");
    }

    #endregion
}