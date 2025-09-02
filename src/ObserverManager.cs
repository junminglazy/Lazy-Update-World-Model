using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ObserverManager : MonoBehaviour
{
    [Header("配置")]
    [Tooltip("观测者NPC的预制体")]
    [SerializeField] private GameObject observerPrefab;

    [Tooltip("ExperimentController引用")]
    [SerializeField] private ExperimentController experimentController;

    [Tooltip("观测者之间的间距")]
    [SerializeField] private float observerSpacing = 5.0f;

    [Tooltip("观测者的初始Y位置")]
    [SerializeField] private float observerYPosition = 5.0f;

    [Tooltip("选中观测者的高亮颜色")]
    [SerializeField] private Color selectedColor = Color.green;

    [Tooltip("未选中观测者的默认颜色")]
    [SerializeField] private Color normalColor = Color.gray;

    // 存储场景中所有活动观测者的列表
    private List<ObserverController> activeObservers = new List<ObserverController>();

    // 当前选中的观测者
    private ObserverController selectedObserver = null;

    // 观测者总数计数器
    private int totalObserverCount = 0;

    private void Awake()
    {
        // 确保初始化
        activeObservers = new List<ObserverController>();
        totalObserverCount = 0;
        Debug.Log($"[ObserverManager] Awake - 初始化完成");
    }

    private void Start()
    {
        // 验证设置
        if (observerPrefab == null)
        {
            Debug.LogWarning("[ObserverManager] 警告：观测者预制体未设置！");
        }
        Debug.Log($"[ObserverManager] Start - observerSpacing = {observerSpacing}, observerYPosition = {observerYPosition}");
    }

    private void Update()
    {
        // 处理鼠标点击选择观测者
        HandleObserverSelection();
    }

    /// <summary>
    /// 生成指定数量的观测者
    /// </summary>
    /// <param name="count">要生成的观测者数量</param>
    /// <param name="mode">当前实验模式</param>
    public void GenerateObservers(int count, ExperimentController.ExperimentMode mode)
    {
        if (observerPrefab == null)
        {
            Debug.LogError("[ObserverManager] 观测者预制体未设置！");
            return;
        }

        Debug.Log($"[ObserverManager] ========== 开始生成{count}个观测者 ==========");

        int successCount = 0;

        for (int i = 0; i < count; i++)
        {
            // 计算生成位置：第一个在(0,5,0)，第二个在(-5,5,0)，第三个在(-10,5,0)
            float xPosition = -i * observerSpacing;
            Vector3 spawnPosition = new Vector3(xPosition, observerYPosition, 0);

            Debug.Log($"[ObserverManager] 生成第{i + 1}/{count}个观测者，位置: {spawnPosition}");

            try
            {
                // 保持预制体的旋转设置（箭头朝下）
                Quaternion spawnRotation = Quaternion.Euler(0, 0, 180);
                GameObject newObserverObj = Instantiate(observerPrefab, spawnPosition, spawnRotation);

                if (newObserverObj == null)
                {
                    Debug.LogError("[ObserverManager] Instantiate返回null！");
                    continue;
                }

                newObserverObj.name = $"Observer_{totalObserverCount}";

                // 获取ObserverController组件
                ObserverController observer = newObserverObj.GetComponent<ObserverController>();
                if (observer != null)
                {
                    activeObservers.Add(observer);
                    observer.SetObserverIndex(totalObserverCount);
                    observer.SetObserverManager(this);
                    observer.SetExperimentMode(mode);

                    // 设置初始颜色
                    observer.SetColor(normalColor);

                    // 增加计数器
                    totalObserverCount++;
                    successCount++;

                    Debug.Log($"[ObserverManager] ✓ 成功生成 {newObserverObj.name} 在位置 {spawnPosition}");

                    // 如果没有选中的观测者，选中第一个
                    if (selectedObserver == null)
                    {
                        SelectObserver(observer);
                    }
                }
                else
                {
                    Debug.LogError("[ObserverManager] 生成的对象缺少ObserverController组件！");
                    Destroy(newObserverObj);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ObserverManager] 生成观测者时出错: {e.Message}\n{e.StackTrace}");
            }
        }

        Debug.Log($"\n[ObserverManager] ========== 生成完成 ==========");
        Debug.Log($"[ObserverManager] 成功生成 {successCount}/{count} 个观测者");
        Debug.Log($"[ObserverManager] 当前总数: {activeObservers.Count}");

        // 列出所有生成的观测者
        Debug.Log("[ObserverManager] 生成的观测者列表:");
        foreach (var observer in activeObservers)
        {
            if (observer != null)
            {
                Debug.Log($"  - {observer.name} at {observer.transform.position}");
            }
        }
    }

    /// <summary>
    /// 处理鼠标点击选择观测者
    /// </summary>
    private void HandleObserverSelection()
    {
        if (Input.GetMouseButtonDown(0)) // 左键点击
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                ObserverController clickedObserver = hit.collider.GetComponent<ObserverController>();
                if (clickedObserver != null && activeObservers.Contains(clickedObserver))
                {
                    SelectObserver(clickedObserver);
                }
            }
        }
    }

    /// <summary>
    /// 选中指定的观测者
    /// </summary>
    private void SelectObserver(ObserverController observer)
    {
        if (observer == null) return;

        // 取消之前选中的观测者
        if (selectedObserver != null)
        {
            selectedObserver.SetSelected(false);
            selectedObserver.SetColor(normalColor);
        }

        // 选中新的观测者
        selectedObserver = observer;
        selectedObserver.SetSelected(true);
        selectedObserver.SetColor(selectedColor);

        Debug.Log($"[ObserverManager] 选中观测者: {selectedObserver.name}");
    }

    /// <summary>
    /// 获取当前选中的观测者
    /// </summary>
    public ObserverController GetSelectedObserver()
    {
        return selectedObserver;
    }

    /// <summary>
    /// 检查指定位置是否会与其他观测者碰撞（移动时使用）
    /// </summary>
    public bool CheckCollisionWithOtherObservers(Vector3 position, ObserverController excludeObserver)
    {
        // 使用预制体上设置的BoxCollider进行检测
        // 获取排除对象的Collider
        Collider excludeCollider = excludeObserver.GetComponent<Collider>();

        // 在目标位置进行重叠检测
        Collider[] colliders = Physics.OverlapBox(
            position,
            excludeCollider.bounds.extents,
            excludeObserver.transform.rotation
        );

        foreach (var collider in colliders)
        {
            if (collider == excludeCollider) continue; // 排除自己

            // 只检测其他观测者
            if (collider.GetComponent<ObserverController>() != null)
            {
                return true; // 会碰撞
            }
        }

        return false; // 不会碰撞
    }

    /// <summary>
    /// 添加观测者（运行时）
    /// </summary>
    public void AddObservers(int count)
    {
        if (count <= 0)
        {
            Debug.LogWarning($"[ObserverManager] 无效的添加数量: {count}");
            return;
        }

        var mode = experimentController != null ? experimentController.CurrentMode : ExperimentController.ExperimentMode.Traditional;

        // 计算新观测者的起始位置
        int startIndex = activeObservers.Count;

        for (int i = 0; i < count; i++)
        {
            // 继续向负方向生成
            float xPosition = -(startIndex + i) * observerSpacing;
            Vector3 spawnPosition = new Vector3(xPosition, observerYPosition, 0);

            try
            {
                Quaternion spawnRotation = Quaternion.Euler(0, 0, 180);
                GameObject newObserverObj = Instantiate(observerPrefab, spawnPosition, spawnRotation);

                if (newObserverObj != null)
                {
                    newObserverObj.name = $"Observer_{totalObserverCount}";
                    ObserverController observer = newObserverObj.GetComponent<ObserverController>();

                    if (observer != null)
                    {
                        activeObservers.Add(observer);
                        observer.SetObserverIndex(totalObserverCount);
                        observer.SetObserverManager(this);
                        observer.SetExperimentMode(mode);
                        observer.SetColor(normalColor);
                        totalObserverCount++;

                        Debug.Log($"[ObserverManager] 添加观测者 {newObserverObj.name} 在位置 {spawnPosition}");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ObserverManager] 添加观测者时出错: {e.Message}");
            }
        }

        Debug.Log($"[ObserverManager] 添加了 {count} 个观测者，当前总数: {activeObservers.Count}");
    }

    /// <summary>
    /// 移除观测者（运行时）
    /// </summary>
    public void RemoveObservers(int count)
    {
        if (count <= 0 || count > activeObservers.Count)
        {
            Debug.LogWarning($"[ObserverManager] 无效的移除数量: {count}，当前只有 {activeObservers.Count} 个观测者");
            return;
        }

        // 确保至少保留一个观测者
        int maxRemove = Mathf.Min(count, activeObservers.Count - 1);
        if (maxRemove <= 0)
        {
            Debug.LogWarning("[ObserverManager] 必须至少保留一个观测者");
            return;
        }

        // 从后往前移除
        for (int i = 0; i < maxRemove; i++)
        {
            int lastIndex = activeObservers.Count - 1;
            ObserverController observer = activeObservers[lastIndex];

            // 如果要移除的是当前选中的观测者，选择另一个
            if (observer == selectedObserver && activeObservers.Count > 1)
            {
                // 选择第一个不是要被移除的观测者
                for (int j = 0; j < activeObservers.Count - 1; j++)
                {
                    if (activeObservers[j] != observer)
                    {
                        SelectObserver(activeObservers[j]);
                        break;
                    }
                }
            }

            // 移除
            activeObservers.RemoveAt(lastIndex);
            if (observer != null)
            {
                Destroy(observer.gameObject);
            }
        }

        // 如果没有观测者了，清空选中状态
        if (activeObservers.Count == 0)
        {
            selectedObserver = null;
        }

        Debug.Log($"[ObserverManager] 移除了 {maxRemove} 个观测者，剩余: {activeObservers.Count}");
    }

    /// <summary>
    /// 销毁所有观测者
    /// </summary>
    public void DestroyAllObservers()
    {
        Debug.Log($"[ObserverManager] 销毁所有 {activeObservers.Count} 个观测者");

        foreach (var observer in activeObservers)
        {
            if (observer != null)
            {
                Destroy(observer.gameObject);
            }
        }

        activeObservers.Clear();
        selectedObserver = null;
        totalObserverCount = 0;
    }

    /// <summary>
    /// 获取观测者数量
    /// </summary>
    public int GetObserverCount()
    {
        return activeObservers.Count;
    }

    /// <summary>
    /// 获取所有观测者列表
    /// </summary>
    public List<ObserverController> GetAllObservers()
    {
        return new List<ObserverController>(activeObservers);
    }
    public List<Clock> GetObserverOfHitClocks()
    {
        List<Clock> totalClocks = new List<Clock>();
        foreach (ObserverController observer in activeObservers)
        {
            List<Clock> clocks = new List<Clock>();
            clocks = observer.GetHitClocksList();
            foreach(Clock clock in clocks)
            {
                if(totalClocks.Count < 0)
                {
                    totalClocks.Add(clock);
                }
                else
                {
                    if (!totalClocks.Contains(clock))
                    {
                        totalClocks.Add(clock);
                    }
                }
            }
        }
        return totalClocks;
    }
    /// <summary>
    /// 配置所有观测者（保留接口兼容性）
    /// </summary>
    public void ConfigureObservers(ExperimentController.ExperimentMode mode)
    {
        Debug.Log($"[ObserverManager] 配置 {activeObservers.Count} 个观测者，模式: {mode}");

        // 可以在这里根据模式进行额外配置
        foreach (var observer in activeObservers)
        {
            if (observer != null)
            {
                // 根据模式设置观测者行为（如果需要）
                observer.SetExperimentMode(mode);
            }
        }
    }

    /// <summary>
    /// 获取观测者统计信息
    /// </summary>
    public ObserverStats GetStats()
    {
        return new ObserverStats
        {
            totalObservers = activeObservers.Count,
            selectedObserver = selectedObserver != null ? selectedObserver.name : "None",
            nextSpawnX = -(activeObservers.Count * observerSpacing),
            totalGenerated = totalObserverCount
        };
    }

    /// <summary>
    /// 观测者统计信息结构
    /// </summary>
    [System.Serializable]
    public struct ObserverStats
    {
        public int totalObservers;
        public string selectedObserver;
        public float nextSpawnX;
        public int totalGenerated;
    }

    /// <summary>
    /// 在编辑器中显示调试信息
    /// </summary>
    private void OnDrawGizmos()
    {
        // 显示下一个生成位置
        if (activeObservers != null)
        {
            Gizmos.color = Color.yellow;
            float nextX = -(activeObservers.Count * observerSpacing);
            Vector3 nextPos = new Vector3(nextX, observerYPosition, 0);
            Gizmos.DrawWireSphere(nextPos, 0.5f);
        }

        // 显示所有观测者的位置
        if (activeObservers != null)
        {
            foreach (var observer in activeObservers)
            {
                if (observer != null)
                {
                    Gizmos.color = observer == selectedObserver ? Color.green : Color.gray;
                    Gizmos.DrawWireSphere(observer.transform.position, 1f);
                }
            }
        }
    }

    /// <summary>
    /// 调试：显示当前状态
    /// </summary>
    [ContextMenu("Show Current State")]
    public void ShowCurrentState()
    {
        Debug.Log($"[ObserverManager] === 当前状态 ===");
        Debug.Log($"activeObservers.Count: {activeObservers.Count}");
        Debug.Log($"totalObserverCount: {totalObserverCount}");
        Debug.Log($"selectedObserver: {(selectedObserver != null ? selectedObserver.name : "None")}");
        Debug.Log($"observerSpacing: {observerSpacing}");
        Debug.Log($"observerYPosition: {observerYPosition}");

        if (activeObservers.Count > 0)
        {
            Debug.Log("观测者位置:");
            foreach (var obs in activeObservers)
            {
                if (obs != null)
                    Debug.Log($"  - {obs.name}: {obs.transform.position}");
            }
        }
    }
}