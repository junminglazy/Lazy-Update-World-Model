using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// 视野触发器检测器 - 使用Box Collider检测进入相机视野的时钟
/// 挂载在相机的子对象上，配合Box Collider使用
/// </summary>
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class ViewFieldDetector : MonoBehaviour
{
    #region 配置参数

    [Header("== 调试设置 ==")]
    [Tooltip("显示调试信息")]
    [SerializeField] private bool showDebugInfo = false;

    //[Tooltip("在Scene视图显示检测范围")]

    [Tooltip("检测到的时钟高亮颜色")]
    [SerializeField] private Color detectedClockColor = new Color(0, 1, 0, 0.3f);

    #endregion

    #region 内部变量

    private Camera parentCamera;
    private CameraController cameraController;
    private BoxCollider boxCollider;
    private Rigidbody rigidBody;

    [Header("检测信息（只读）")]
    [SerializeField] private int detectedClockCount = 0; // 当前检测到的时钟数量
    [SerializeField] private List<string> detectedClockNames = new List<string>(); // 检测到的时钟名称列表
    // 当前帧检测到的时钟                                                                            
    private HashSet<Clock> currentFrameHitClocks = new HashSet<Clock>();

    private bool isDetecting = false;
    private bool clearHitList = false;

    #endregion

    #region Unity生命周期

    private void Awake()
    {
        // 获取组件引用
        boxCollider = GetComponent<BoxCollider>();
        rigidBody = GetComponent<Rigidbody>();

        // 设置为触发器
        boxCollider.isTrigger = true;

        // 设置Rigidbody为Kinematic（不受物理影响）
        rigidBody.useGravity = false;
        rigidBody.isKinematic = true;

        // 获取父相机
        parentCamera = GetComponentInParent<Camera>();
        if (parentCamera == null)
        {
            Debug.LogError("[ViewFieldDetector] 找不到父相机！请将此组件放在相机的子对象上。");
            enabled = false;
            return;
        }

        // 获取CameraController
        cameraController = parentCamera.GetComponent<CameraController>();
        if (cameraController == null)
        {
            Debug.LogWarning("[ViewFieldDetector] 找不到CameraController组件。");
        }
        boxCollider.enabled = false;
    }
    private void Update()
    {
        // 只在观测者模式下工作
        if (cameraController != null && cameraController.CurrentMode == CameraController.CameraMode.InternalObserver)
        {
            // 动态更新Box Collider大小以匹配相机视野
            boxCollider.enabled = true;
            UpdateBoxColliderSize();
        }
        else
        {
            boxCollider.enabled = false;
            if(currentFrameHitClocks.Count > 0)
            {
                clearHitList = true;
            }
            if (clearHitList)
            {
                currentFrameHitClocks.Clear();
                detectedClockNames.Clear();
                detectedClockCount = currentFrameHitClocks.Count;
                clearHitList = false;
            }

        }
        // 批量更新逻辑
        if(isDetecting)
        {
            DetectObjectToLasyUpdate();
        }
    }
    public List<Clock> GetHitClocks()
    {
        List<Clock> result = new List<Clock>();
        foreach (Clock clock in currentFrameHitClocks)
        {
            result.Add(clock);
        }
        return result;
    }
    #endregion

    #region Box Collider管理

    /// <summary>
    /// 更新Box Collider大小以匹配相机视野
    /// </summary>
    private void UpdateBoxColliderSize()
    {
        if (parentCamera == null || boxCollider == null) return;

        // 计算相机视野大小
        float orthoSize = parentCamera.orthographicSize;
        float aspect = parentCamera.aspect;

        Vector3 position = parentCamera.transform.position;

        // 精确的相机视野大小
        float viewWidth = orthoSize * aspect * 2;
        float viewHeight = orthoSize * 2;

        // 根据设置决定是否扩展检测范围
        float finalWidth, finalHeight;
        // 精确匹配相机视野
        finalWidth = viewWidth;
        finalHeight = viewHeight;

        // 更新Box Collider大小
        Vector3 newSize = new Vector3(finalWidth, finalHeight,0);
        Vector3 newposition = new Vector3(position.x, position.y, 0);
        if (boxCollider.size != newSize)
        {
            boxCollider.size = newSize;
        }
/*        if(this.transform.position != newposition)
        {
            this.transform.position = newposition;
        }*/
    }

    #endregion

    #region 触发器检测

    private void OnTriggerStay(Collider collision)
    {
        Clock clock = collision.GetComponentInChildren<Clock>();
        if (clock != null)
        {
            if (!currentFrameHitClocks.Contains(clock))
            {
                currentFrameHitClocks.Add(clock);
                //detectedClockNames.Add(clock.name);
                detectedClockCount = currentFrameHitClocks.Count;
            }
        }
        isDetecting = true;
    }
    private void OnTriggerExit(Collider collision)
    {
        RemoveExitObject(collision.gameObject);
        isDetecting = false;
    }
    private void RemoveExitObject(GameObject target)
    {
        Clock clock = target.GetComponentInChildren<Clock>();
        if (clock != null)
        {
/*            string name = clock.gameObject.name.ToString();
            print(name);
            detectedClockNames.Remove(name);*/
            currentFrameHitClocks.Remove(clock);
            detectedClockCount = currentFrameHitClocks.Count;
        }
    }
    private void DetectObjectToLasyUpdate()
    {
        // 在惰性模式下，持续更新
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
                allClocksToUpdate.Add(clock.gameObject);
            }

            // 批量更新所有在射线中的时钟
            if (ObservableManager.Instance != null)
            {
                ObservableManager.Instance.BatchUpdateStateOnObserve(allClocksToUpdate, currentTime);
            }
        }
    }
    #endregion

    #region 调试

    /// <summary>
    /// 在Scene视图绘制检测范围
    /// </summary>

    /// <summary>
    /// 在Inspector显示统计信息
    /// </summary>
    private void OnGUI()
    {
        if (!showDebugInfo || !Application.isPlaying) return;

        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.alignment = TextAnchor.UpperLeft;
        style.normal.textColor = Color.white;

        string info = "视野检测器状态\n";
        info += "================\n";
        info += $"模式: {(cameraController != null ? cameraController.CurrentMode.ToString() : "未知")}\n";

        if (parentCamera != null)
        {
            info += $"\n相机信息:\n";
            info += $"视野大小: {parentCamera.orthographicSize:F1}\n";
            info += $"宽高比: {parentCamera.aspect:F2}\n";
        }

        if (boxCollider != null)
        {
            info += $"\nBox Collider:\n";
            info += $"大小: ({boxCollider.size.x:F1}, {boxCollider.size.y:F1}, {boxCollider.size.z:F1})\n";
            info += $"中心: ({boxCollider.center.x:F1}, {boxCollider.center.y:F1}, {boxCollider.center.z:F1})\n";
        }

        info += $"\n同步状态:\n";
        info += $"位置: {transform.localPosition}\n";
        info += $"旋转: {transform.localRotation.eulerAngles}\n";
        info += $"缩放: {transform.localScale}\n";

        GUI.Box(new Rect(Screen.width - 280, 10, 270, 320), info, style);
    }

    #endregion
}