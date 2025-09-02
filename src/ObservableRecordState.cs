using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 纯C#数据类，作为世界中所有惰性更新物体的“数据灵魂”。
/// 它独立于Unity的MonoBehaviour，非常轻量。
/// </summary>
[System.Serializable] // 这个属性让它可以在Unity的Inspector中显示出来
public class ObservableRecordState
{
    // --- 核心数据 ---
    public float timeElapsed; // 时间戳
    public float lastObserveTime;        // 最后一次被成功计算的时间戳
    public object currentState;         // 物体的当前状态

    // --- 通用逻辑委托 (Delegates) ---
    // 这两个委托是实现“客制化”的关键
    // 任何物体（时钟、苹果、门）都可以提供自己的逻辑，赋值给这两个委托

    // 演化函数e()的委托：(初始状态, 总流逝时间) => 计算出的新状态
    public System.Func<object, float, object> evolution;

    // 应用状态的委托：(目标GameObject, 要应用的新状态) => 无返回值
    public System.Action<object> applyStateAction;
}