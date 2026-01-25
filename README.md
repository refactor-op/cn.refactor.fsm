# Refactor.Fsm

基于 R3 的声明式有限状态机（FSM）实现。

## 使用

### 基础用法

```csharp
using Refactor.Fsm;
using R3;

public enum AIState { Idle, Patrol, Chase, Attack }

public class AIContext
{
    public Transform Transform;
    public Animator Animator;
    public float IdleTime;
    public float EnemyDistance;
}

public class AIController : MonoBehaviour
{
    private Fsm<AIState, AIContext> _fsm;

    void Start()
    {
        var context = new AIContext
        {
            Transform = transform,
            Animator = GetComponent<Animator>()
        };

        using var builder = Fsms.Create<AIState, AIContext>();
        
        _fsm = builder
            .AddState(AIState.Idle)
                .Enter(ctx => {
                    ctx.Animator.Play("Idle");
                    ctx.IdleTime = 0f;
                })
                .Update(ctx => ctx.IdleTime += Time.deltaTime)
                .To(AIState.Patrol)
                    .When(ctx => ctx.IdleTime > 3f, priority: 10)
                .To(AIState.Chase)
                    .When(ctx => ctx.EnemyDistance < 10f, priority: 100)
            .End()
            
            .AddState(AIState.Patrol)
                .Enter(ctx => ctx.Animator.Play("Walk"))
                .Update(ctx => ctx.Transform.Translate(Vector3.forward * Time.deltaTime))
                .To(AIState.Chase)
                    .When(ctx => ctx.EnemyDistance < 10f, priority: 100)
                .To(AIState.Idle)
                    .When(ctx => Random.value < 0.01f, priority: 5)
            .End()
            
            .AddState(AIState.Chase)
                .Enter(ctx => ctx.Animator.Play("Run"))
                .Update(ctx => {
                    var dir = (enemy.position - ctx.Transform.position).normalized;
                    ctx.Transform.Translate(dir * 2f * Time.deltaTime);
                })
                .To(AIState.Attack)
                    .When(ctx => ctx.EnemyDistance < 2f, priority: 200)
                .To(AIState.Patrol)
                    .When(ctx => ctx.EnemyDistance > 20f, priority: 10)
            .End()
            
            .AddState(AIState.Attack)
                .Enter(ctx => ctx.Animator.Play("Attack"))
                .To(AIState.Chase)
                    .When(ctx => ctx.EnemyDistance > 3f, priority: 100)
            .End()
            
            .StartWith(AIState.Idle)
            .ContextWith(context)
            .Build();
    }

    void OnDestroy()
    {
        _fsm?.Dispose();
    }
}
```

### 修改状态

```csharp
builder
    .ModifyState(AIState.Idle)
        .Update(ctx => ctx.IdleTime += Time.deltaTime * 0.5f)
        .RemoveTo(AIState.Patrol)
        .To(AIState.Patrol)
            .When(ctx => ctx.IdleTime > 5f, priority: 10)
    .End();
```

### 零闭包

如果需要避免构建时的闭包分配，可以使用状态参数：

```csharp
// ❌ 可能产生闭包.
float threshold = 3f;
.When(ctx => ctx.IdleTime > threshold)

// ✅ 零闭包 (使用 static lambda).
.When(3f, static (ctx, threshold) => ctx.IdleTime > threshold)

// ✅ 传递 this 引用.
.When(this, static (ctx, self) => ctx.EnemyDistance < self._chaseRange)
```

### 静态工厂

```csharp
Fsms.Create<TState, TContext>(int stateCapacity = 8, int transitionCapacity = 8)
Fsms.From<TState, TContext>(Fsm<TState, TContext> fsm)  // 克隆现有 FSM.
```

### FsmBuilder

```csharp
.AddState(TState state)                    // 添加新状态.
.ModifyState(TState state)                 // 修改已有状态.
.RemoveState(TState state)                 // 删除状态.
.StartWith(TState state)                   // 设置初始状态.
.ContextWith(TContext context)             // 设置上下文.
.Build()                                   // 构建并自动开始驱动.
.Dispose()                                 // 释放资源.
```

### StateBuilder

#### 生命周期钩子

```csharp
.Initialization(Action<TContext>)          // FSM 初始化时执行一次.
.EarlyUpdate(Action<TContext>)             // 在 FixedUpdate 之前.
.FixedUpdate(Action<TContext>)             // 物理更新时机.
.PostFixedUpdate(Action<TContext>)         // 在 FixedUpdate 之后.
.PreUpdate(Action<TContext>)               // 在主 Update 之前.
.Update(Action<TContext>)                  // 主逻辑更新.
.PreLateUpdate(Action<TContext>)           // LateUpdate 早期阶段.
.PostLateUpdate(Action<TContext>)          // LateUpdate 晚期阶段.
.TimeUpdate(Action<TContext>)              // 受 Time.timeScale 影响的更新.
.Enter(Action<TContext>)                   // 进入状态时执行.
.Exit(Action<TContext>)                    // 离开状态时执行.
```

#### 生命周期钩子（别名）

```csharp
.LateUpdate(Action<TContext>)              // 等同于 PreLateUpdate.
```

> **推荐用法**：大部分场景只需要 `Update()`, `FixedUpdate()`, `LateUpdate()`, `Enter()`, `Exit()`。

#### 转换管理

```csharp
.To(TState target)                         // 添加转换.
.RemoveTo(TState target)                   // 删除到指定目标的转换.
.RemoveAllTo()                             // 删除所有转换.
.End()                                     // 结束当前状态定义.
```

### TransitionBuilder

```csharp
.When(Func<TContext, bool> predicate, int priority = 0)
// 定义转换条件和优先级 (默认优先级 0).

.When<TConditionState>(TConditionState state, 
                       Func<TContext, TConditionState, bool> predicate, 
                       int priority = 0)
// 零闭包版本：传递状态参数避免闭包.
```

**优先级规则**：
- 数值越大，优先级越高
- 多个转换同时满足时，执行优先级最高的
- 相同优先级按添加顺序执行
- 默认 priority = 0

### Fsm 实例

```csharp
ref readonly State<TState, TContext> CurrentState     // 当前状态定义.
ref TContext Context                                  // 上下文引用.
TState CurrentStateId                                 // 当前状态 ID.
void Dispose()                                        // 释放资源.
```

## 迭代历程

### OOP 范式探索

最初参考 QFramework，使用委托表示状态逻辑：

```csharp
fsm.AddState(GameState.Menu, () => Debug.Log("Entered Menu"));
```

**问题**：每个委托都是闭包对象（24+ bytes），10 个状态 = 240+ bytes。

转向接口方案：

```csharp
public interface IStateHandler<TState>
{
    void OnEnter(TState state, TState fromState);
    void OnExit(TState state, TState toState);
}
```

引入 Policy-Based 设计（受 Pooling 包启发）：

```csharp
Fsm<TState, TContext, TStackPolicy, TTransitionPolicy>
```

通过 Benchmark 发现 nullable 检查比泛型 Policy 更快（6.50ns vs 18.23ns），简化为：

```csharp
private IStackPolicy<TState>? _stackPolicy;  // null = 平面 FSM.
```

对比 UE5 State Tree 后，发现状态栈可以通过外部管理实现，删除内置栈。

删除 `ISuspendable`（与 `OnEnter`/`OnExit` 语义重叠）、Update 时间参数（Unity 有 `Time.deltaTime`）、拆分可选接口。

**Benchmark（接口版 vs Lambda 版）**：

| 操作 | 接口版 | Lambda 版 |
|------|--------|-----------|
| Update | 1.3ns | 17ns |
| GoTo | 11ns | 5ns |
| GC | 336 bytes | 672 bytes |

### 抽象类优化

将接口改为抽象类，性能提升：

| 操作 | 接口版 | 抽象类版 |
|------|--------|----------|
| Update (Mean) | 2.219ns | 1.708ns (-23%) |
| Update (Median) | 2.181ns | 1.605ns (-26%) |
| GoTo (Mean) | 12.084ns | 5.083ns (-58%) |
| GoTo (Median) | 12.001ns | 4.959ns (-59%) |

### 范式重构

**核心洞察**：

1. **转换条件应该显式定义**：不应散落在业务代码的 `if` 语句中
2. **OnUpdate 是状态内的持续副作用**：不应包含转换逻辑
3. **FSM 是 Observable over Time 的特化**：不是独立的底层抽象

重新设计 API：

```csharp
.AddState(AIState.Idle)
    .Update(ctx => ctx.IdleTime += Time.deltaTime)
    .To(AIState.Patrol)
        .When(ctx => ctx.IdleTime > 3f)
```

性能优化：只检查当前状态的出边（O(出边数) vs O(所有转换数)）。

**Benchmark**：

| 场景 | 抽象类 | 声明式 | 差异 |
|------|--------------|----------------|------|
| Update (无转换) | 0.004 ns | 2.658 ns | +2.65 ns |
| Transition | 5.083 ns | 14.859 ns | +9.78 ns |
| 运行时 GC | 0 | 0 | ✅ |

> 声明式转换虽慢 2.9 倍，但绝对值仍极快（14.9ns = 60fps 下的 0.00009% 帧预算）
> 10ns 换来代码清晰度和可维护性

### 性能

- **零分配运行时**：运行时无 GC（构建时使用 `ArrayPool`）
- **零闭包**：支持状态参数避免闭包（可选）
- **结构化优化**：只检查当前状态的转换（而非所有转换）
- **优先级排序**：构建时排序一次，运行时零开销
- **装箱方案优势**：
  - 构建时分配：24 bytes/转换（比闭包少 50%）
  - 运行时性能：2.8ns/op（比闭包快 12.5%）
  - 原理：静态委托 + 类型转换（JIT 优化友好）

## 许可

MIT License

## 贡献

欢迎 Issue 与 PR。