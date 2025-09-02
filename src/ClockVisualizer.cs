using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 时钟视觉效果组件 - 负责时钟的视觉反馈
/// 包括边框颜色、更新特效等
/// </summary>
public class ClockVisualizer : MonoBehaviour
{
    #region 配置参数

    [Header("边框设置")]
    [Tooltip("边框渲染器")]
    [SerializeField] private Renderer borderRenderer;

    [Tooltip("边框材质")]
    [SerializeField] private Material borderMaterial;

    [Header("状态颜色")]
    [Tooltip("静止状态颜色")]
    [SerializeField] private Color inactiveColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    [Tooltip("更新中颜色")]
    [SerializeField] private Color activeColor = new Color(0f, 1f, 0f, 1f);

    [Tooltip("刚被激活颜色")]
    [SerializeField] private Color activatedColor = new Color(1f, 1f, 0f, 1f);

    [Tooltip("相机视野内颜色（可选）")]
    [SerializeField] private Color inCameraViewColor = new Color(0f, 0.5f, 1f, 1f);

    [Header("特效设置")]
    [Tooltip("激活特效持续时间")]
    [SerializeField] private float activationEffectDuration = 0.5f;

    [Tooltip("闪烁速度")]
    [SerializeField] private float blinkSpeed = 4f;

    [Tooltip("发光强度")]
    [SerializeField] private float glowIntensity = 2f;

    #endregion

    #region 内部变量

    private Material materialInstance;
    private float effectTimer = 0f;
    private Color currentColor;
    private bool isInCameraView = false;

    #endregion

    #region Unity生命周期

    private void Awake()
    {
        // 创建材质实例
        if (borderRenderer != null && borderMaterial != null)
        {
            materialInstance = new Material(borderMaterial);
            borderRenderer.material = materialInstance;
        }

        // 初始颜色
        SetColor(inactiveColor);
    }

    private void Update()
    {
        // 更新特效
        if (effectTimer > 0)
        {
            effectTimer -= Time.deltaTime;
            UpdateActivationEffect();
        }
    }

    #endregion

    #region 公开接口

    /// <summary>
    /// 显示更新特效
    /// </summary>
    public void ShowUpdateEffect()
    {
        effectTimer = activationEffectDuration;
    }

    /// <summary>
    /// 设置活跃状态
    /// </summary>
    public void SetActiveState(bool isActive)
    {
        if (effectTimer <= 0)  // 特效期间不改变基础颜色
        {
            SetColor(isActive ? activeColor : inactiveColor);
        }
    }

    /// <summary>
    /// 设置是否在相机视野内
    /// </summary>
    public void SetInCameraView(bool inView)
    {
        isInCameraView = inView;
        if (inView && effectTimer <= 0)
        {
            SetColor(inCameraViewColor);
        }
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 设置颜色
    /// </summary>
    private void SetColor(Color color)
    {
        currentColor = color;
        if (materialInstance != null)
        {
            materialInstance.color = color;

            // 如果材质支持发光
            if (materialInstance.HasProperty("_EmissionColor"))
            {
                materialInstance.SetColor("_EmissionColor", color * 0.5f);
            }
        }
    }

    /// <summary>
    /// 更新激活特效
    /// </summary>
    private void UpdateActivationEffect()
    {
        if (materialInstance == null) return;

        // 计算闪烁
        float t = Mathf.PingPong(Time.time * blinkSpeed, 1f);
        Color blinkColor = Color.Lerp(activeColor, activatedColor, t);

        // 应用颜色
        materialInstance.color = blinkColor;

        // 发光效果
        if (materialInstance.HasProperty("_EmissionColor"))
        {
            materialInstance.SetColor("_EmissionColor", blinkColor * glowIntensity * t);
        }
    }

    #endregion

    #region 边框创建辅助

    /// <summary>
    /// 创建边框（如果没有的话）
    /// </summary>
    [ContextMenu("Create Border")]
    private void CreateBorder()
    {
        if (borderRenderer != null) return;

        // 创建一个圆环作为边框
        GameObject borderObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        borderObj.name = "ClockBorder";
        borderObj.transform.SetParent(transform);
        borderObj.transform.localPosition = Vector3.zero;
        borderObj.transform.localScale = new Vector3(2.2f, 0.05f, 2.2f);

        // 移除碰撞体
        Destroy(borderObj.GetComponent<Collider>());

        borderRenderer = borderObj.GetComponent<Renderer>();

        Debug.Log("边框已创建");
    }

    #endregion
}