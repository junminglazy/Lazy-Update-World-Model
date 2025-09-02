using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static ExperimentController;

/// <summary>
/// 上帝视角相机控制器 - 管理实验的主相机系统
/// 支持两种模式：观察模式（不触发更新）和观测者模式（视野内触发更新）
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    #region 单例模式
    public static CameraController Instance { get; private set; }
    #endregion

    #region 相机模式定义

    public enum CameraMode
    {
        ExternalObserver,    // 外部观测者模式 - 纯观察，不触发任何更新
        InternalObserver     // 内部观测者模式 - 相机视野成为观测范围，触发更新
    }

    #endregion

    #region 配置参数

    [Header("相机模式")]
    [Tooltip("当前相机模式")]
    [SerializeField] private CameraMode currentMode = CameraMode.ExternalObserver;

    [Header("移动控制")]
    [Tooltip("相机移动速度")]
    [SerializeField] private float moveSpeed = 10f;

    [Tooltip("移动加速倍数（按Shift时）")]
    [SerializeField] private float speedMultiplier = 2f;

    [Tooltip("移动平滑度")]
    [SerializeField] private float moveSmoothing = 0.1f;

    [Header("缩放控制")]
    [Tooltip("缩放速度")]
    [SerializeField] private float zoomSpeed = 0.1f;

    [Tooltip("最小视野大小")]
    [SerializeField] private float minOrthoSize = 5f;

    [Tooltip("最大视野大小")]
    [SerializeField] private float maxOrthoSize = 50f;

    [Header("观测者模式设置")]
    [Tooltip("视野检测间隔（秒）")]
    [SerializeField] private float detectionInterval = 0.1f;

    [Tooltip("在观测者模式下显示视野边框")]
    [SerializeField] private bool showViewportBorder = true;

    [Header("视觉反馈")]
    [Tooltip("视野边框的LineRenderer")]
    [SerializeField] private LineRenderer viewportBorder;

    [Tooltip("观察模式边框颜色")]
    [SerializeField] private Color spectatorBorderColor = new Color(1f, 1f, 1f, 0.3f);

    [Tooltip("观测者模式边框颜色")]
    [SerializeField] private Color observerBorderColor = new Color(0f, 1f, 0f, 0.5f);

    #endregion

    #region 内部变量

    private Camera mainCamera;
    private ViewFieldDetector viewFieldDetector;
    private ExperimentController experimentController;
    private Vector3 targetPosition;
    private float targetOrthoSize;
    private float lastDetectionTime = 0f;
    private List<GameObject> objectsInView = new List<GameObject>();

    // 移动输入缓存
    private Vector3 moveInput;

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

        // 获取Camera组件
        mainCamera = GetComponent<Camera>();
        if (mainCamera == null)
        {
            Debug.LogError("[CameraController] 找不到Camera组件！");
            return;
        }

        viewFieldDetector = GetComponent<ViewFieldDetector>();
        experimentController = GetComponentInParent<ExperimentController>();

        // 确保是正交相机
        mainCamera.orthographic = true;

        // 初始化目标值
        targetPosition = transform.position;
        targetOrthoSize = mainCamera.orthographicSize;
    }

    private void Start()
    {
        // 创建视野边框
        CreateViewportBorder();

        // 设置初始模式
        SetCameraMode(currentMode);
    }

    private void Update()
    {
        // 处理输入
        HandleInput();

        // 平滑移动和缩放
        ApplyMovementAndZoom();

        if(experimentController.CurrentMode == ExperimentMode.Traditional)
        {
            currentMode = CameraMode.ExternalObserver;
        }
        // 更新视野边框
        //UpdateViewportBorder();

        // 在观测者模式下检测视野内的物体
/*        if (currentMode == CameraMode.Observer)
        {
            HandleObserverModeDetection();
        }*/
    }

    #endregion

    #region 输入处理

    /// <summary>
    /// 处理所有输入
    /// </summary>
    private void HandleInput()
    {
        // 移动输入 (仅WASD，方向键留给观测者控制)
        float horizontal = 0f;
        float vertical = 0f;

        // 使用WASD控制相机移动
        if (Input.GetKey(KeyCode.W)) vertical = 1f;
        if (Input.GetKey(KeyCode.S)) vertical = -1f;
        if (Input.GetKey(KeyCode.A)) horizontal = 1f;
        if (Input.GetKey(KeyCode.D)) horizontal = -1f;

        // 在XZ平面移动（俯视图）
        moveInput = new Vector3(horizontal, vertical, 0).normalized;

        // 速度加成（按住Shift）
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? moveSpeed * speedMultiplier : moveSpeed;
        Vector3 moveAmount = moveInput * currentSpeed * Time.deltaTime;
        targetPosition += moveAmount;

        // 缩放输入 (Q/E 或 鼠标滚轮)
        float zoomInput = 0f;
        if (Input.GetKey(KeyCode.Q)) zoomInput = 1f;
        if (Input.GetKey(KeyCode.E)) zoomInput = -1f;
        zoomInput += Input.GetAxis("Mouse ScrollWheel") * 10f;

        if (Mathf.Abs(zoomInput) > 0.01f)
        {
            targetOrthoSize *= 1f - (zoomInput * zoomSpeed);
            targetOrthoSize = Mathf.Clamp(targetOrthoSize, minOrthoSize, maxOrthoSize);
        }

        // 模式切换 (Tab)
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleCameraMode();
        }

        // 重置视角 (Home)
        if (Input.GetKeyDown(KeyCode.Home))
        {
            ResetCamera();
        }
    }

    #endregion

    #region 相机控制

    /// <summary>
    /// 应用平滑的移动和缩放
    /// </summary>
    private void ApplyMovementAndZoom()
    {
        // 平滑移动
        if (moveSmoothing > 0)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, 1f - moveSmoothing);
        }
        else
        {
            transform.position = targetPosition;
        }

        // 平滑缩放
        mainCamera.orthographicSize = Mathf.Lerp(mainCamera.orthographicSize, targetOrthoSize, 1f - moveSmoothing);
    }

    /// <summary>
    /// 重置相机到初始位置
    /// </summary>
    public void ResetCamera()
    {
        targetPosition = new Vector3(0, 50, 0);  // 默认高度50，更适合俯瞰整个场景
        targetOrthoSize = 25f;                   // 默认视野大小，能看到大部分时钟
        transform.rotation = Quaternion.Euler(90, 0, 0);  // 垂直向下看

        Debug.Log("[CameraController] 相机已重置");
    }

    #endregion

    #region 模式管理

    /// <summary>
    /// 设置相机模式
    /// </summary>
    public void SetCameraMode(CameraMode mode)
    {
        currentMode = mode;

        // 更新视野边框颜色
        if (viewportBorder != null)
        {
            viewportBorder.startColor = (mode == CameraMode.ExternalObserver) ? observerBorderColor : spectatorBorderColor;
            viewportBorder.endColor = viewportBorder.startColor;
        }

        // 清空之前的检测列表
        objectsInView.Clear();

        Debug.Log($"[CameraController] 切换到{(mode == CameraMode.ExternalObserver ? "外部观测者" : "内部观测者")}模式");

    }

    /// <summary>
    /// 切换相机模式
    /// </summary>
    public void ToggleCameraMode()
    {
        if(experimentController.CurrentMode == ExperimentMode.LazyUpdate)
        {
            CameraMode newMode = (currentMode == CameraMode.InternalObserver) ? CameraMode.ExternalObserver : CameraMode.InternalObserver;
            SetCameraMode(newMode);
        }
    }
    #endregion

    #region 观测者模式逻辑

    /// <summary>
    /// 处理观测者模式下的视野检测
    /// </summary>
    private void HandleObserverModeDetection()
    {
        // 按照设定的间隔检测
        if (Time.time - lastDetectionTime < detectionInterval)
        {
            return;
        }

        lastDetectionTime = Time.time;

        // 获取视野边界
        Bounds viewBounds = CalculateViewportBounds();

        // 查找视野内的所有时钟
        GameObject[] allClocks = GameObject.FindGameObjectsWithTag("Clock");
        objectsInView.Clear();

        foreach (GameObject clock in allClocks)
        {
            if (IsObjectInView(clock, viewBounds))
            {
                objectsInView.Add(clock);

                // 请求更新该时钟的状态
                if (ExperimentController.Instance != null)
                {
                    ExperimentController.Instance.RequestStateUpdate(clock);
                }
            }
        }

        // 更新UI显示
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateObjectsInViewCount(objectsInView.Count);
        }
    }

    /// <summary>
    /// 计算相机视野边界
    /// </summary>
    private Bounds CalculateViewportBounds()
    {
        float height = mainCamera.orthographicSize * 2f;
        float width = height * mainCamera.aspect;

        Vector3 center = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 size = new Vector3(width, 10f, height);  // Y轴给一个合理的高度范围

        return new Bounds(center, size);
    }

    /// <summary>
    /// 检查对象是否在视野内
    /// </summary>
    private bool IsObjectInView(GameObject obj, Bounds viewBounds)
    {
        // 简单的边界框检测
        return viewBounds.Contains(obj.transform.position);
    }

    #endregion

    #region 视觉反馈

    /// <summary>
    /// 创建视野边框
    /// </summary>
    private void CreateViewportBorder()
    {
        if (viewportBorder == null)
        {
            GameObject borderObj = new GameObject("ViewportBorder");
            borderObj.transform.SetParent(transform);
            viewportBorder = borderObj.AddComponent<LineRenderer>();

            // 设置LineRenderer属性
            viewportBorder.positionCount = 5;  // 矩形需要5个点（闭合）
            viewportBorder.loop = true;
            viewportBorder.startWidth = 0.2f;
            viewportBorder.endWidth = 0.2f;
            viewportBorder.material = new Material(Shader.Find("Sprites/Default"));
            viewportBorder.sortingOrder = 100;  // 确保在最前面
        }
    }

    /// <summary>
    /// 更新视野边框位置
    /// </summary>
    private void UpdateViewportBorder()
    {
        if (viewportBorder == null || !showViewportBorder) return;

        float height = mainCamera.orthographicSize * 2f;
        float width = height * mainCamera.aspect;

        // 计算四个角的位置（在相机空间中）
        Vector3[] corners = new Vector3[5];
        corners[0] = new Vector3(-width / 2f, 0, -height / 2f);  // 左下
        corners[1] = new Vector3(width / 2f, 0, -height / 2f);   // 右下
        corners[2] = new Vector3(width / 2f, 0, height / 2f);    // 右上
        corners[3] = new Vector3(-width / 2f, 0, height / 2f);   // 左上
        corners[4] = corners[0];  // 闭合

        // 转换到世界坐标
        for (int i = 0; i < corners.Length; i++)
        {
            corners[i] = transform.position + corners[i];
            corners[i].y = 0.1f;  // 稍微抬高一点避免Z-fighting
        }

        viewportBorder.SetPositions(corners);

        // 根据模式显示/隐藏边框
        viewportBorder.enabled = showViewportBorder && (currentMode == CameraMode.ExternalObserver || Input.GetKey(KeyCode.LeftControl));
    }

    #endregion

    #region 公开接口

    /// <summary>
    /// 获取当前相机模式
    /// </summary>
    public CameraMode CurrentMode => currentMode;

    /// <summary>
    /// 获取当前视野大小
    /// </summary>
    public float CurrentOrthoSize => mainCamera.orthographicSize;

    /// <summary>
    /// 获取当前位置
    /// </summary>
    public Vector3 CurrentPosition => transform.position;

    /// <summary>
    /// 获取视野内的对象数量
    /// </summary>
    public int ObjectsInViewCount => objectsInView.Count;

    /// <summary>
    /// 设置相机位置（用于快速定位）
    /// </summary>
    public void SetPosition(Vector3 position)
    {
        targetPosition = position;
        targetPosition.y = transform.position.y;  // 保持高度不变
    }

    #endregion
}