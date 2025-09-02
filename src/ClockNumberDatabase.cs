using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 时钟数字网格数据库 - 存储0-9的数字网格资源
/// 用于时钟的数字显示
/// </summary>
public class ClockNumberDatabase : MonoBehaviour
{
    [Header("数字网格资源")]
    [Tooltip("数字0的网格")]
    public MeshFilter number0;
    public Mesh num0;

    [Tooltip("数字1的网格")]
    public MeshFilter number1;
    public Mesh num1;

    [Tooltip("数字2的网格")]
    public MeshFilter number2;
    public Mesh num2;

    [Tooltip("数字3的网格")]
    public MeshFilter number3;
    public Mesh num3;

    [Tooltip("数字4的网格")]
    public MeshFilter number4;
    public Mesh num4;

    [Tooltip("数字5的网格")]
    public MeshFilter number5;
    public Mesh num5;

    [Tooltip("数字6的网格")]
    public MeshFilter number6;
    public Mesh num6;

    [Tooltip("数字7的网格")]
    public MeshFilter number7;
    public Mesh num7;

    [Tooltip("数字8的网格")]
    public MeshFilter number8;
    public Mesh num8;

    [Tooltip("数字9的网格")]
    public MeshFilter number9;
    public Mesh num9;

    private void Awake()
    {
        num0 = number0.mesh;
        num1 = number1.mesh;
        num2 = number2.mesh;
        num3 = number3.mesh;
        num4 = number4.mesh;
        num5 = number5.mesh;
        num6 = number6.mesh;
        num7 = number7.mesh;
        num8 = number8.mesh;
        num9 = number9.mesh;

    }
}