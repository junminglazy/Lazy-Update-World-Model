using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 时钟组件 - 管理时钟的显示和更新逻辑
/// 核心设计：
/// 1. 生成时初始化两种模式的数据集（无模式偏向）
/// 2. 传统模式完全不使用ObservableRecordState
/// 3. ObservableRecordState专门为惰性模式服务
/// </summary>
public class Clock : MonoBehaviour
{
    #region 更新模式定义

    public enum UpdateMode
    {
        Traditional,  // 传统模式 - 每帧自主更新，不使用ObservableRecordState
        Lazy         // 惰性模式 - 只在被观测时更新，使用ObservableRecordState
    }

    #endregion

    #region 基础信息（两种模式共享）

    [Header("== 时钟基础信息（共享）==")]
    [SerializeField] private int clockId = -1;                    // 时钟唯一ID
    [SerializeField] private int initialTimeInSeconds = 0;        // 初始时间（秒）
    [SerializeField] private UpdateMode currentMode = UpdateMode.Traditional;  // 当前模式

    #endregion

    #region 传统模式数据集

    /// <summary>
    /// 传统模式的数据集 - 实际运行的时钟数据
    /// 传统模式完全不依赖ObservableRecordState
    /// </summary>
    [System.Serializable]
    public class TraditionalClockState
    {
        [Header("传统模式时间（基于主时间计算）")]
        public int hour;
        public int minute;
        public int second;

        public void InitializeFromSeconds(int totalSeconds)
        {
            hour = (totalSeconds / 3600) % 24;
            minute = (totalSeconds / 60) % 60;
            second = totalSeconds % 60;
        }

        public void UpdateFromSeconds(int totalSeconds)
        {
            hour = (totalSeconds / 3600) % 24;
            minute = (totalSeconds / 60) % 60;
            second = totalSeconds % 60;
        }

        public string GetTimeString()
        {
            return $"{hour:D2}:{minute:D2}:{second:D2}";
        }
    }

    [Header("== 传统模式数据集 ==")]
    [SerializeField] private TraditionalClockState traditionalState = new TraditionalClockState();

    #endregion

    #region 惰性模式数据集（Inspector显示用）

    [System.Serializable]
    public class LazyClockState
    {
        [Header("=== 当前显示时间 ===")]
        public int hour;
        public int minute;
        public int second;

        [Header("=== ObservableRecordState 镜像 ===")]
        [Tooltip("当前状态（精确秒数，包含小数）")]
        public float currentStateSeconds = 0f;

        [Tooltip("显示用整数秒")]
        public int displaySeconds = 0;

        [Tooltip("最后观测时间")]
        public float lastObserveTime = 0f;

        [Tooltip("时间流逝")]
        public float timeElapsed = 0f;

        [Header("=== 函数状态 ===")]
        [Tooltip("有演化函数")]
        public bool hasEvolutionFunction = false;

        [Tooltip("有应用函数")]
        public bool hasApplyFunction = false;

        /// <summary>
        /// 从ObservableRecordState同步数据
        /// </summary>
        public void SyncFromObservableState(ObservableRecordState state)
        {
            if (state == null) return;

            // 同步当前状态（支持float和int）
            if (state.currentState is float floatSeconds)
            {
                currentStateSeconds = floatSeconds;
                displaySeconds = Mathf.FloorToInt(floatSeconds);
            }
            else if (state.currentState is int intSeconds)
            {
                // 兼容旧的int状态
                currentStateSeconds = intSeconds;
                displaySeconds = intSeconds;
                //UpdateDisplayFromSeconds(displaySeconds);
            }

            // 同步函数状态
            hasEvolutionFunction = (state.evolution != null);
            hasApplyFunction = (state.applyStateAction != null);
        }
        public bool CheckDisplayFromSecond()
        {
            int totalTime = hour + minute + second;
            if (totalTime == displaySeconds)
            {
                return true;
            }
            return false;
        }
        public void UpdateDisplayFromSeconds()
        {
            hour = (displaySeconds / 3600) % 24;
            minute = (displaySeconds / 60) % 60;
            second = displaySeconds % 60;
        }

        public string GetTimeString()
        {
            return $"{hour:D2}:{minute:D2}:{second:D2}";
        }
    }

    [Header("== 惰性模式显示（从ObservableRecordState派生）==")]
    [SerializeField] private LazyClockState lazyState = new LazyClockState();

    // 【核心】ObservableRecordState - 真正的状态源
    private ObservableRecordState observableState;

    #endregion

    #region 视觉组件

    [Header("== 视觉组件 ==")]
    [SerializeField] private GameObject pointerSeconds;
    [SerializeField] private GameObject pointerMinutes;
    [SerializeField] private GameObject pointerHours;

    [Header("数字显示网格")]
    public MeshFilter hourleft, hourright, minuteleft, minuteright, secondleft, secondright;
    private List<Mesh> numberMeshes;

    #endregion

    #region 运行状态

    [Header("== 运行状态 ==")]
    [SerializeField] private bool isActive = false;
    [SerializeField] private bool experimentStarted = false;

    #endregion

    #region 核心引用

    private TimeManager timeManager;

    #endregion

    #region Unity生命周期

    private void Update()
    {
        // 只有当实验开始且时间管理器存在时才更新
        if (timeManager != null && timeManager.IsExperimentRunning && !timeManager.IsPaused)
        {
            // 标记实验已开始
            if (!experimentStarted)
            {
                experimentStarted = true;
            }

            // 【核心原理】
            // 传统模式：在Update()中自主更新
            // 惰性模式：不在Update()中做任何事，等待观测者触发
            if (currentMode == UpdateMode.Traditional)
            {
                UpdateTraditionalMode();
            }
            // 惰性模式下什么都不做，这是核心设计
        }
        else
        {
            // 实验未开始或已暂停
            if (isActive && currentMode == UpdateMode.Traditional)
            {
                SetActiveState(false);
            }
        }
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化时钟
    /// 生成时初始化两种模式的数据集，不偏向任何模式
    /// ObservableRecordState也在此时创建，但只有惰性模式会使用它
    /// </summary>
    public void Initialize(int id, float initialSeconds, TimeManager tm, ClockNumberDatabase db)
    {
        clockId = id;
        initialTimeInSeconds = Mathf.FloorToInt(initialSeconds);
        timeManager = tm;

        if (db != null)
        {
            numberMeshes = new List<Mesh> {
                db.num0, db.num1, db.num2, db.num3, db.num4,
                db.num5, db.num6, db.num7, db.num8, db.num9
            };
        }

        // 初始化传统模式数据
        traditionalState.InitializeFromSeconds(initialTimeInSeconds);

        // 创建ObservableRecordState（真正的状态源）
        observableState = new ObservableRecordState
        {
            currentState = initialSeconds,           // 初始状态：秒数
            lastObserveTime = 0f,                    // 初始观测时间
            timeElapsed = 0f,
            evolution = ClockEvolution,              // 演化函数
            applyStateAction = ApplyStateToVisuals   // 应用函数
        };

        // 初始化惰性模式显示
        lazyState.SyncFromObservableState(observableState);

        currentMode = UpdateMode.Traditional;
        UpdateVisuals();
        SetActiveState(false);
        UpdateClockName();

        Debug.Log($"[Clock_{clockId}] 初始化完成:");
        Debug.Log($"  - 初始时间: {initialSeconds}秒");
        Debug.Log($"  - ObservableState.currentState: {initialSeconds}");
        Debug.Log($"  - 默认模式: {currentMode}");
    }
    /// <summary>
    /// 获取ObservableState引用（只有惰性模式使用）
    /// </summary>
    public ObservableRecordState GetObservableState()
    {
        return observableState;
    }

    #endregion

    #region 演化函数

    /// <summary>
    /// 时钟的演化函数 - 状态转换函数
    /// f(旧状态, 时间流逝) → 新状态
    /// 
    /// 对于时钟：
    /// - 状态 = 总秒数
    /// - 新状态 = 旧状态 + 流逝的秒数
    /// 
    /// 这个设计可以推广到其他物体：
    /// - 苹果：新鲜度 = 旧鲜度 - 腐烂量
    /// - 火焰：大小 = 旧大小 - 燃烧量
    /// </summary>
    private object ClockEvolution(object oldState, float timeElapsed)
    {
        if (oldState is float floatSeconds)
        {
            // 状态转换：旧秒数 + 流逝的秒数 = 新秒数
            float newSeconds = floatSeconds + timeElapsed;
            //int oldDisplay = Mathf.FloorToInt(floatSeconds);
            //int newDisplay = Mathf.FloorToInt(newSeconds);
            object newState = newSeconds;

            //状态检查
/*            int oldH = (oldDisplay / 3600) % 24;
            int oldM = (oldDisplay / 60) % 60;
            int oldS = oldDisplay % 60;

            int newH = (newDisplay / 3600) % 24;
            int newM = (newDisplay / 60) % 60;
            int newS = newDisplay % 60;*/

/*            Debug.Log($"[Clock_{clockId}] 状态演化:");
            Debug.Log($"  - 旧状态: {oldDisplay}秒 [{oldH:D2}:{oldM:D2}:{oldS:D2}]");
            Debug.Log($"  - 时间流逝: {timeElapsed:F2}秒 (取整: {timeElapsed}秒)");
            Debug.Log($"  - 新状态: {newSeconds}秒 [{newH:D2}:{newM:D2}:{newS:D2}]");*/

            return newState;
        }

        // 如果状态类型不匹配，返回原状态
        return oldState;
    }

    #endregion

    #region 模式切换

    public void SetUpdateMode(UpdateMode mode)
    {
        if (currentMode != mode)
        {
            currentMode = mode;

            if (mode == UpdateMode.Traditional)
            {
                if (timeManager != null && timeManager.IsExperimentRunning && !timeManager.IsPaused)
                {
                    SetActiveState(true);

                    // 根据主时间重新计算当前应该显示的时间
                    float mainTime = timeManager.GetMainTime();
                    int totalSeconds = initialTimeInSeconds + Mathf.FloorToInt(mainTime);
                    traditionalState.InitializeFromSeconds(totalSeconds);
                    UpdateVisuals();

                    Debug.Log($"[Clock_{clockId}] 切换到传统模式，时间校正: {traditionalState.GetTimeString()}");
                }
                else
                {
                    SetActiveState(false);
                }
            }
        }
    }

    #endregion

    #region 传统模式更新

    /// <summary>
    /// 传统模式下的自主更新
    /// 基于主时间计算当前应该显示的时间
    /// </summary>
    private void UpdateTraditionalMode()
    {
        if (!isActive)
        {
            SetActiveState(true);
        }

        // 【改进】基于主时间计算，而不是自己累加
        // 这样可以确保时间始终与主时间同步
        if (timeManager != null)
        {
            float mainTime = timeManager.GetMainTime();
            int totalSeconds = initialTimeInSeconds + Mathf.FloorToInt(mainTime);

            // 检查是否需要更新（每秒更新一次）
            int newHour = (totalSeconds / 3600) % 24;
            int newMinute = (totalSeconds / 60) % 60;
            int newSecond = totalSeconds % 60;

            // 只有时间改变时才更新
            if (newHour != traditionalState.hour ||
                newMinute != traditionalState.minute ||
                newSecond != traditionalState.second)
            {
                traditionalState.hour = newHour;
                traditionalState.minute = newMinute;
                traditionalState.second = newSecond;

                // 更新显示
                UpdateVisuals();
                UpdateClockName();
            }
        }
    }

    #endregion

    #region 惰性模式更新
    /// <summary>
    /// 延迟后设置为非活跃
    /// </summary>
    private IEnumerator DeactivateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (currentMode == UpdateMode.Lazy)
        {
            SetActiveState(false);
        }
    }

    /// <summary>
    /// 应用状态到视觉显示（由ObservableManager调用）
    /// 注意：ObservableRecordState.currentState已经被ObservableManager更新了
    /// 这里只需要同步显示
    /// </summary>
    public void ApplyStateToVisuals(object state)
    {
        if (currentMode != UpdateMode.Lazy)
        {
            Debug.LogWarning($"[Clock_{clockId}] 非惰性模式不应该调用ApplyStateToVisuals！");
            return;
        }

        // 【重要】不要修改observableState.currentState！
        // 它已经被ObservableManager更新了

        // 只需要同步显示
        if (observableState != null)
        {
            // 从ObservableRecordState同步到显示层
            lazyState.SyncFromObservableState(observableState);

            if(lazyState.CheckDisplayFromSecond())
            {
                return;
            }
            else
            {
                lazyState.UpdateDisplayFromSeconds();
                // 更新视觉
                UpdateVisuals();
                UpdateClockName();
            }

/*            // 临时激活效果
            if (!isActive)
            {
                SetActiveState(true);
                StartCoroutine(DeactivateAfterDelay(0.01f));
            }*/

            // 调试输出
/*            Debug.Log($"[Clock_{clockId}] 应用视觉更新:");
            Debug.Log($"  - 显示时间: {lazyState.GetTimeString()}");
            Debug.Log($"  - ObservableState.currentState: {observableState.currentState}");
            Debug.Log($"  - ObservableState.lastObserveTime: {observableState.lastObserveTime:F3}");*/
        }
    }
    #endregion

    #region 视觉更新

    /// <summary>
    /// 更新时钟的视觉显示
    /// </summary>
    private void UpdateVisuals()
    {
        int displayHour, displayMinute, displaySecond;

        // 根据模式选择显示的时间（从对应的数据集读取）
        if (currentMode == UpdateMode.Traditional)
        {
            displayHour = traditionalState.hour;
            displayMinute = traditionalState.minute;
            displaySecond = traditionalState.second;
        }
        else
        {
            displayHour = lazyState.hour;
            displayMinute = lazyState.minute;
            displaySecond = lazyState.second;
        }

        // 更新指针旋转
        float rotationSeconds = (360.0f / 60.0f) * displaySecond;
        float rotationMinutes = (360.0f / 60.0f) * displayMinute;
        float rotationHours = ((360.0f / 12.0f) * (displayHour % 12)) + ((360.0f / (60.0f * 12.0f)) * displayMinute);

        if (pointerSeconds) pointerSeconds.transform.localEulerAngles = new Vector3(0.0f, 0.0f, rotationSeconds);
        if (pointerMinutes) pointerMinutes.transform.localEulerAngles = new Vector3(0.0f, 0.0f, rotationMinutes);
        if (pointerHours) pointerHours.transform.localEulerAngles = new Vector3(0.0f, 0.0f, rotationHours);

        // 更新数字显示
        if (numberMeshes != null && numberMeshes.Count == 10)
        {
            if (hourleft) hourleft.mesh = numberMeshes[displayHour / 10];
            if (hourright) hourright.mesh = numberMeshes[displayHour % 10];
            if (minuteleft) minuteleft.mesh = numberMeshes[displayMinute / 10];
            if (minuteright) minuteright.mesh = numberMeshes[displayMinute % 10];
            if (secondleft) secondleft.mesh = numberMeshes[displaySecond / 10];
            if (secondright) secondright.mesh = numberMeshes[displaySecond % 10];
        }
    }

    /// <summary>
    /// 更新时钟名称
    /// </summary>
    private void UpdateClockName()
    {
        string timeStr = GetDisplayTime();
        string modeStr = currentMode == UpdateMode.Traditional ? "T" : "L";
        string activeStr = isActive ? "*" : " ";
        gameObject.name = $"Clock_{clockId:D4} [{modeStr}]{activeStr} {timeStr}";
    }

    /// <summary>
    /// 获取显示时间字符串
    /// </summary>
    private string GetDisplayTime()
    {
        if (currentMode == UpdateMode.Traditional)
        {
            return traditionalState.GetTimeString();
        }
        else
        {
            return lazyState.GetTimeString();
        }
    }

    /// <summary>
    /// 设置活跃状态
    /// </summary>
    private void SetActiveState(bool active)
    {
        if (isActive != active)
        {
            isActive = active;

            // 通知ObjectManager更新统计
            if (ObjectManager.Instance != null)
            {
                ObjectManager.Instance.UpdateActiveCount(active ? 1 : -1);
            }
        }
    }

    #endregion

    #region 公开接口

    /// <summary>
    /// 获取时钟ID
    /// </summary>
    public int ClockId => clockId;

    /// <summary>
    /// 获取当前模式
    /// </summary>
    public UpdateMode CurrentMode => currentMode;

    /// <summary>
    /// 获取初始时间（秒）
    /// </summary>
    public int InitialTimeInSeconds => initialTimeInSeconds;

    /// <summary>
    /// 获取是否活跃
    /// </summary>
    public bool IsActive => isActive;

    /// <summary>
    /// 获取当前显示的时间（用于验证）
    /// </summary>
    public (int hour, int minute, int second) GetCurrentTime()
    {
        if (currentMode == UpdateMode.Traditional)
        {
            return (traditionalState.hour, traditionalState.minute, traditionalState.second);
        }
        else
        {
            return (lazyState.hour, lazyState.minute, lazyState.second);
        }
    }

    /// <summary>
    /// 强制开始/停止时钟（调试用）
    /// </summary>
    public void ForceSetRunning(bool running)
    {
        if (running && timeManager != null && timeManager.IsExperimentRunning)
        {
            experimentStarted = true;

            // 传统模式下，设置为活跃并根据主时间校正
            if (currentMode == UpdateMode.Traditional)
            {
                SetActiveState(true);

                // 【重要】根据主时间重新校正时钟显示
                float mainTime = timeManager.GetMainTime();
                int totalSeconds = initialTimeInSeconds + Mathf.FloorToInt(mainTime);
                traditionalState.InitializeFromSeconds(totalSeconds);
                UpdateVisuals();

                Debug.Log($"[Clock_{clockId}] 强制启动 - 时间校正: {traditionalState.GetTimeString()} (初始{initialTimeInSeconds}秒 + 主时间{mainTime:F1}秒)");
            }
        }
        else
        {
            // 停止时只是设置为非活跃，不改变时间
            SetActiveState(false);

            if (currentMode == UpdateMode.Traditional)
            {
                Debug.Log($"[Clock_{clockId}] 强制停止 - 保持时间: {traditionalState.GetTimeString()}");
            }
        }
    }

    #endregion

    #region 调试

    /// <summary>
    /// 在场景视图中绘制调试信息
    /// </summary>
/*    private void OnDrawGizmosSelected()
    {
        Vector3 pos = transform.position + Vector3.up * 2;
        string info = $"Clock #{clockId}\n";
        info += $"Mode: {currentMode}\n";
        info += $"Active: {isActive}\n";
        info += $"Time: {GetDisplayTime()}\n";

        if (currentMode == UpdateMode.Lazy && observableState != null)
        {
            info += $"\n=== ObservableRecordState ===\n";
            info += $"Last Observe: {observableState.lastObserveTime:F1}s\n";
            info += $"Time Elapsed: {observableState.timeElapsed:F1}s\n";

            if (observableState.currentState is int reconSec)
            {
                info += $"Reconstructed: {reconSec}s\n";
            }

            info += $"Evolution: {(observableState.evolution != null ? "✓" : "✗")}\n";
            info += $"ApplyAction: {(observableState.applyStateAction != null ? "✓" : "✗")}\n";
        }
        else if (currentMode == UpdateMode.Traditional)
        {
            info += "\n传统模式：自主更新\n不使用ObservableRecordState";
        }

#if UNITY_EDITOR
        UnityEditor.Handles.Label(pos, info);
#endif
    }*/

    #endregion
}