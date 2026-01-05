using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class BallAgent : Agent
{
    [Header("核心配置")]
    public Transform target; // 目标物体（需在面板赋值）
    public float moveSpeed = 10f; // 移动速度
    public float maxVelocity = 15f; // 最大速度上限
    public int maxStep = 1000; // 单轮最大步数（和面板配置一致）

    private Rigidbody rb;
    private bool isInitSuccess = false; // 初始化成功标记

    void Awake()
    {
        // 1. 校验Rigidbody组件
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("[BallAgent] 小球缺少Rigidbody组件！请添加后重试");
            isInitSuccess = false;
            return;
        }
        // 2. 配置Rigidbody（确保物理移动正常）
        rb.useGravity = true; // 启用重力
        rb.isKinematic = false; // 禁用运动学（否则无法施加力）
        rb.drag = 0.5f; // 增加拖拽，避免移动过快

        // 3. 校验目标物体
        if (target == null)
        {
            Debug.LogError("[BallAgent] 未赋值Target Transform！请在面板拖入目标物体");
            isInitSuccess = false;
            return;
        }

        isInitSuccess = true;
        Debug.Log("[BallAgent] 初始化成功！");
    }

    // 核心：处理AI输出的动作（控制小球移动）
    public override void OnActionReceived(ActionBuffers actions)
    {
        // 初始化失败则直接返回，避免报错
        if (!isInitSuccess || rb == null) return;

        // 连续动作：2维（X/Z轴移动）
        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];

        // 构造移动方向（仅水平方向）
        Vector3 moveDir = new Vector3(moveX, 0, moveZ).normalized;

        // 施加力让小球移动
        rb.AddForce(moveDir * moveSpeed, ForceMode.Force);
        // 限制最大速度，避免失控
        rb.velocity = Vector3.ClampMagnitude(rb.velocity, maxVelocity);

        // 调试：每10帧打印一次动作值（看AI是否输出动作）
        if (StepCount % 10 == 0)
        {
            Debug.Log($"[AI动作] X={moveX:F2}, Z={moveZ:F2} | 速度={rb.velocity.magnitude:F2}");
        }
    }

    // 手动控制（测试用：按WASD移动，验证物理逻辑）
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (!isInitSuccess) return;

        var continuousActions = actionsOut.ContinuousActions;
        // 键盘W/S/A/D控制移动
        continuousActions[0] = Input.GetAxis("Horizontal"); // A/D → X轴
        continuousActions[1] = Input.GetAxis("Vertical");   // W/S → Z轴
    }

    // 收集环境观测（让AI感知世界）
    public override void CollectObservations(VectorSensor sensor)
    {
        if (!isInitSuccess) return;

        // 1. 小球自身位置（3维）
        sensor.AddObservation(transform.position);
        // 2. 小球速度（3维）
        sensor.AddObservation(rb.velocity);
        // 3. 目标物体位置（3维）
        sensor.AddObservation(target.position);
        // 总观测维度：3+3+3=9 → 必须和Behavior Parameters的Vector Observation Space Size=9一致！
    }

    // 每帧更新奖励（驱动AI学习）
    void FixedUpdate()
    {
        if (!isInitSuccess || target == null) return;

        // 1. 计算到目标的距离
        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        // 2. 靠近目标加奖励（鼓励移动）
        float reward = 0.01f / Mathf.Max(distanceToTarget, 0.1f); // 避免除以0
        AddReward(reward);

        // 3. 到达目标：加大量奖励+结束本轮
        if (distanceToTarget < 1f)
        {
            AddReward(100f); // 正向奖励
            Debug.Log($"[训练成功] 到达目标！总奖励={GetCumulativeReward():F2}");
            EndEpisode();
        }

        // 4. 超时惩罚：结束本轮
        if (StepCount >= maxStep)
        {
            AddReward(-10f); // 负向惩罚
            Debug.Log($"[训练超时] 步数超限！总奖励={GetCumulativeReward():F2}");
            EndEpisode();
        }
    }

    // 本轮训练结束，重置场景
    public override void OnEpisodeBegin()
    {
        if (!isInitSuccess || rb == null) return;

        // 重置小球位置（回到原点）
        transform.position = new Vector3(0, 0.5f, 0);
        // 重置速度和旋转
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.rotation = Quaternion.identity;

        Debug.Log($"[场景重置] 新轮训练开始 | 步数重置为0");
    }

    // 可选：添加障碍碰撞检测（如果有障碍）
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            AddReward(-50f); // 撞障碍扣奖励
            Debug.Log($"[碰撞惩罚] 撞到障碍！总奖励={GetCumulativeReward():F2}");
            EndEpisode();
        }
    }
}