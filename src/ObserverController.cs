using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class ObserverController : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] private float moveSpeed = 5.0f; // 控制箭头移动速度

    [Header("旋转设置")]
    [SerializeField] private float rotationSpeed = 120f; // 选中时的旋转速度

    [Header("射线设置")]
    [SerializeField] private float rayDistance = 20.0f; // 射线的最长距离
    [SerializeField] private Color rayColor = Color.green; // 射线颜色
    [SerializeField] private Color rayHitColor = Color.yellow; // 击中时的颜色
    [SerializeField] private float rayWidth = 0.1f; // 射线宽度
    [SerializeField] private float lastRaycastTime = 0f; 

    [Header("检测信息（只读）")]
    [SerializeField] private int detectedClockCount = 0; // 当前检测到的时钟数量
    [SerializeField] private List<string> detectedClockNames = new List<string>(); // 检测到的时钟名称列表
    [SerializeField] private string detectionStatus = "未检测"; // 检测状态描述

    // 组件引用
    private LineRenderer lineRenderer;
    private Renderer meshRenderer;
    private ObserverManager observerManager;
    private ExperimentController experimentController;

    // 状态
    private bool isSelected = false;
    private int observerIndex = -1;
    private ExperimentController.ExperimentMode currentMode;

    // 当前帧检测到的时钟
    private HashSet<Clock> currentFrameHitClocks = new HashSet<Clock>();
    private void Start()
    {
        // 初始化组件
        InitializeComponents();

        // 获取ExperimentController引用
        experimentController = ExperimentController.Instance;
        if (experimentController == null)
        {
            Debug.LogError("[ObserverController] 无法找到ExperimentController！");
        }
    }
    private void Update()
    {
        // 只有被选中的观测者才能移动和旋转
        if (isSelected)
        {
            HandleMovement();
            HandleRotation();
        }

        // 所有观测者都进行射线检测（在所有模式下）
        if (Time.time - lastRaycastTime > 0.1f)
        {
            HandleRaycastingAll();

            // 更新射线显示
            UpdateVisibleRay();
            lastRaycastTime = Time.time;
        }
    }

    /// <summary>
    /// 初始化组件
    /// </summary>
    private void InitializeComponents()
    {
        // 获取或添加LineRenderer
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        // 设置射线的属性
        lineRenderer.startWidth = rayWidth;
        lineRenderer.endWidth = rayWidth * 0.5f; // 末端稍细

        // 创建射线材质
        Material rayMaterial = new Material(Shader.Find("Sprites/Default"));
        rayMaterial.color = rayColor;
        lineRenderer.material = rayMaterial;

        lineRenderer.startColor = rayColor;
        lineRenderer.endColor = new Color(rayColor.r, rayColor.g, rayColor.b, 0.3f); // 末端半透明
        lineRenderer.positionCount = 2;

        // 获取渲染器组件
        meshRenderer = GetComponentInChildren<Renderer>();
        if (meshRenderer == null)
        {
            Debug.LogWarning("[ObserverController] 未找到Renderer组件！");
        }
    }

    /// <summary>
    /// 处理移动输入（只有选中的观测者响应）
    /// </summary>
    private void HandleMovement()
    {
        // 使用方向键而不是WASD
        float horizontal = 0f;
        float vertical = 0f;

        // 检测方向键输入
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            horizontal = 1f;
        }
        else if (Input.GetKey(KeyCode.RightArrow))
        {
            horizontal = -1f;
        }

        if (Input.GetKey(KeyCode.UpArrow))
        {
            vertical = 1f;
        }
        else if (Input.GetKey(KeyCode.DownArrow))
        {
            vertical = -1f;
        }

        // 检查是否有输入
        if (Mathf.Abs(horizontal) > 0.01f || Mathf.Abs(vertical) > 0.01f)
        {
            // 计算移动方向（在XZ平面上移动）
            Vector3 moveDirection = new Vector3(horizontal, vertical, 0).normalized;

            // 计算目标位置
            Vector3 targetPosition = transform.position + moveDirection * moveSpeed * Time.deltaTime;

            // 只检查是否会与其他观测者碰撞，不检查时钟
            if (observerManager != null && !observerManager.CheckCollisionWithOtherObservers(targetPosition, this))
            {
                // 执行移动
                transform.position = targetPosition;
            }
            else if (observerManager != null)
            {
                Debug.Log($"[ObserverController] {name} 无法移动：会与其他观测者碰撞");
            }
        }
    }

    /// <summary>
    /// 处理旋转效果（只有选中的观测者旋转）
    /// </summary>
    private void HandleRotation()
    {
        // 围绕世界Y轴旋转，保持箭头朝下
        transform.Rotate(0, rotationSpeed * Time.deltaTime, 0, Space.World);
    }

    // <summary>
    /// 处理射线检测与交互 - 持续更新所有检测到的时钟
    /// 核心改进：每帧都更新所有在射线中的时钟，而不只是新进入的
    /// </summary>
    private void HandleRaycastingAll()
    {
        Vector3 rayOrigin = transform.position;
        Vector3 rayDirection = Vector3.down;

        // 清空当前帧的检测结果
        currentFrameHitClocks.Clear();
        detectedClockNames.Clear();

        // 发射射线，获取所有击中对象
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, rayDirection, rayDistance);

        // 按距离排序
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        // 收集所有击中的时钟
        foreach (RaycastHit hit in hits)
        {
            Clock clock = hit.collider.GetComponentInChildren<Clock>();
            if (clock != null)
            {
                currentFrameHitClocks.Add(clock);
                detectedClockNames.Add(clock.name);
            }
        }

        detectedClockCount = currentFrameHitClocks.Count;
        // 【核心改进】在惰性模式下，持续更新所有被射线击中的时钟
        if (experimentController != null &&
            experimentController.CurrentMode == ExperimentController.ExperimentMode.LazyUpdate)
        {
            float currentTime = 0f;
            if (TimeManager.Instance != null)
            {
                currentTime = TimeManager.Instance.GetMainTime();
            }

            // 【关键修改】更新所有当前检测到的时钟，不管是不是新的
            if (currentFrameHitClocks.Count > 0)
            {
                List<GameObject> allClocksToUpdate = new List<GameObject>();

                // 收集所有需要更新的时钟对象
                foreach (Clock clock in currentFrameHitClocks)
                {
                    //print(clock.gameObject);
                    allClocksToUpdate.Add(clock.gameObject);
                }

                // 批量更新所有在射线中的时钟
                if (ObservableManager.Instance != null)
                {
                    ObservableManager.Instance.BatchUpdateStateOnObserve(allClocksToUpdate, currentTime);
                }
            }
        }
    }

    /// <summary>
    /// 更新可见射线的位置和颜色
    /// </summary>
    private void UpdateVisibleRay()
    {
        if (lineRenderer == null) return;

        // 射线的起点是观测者的位置
        Vector3 startPos = transform.position;
        lineRenderer.SetPosition(0, startPos);

        // 计算射线终点
        Vector3 endPos;

        // 获取第一个击中点（用于确定射线终点）
        RaycastHit firstHit;
        if (Physics.Raycast(startPos, Vector3.down, out firstHit, rayDistance))
        {
            endPos = firstHit.point;
        }
        else
        {
            // 没有击中，终点在最大距离处
            endPos = startPos + Vector3.down * rayDistance;
        }

        lineRenderer.SetPosition(1, endPos);

        // 根据检测到的时钟数量改变颜色
        if (detectedClockCount > 0)
        {
            // 击中时钟时使用特殊颜色，颜色强度根据数量变化
            float intensity = Mathf.Min(1f, detectedClockCount / 5f); // 5个时钟达到最大强度
            Color hitColor = Color.Lerp(rayHitColor, Color.red, intensity * 0.5f);

            lineRenderer.startColor = hitColor;
            lineRenderer.endColor = new Color(hitColor.r, hitColor.g, hitColor.b, 0.3f);

            // 根据击中数量调整线宽
            float widthMultiplier = 1f + (detectedClockCount * 0.1f);
            lineRenderer.startWidth = rayWidth * widthMultiplier;
            lineRenderer.endWidth = rayWidth * 0.5f * widthMultiplier;
        }
        else
        {
            // 没有击中时使用默认颜色
            lineRenderer.startColor = rayColor;
            lineRenderer.endColor = new Color(rayColor.r, rayColor.g, rayColor.b, 0.3f);
            lineRenderer.startWidth = rayWidth;
            lineRenderer.endWidth = rayWidth * 0.5f;
        }
    }

    /// <summary>
    /// 设置观测者是否被选中
    /// </summary>
    public void SetSelected(bool selected)
    {
        isSelected = selected;

        // 可以在这里添加选中/取消选中的视觉反馈
        if (isSelected)
        {
            Debug.Log($"[ObserverController] {name} 被选中");
        }
        else
        {
            Debug.Log($"[ObserverController] {name} 取消选中");
            // 停止旋转时保持当前朝向
        }
    }
    public List<Clock> GetHitClocksList()
    {
        List < Clock > clockList = new List<Clock>();
        foreach (Clock clock in currentFrameHitClocks)
        {
            clockList.Add(clock);
        }
        return clockList;
    }
    /// <summary>
    /// 设置观测者颜色
    /// </summary>
    public void SetColor(Color color)
    {
        if (meshRenderer != null)
        {
            meshRenderer.material.color = color;
        }
    }

    /// <summary>
    /// 设置观测者索引
    /// </summary>
    public void SetObserverIndex(int index)
    {
        observerIndex = index;
    }

    /// <summary>
    /// 设置ObserverManager引用
    /// </summary>
    public void SetObserverManager(ObserverManager manager)
    {
        observerManager = manager;
    }

    /// <summary>
    /// 设置实验模式
    /// </summary>
    public void SetExperimentMode(ExperimentController.ExperimentMode mode)
    {
        currentMode = mode;
        Debug.Log($"[ObserverController] {name} 设置模式为: {mode}");
    }

    /// <summary>
    /// 获取观测者是否被选中
    /// </summary>
    public bool IsSelected => isSelected;

    /// <summary>
    /// 获取观测者索引
    /// </summary>
    public int ObserverIndex => observerIndex;

    /// <summary>
    /// 获取当前检测到的时钟数量
    /// </summary>
    public int DetectedClockCount => detectedClockCount;

    /// <summary>
    /// 获取检测到的时钟名称列表
    /// </summary>
    public List<string> GetDetectedClockNames()
    {
        return new List<string>(detectedClockNames);
    }

    /// <summary>
    /// 获取当前帧检测到的所有时钟对象
    /// </summary>
    public List<Clock> GetCurrentDetectedClocks()
    {
        return new List<Clock>(currentFrameHitClocks);
    }

    /// <summary>
    /// 检查特定时钟是否在当前射线中
    /// </summary>
    public bool IsClockInRay(Clock clock)
    {
        return currentFrameHitClocks.Contains(clock);
    }

    /// <summary>
    /// 获取检测状态描述
    /// </summary>
    public string GetDetectionStatus()
    {
        return detectionStatus;
    }

    /// <summary>
    /// 在编辑器中绘制射线和检测信息（用于调试）
    /// </summary>
    private void OnDrawGizmos()
    {
        // 绘制射线
        if (Application.isPlaying && detectedClockCount > 0)
        {
            // 如果检测到时钟，射线显示为黄色到红色渐变
            float intensity = Mathf.Min(1f, detectedClockCount / 5f);
            Gizmos.color = Color.Lerp(Color.yellow, Color.red, intensity);
        }
        else
        {
            // 默认射线颜色
            Gizmos.color = isSelected ? Color.green : Color.cyan;
        }

        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * rayDistance);

        // 如果检测到时钟，在每个时钟位置画圈
        if (Application.isPlaying && currentFrameHitClocks != null && currentFrameHitClocks.Count > 0)
        {
            int index = 0;
            foreach (Clock clock in currentFrameHitClocks)
            {
                if (clock != null)
                {
                    // 第一个时钟（最近的）用更大的圈
                    float sphereSize = (index == 0) ? 0.5f : 0.3f;

                    // 根据距离改变颜色亮度
                    float brightness = 1f - (index * 0.15f);
                    brightness = Mathf.Max(0.3f, brightness);
                    Gizmos.color = new Color(1f, 1f, 0f, brightness);

                    Gizmos.DrawWireSphere(clock.transform.position, sphereSize);
                    index++;
                }
            }

            // 在观测者位置显示检测数量
            Gizmos.color = Color.white;
            Vector3 countPos = transform.position + Vector3.right * 1.5f;
#if UNITY_EDITOR
            UnityEditor.Handles.Label(countPos, $"[{detectedClockCount}]");
#endif
        }
    }

    /// <summary>
    /// 在场景视图中显示检测信息
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying)
        {
            Vector3 labelPos = transform.position + Vector3.up * 2;
            string info = $"{name}\n";
            info += $"模式: {currentMode}\n";
            info += $"═══════════════\n";
            info += $"当前射线检测: {detectedClockCount} 个时钟\n";

            if (detectedClockCount > 0 && detectedClockCount <= 8)
            {
                info += "当前击中的时钟:\n";
                foreach (string clockName in detectedClockNames)
                {
                    info += $"  • {clockName}\n";
                }
            }
            else if (detectedClockCount > 8)
            {
                info += "当前击中的时钟:\n";
                // 显示前4个和后2个
                for (int i = 0; i < 4 && i < detectedClockNames.Count; i++)
                {
                    info += $"  • {detectedClockNames[i]}\n";
                }
                info += $"  • ... ({detectedClockCount - 6} 个省略) ...\n";
                for (int i = detectedClockNames.Count - 2; i < detectedClockNames.Count; i++)
                {
                    if (i >= 0)
                        info += $"  • {detectedClockNames[i]}\n";
                }
            }

#if UNITY_EDITOR
            UnityEditor.Handles.Label(labelPos, info);
#endif
        }
    }
}