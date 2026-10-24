# 魔法存储公开交易接口（API 1）

RimWorld 1.6 与 1.7 使用相同的公开类型和签名，程序集、命名空间均为 `MagicStorage`。编译时引用对应游戏版本的 `MagicStorage.dll`，不要把本体 DLL 复制进补丁的 Assemblies。硬依赖补丁应在 About.xml 中声明 `mjcg.magicstorage` 并在其后加载。

稳定入口：

```csharp
StorageTradeOpenResult StorageTradeApi.Open(StorageTradeOptions options)
```

`StorageTradeApi.ApiVersion` 当前为 `1`。以后通过增加可选配置扩展功能，保留此入口和现有属性、枚举值。不要再反射调用内部 `StorageTradeSession.Open`。

## 调用时机与职责

在游戏主线程、谈判者到达交互位置后的 Job/Toil 中调用。接口立即尝试开窗，不负责创建右键选项、寻路、预留摊位或通讯台，也不负责检查模组自有的营业时间、费用、声望等限制。调用方负责这些业务规则，通过 `IsStillValid` 持续验证。

始终传入原有 `ITrader` 实例。魔法存储负责自己的物品列表、白银、预留和扣除；买卖交付仍调用这个商人的原有接口。买入物品、卖出所得白银如何交付由商人决定，不会自动改成存入核心。普通交易也不会被本接口自动打开。

## 配置

| 属性 | 规则 |
| --- | --- |
| `Negotiator` | 必填，已生成且位于地图上的谈判者。 |
| `Trader` | 必填，原始 `ITrader`。需仍可交易，并满足谈判者的交易许可。 |
| `Mode` | 默认 `Ground`。轨道交易必须明确设为 `Orbital`，不根据 `TradeShip` 继承关系推断。 |
| `GroundTarget` | 地面交易的实际交互对象。普通 Pawn 商人可省略，默认使用商人自身；自定义地面商人必须提供。目标必须存活、已生成并位于谈判者地图。Pawn 商人自身也必须仍位于同图且未被遣散。 |
| `CommsConsole` | 仅用于轨道交易；轨道模式还要求商人为当前地图的在轨 `TradeShip`，通讯台可用，谈判者能说话。地面模式不能传入。 |
| `IsStillValid` | 可选 `Func<bool>`。开窗、刷新、成交及预留有效性检查时可能多次调用；必须快速、无副作用，不应开窗、改动物品或启动任务。闭包可以读取实时状态。返回 false 时不可成交；抛异常会记录日志并使本次会话永久失效，关闭后可重新请求。 |
| `NetworkScope` | 默认 `AllEligible`；也可选择 `SpecifiedCores`。 |
| `Cores` | 仅搭配 `SpecifiedCores`，必须非空且不含 null，可传一个或多个核心，重复核心会去重。`AllEligible` 下传非空集合会被拒绝，避免误把指定范围扩大成全图。 |

配置和核心集合在调用时复制，后续修改原配置不会改变窗口范围。委托仍读取它捕获的实时状态。

## 网络范围

`AllEligible` 在开窗和刷新时汇总同图所有符合魔法交易条件的网络。`SpecifiedCores` 只使用指定核心当前所属的网络；指定核心中任何一个失效、跨图、不可交易或网络不可工作，整个指定范围都不可成交，不会自动换成其他核心。

接口按核心重新解析网络，因此不要求补丁长期持有可能在重建后失效的 `StorageNetwork`。指定的是核心当前所属网络，重新布线后会随这个核心的实际网络变化。范围不会覆盖魔法存储原有的阵营、迷雾、可达性、附近禁止出售规则；轨道交易仍要求核心在供电交易信标范围内，地面交易不需要信标。

商品、白银、连接补货架的物品以及实际预留和扣除使用同一个范围，并扣除已被其他任务预留的数量。开窗时有符合条件的空网络也能成功，空库存不等于网络失效。开窗不锁定库存；来源变化后需要刷新，最终成交前仍会校验，不能用开窗成功保证稍后的成交成功。

集市补丁应自行确定一个绑定核心，让补货、库存显示和本次交易都使用它。若选择“最近核心”，距离规则和并列规则由补丁定义，本接口不会自动选择最近网络。

## 返回结果与回退

| `Status` | 含义 | `CanFallbackToVanilla` |
| --- | --- | --- |
| `Opened` | 对应会话和原生式交易窗口或已兼容的动态交易窗口均已建立。`Opened` 属性为 true。 | false |
| `TradeWindowAlreadyOpen` | 已存在原版、魔法或已兼容的动态交易窗口。 | false |
| `OpeningInProgress` | 重入请求，另一次开窗尚未结束。 | false |
| `InvalidRequest` | 配置缺失、枚举无效或参数组合不合法。 | false |
| `TraderUnavailable` | 商人、目标、权限或自定义有效性检查未通过。 | false |
| `NoEligibleNetwork` | 当前没有可用范围，或者指定核心中有不可用项。 | 正常为 true；若出现交易窗口则为 false |
| `OpenFailed` | 构造、插入、窗口替换或开窗后确认失败。 | 仅完整清理本次窗口、会话及预留，恢复全局交易状态且没有遗留新窗口时为 true |

`Reason` 为供作者排查的诊断文本，不是本地化玩家消息。请按 `Status` 选择补丁自己的提示。接口只承认自身窗口及已兼容的动态交易界面；未知窗口替换导致无法确认时，返回失败，并保守地禁止回退。

```csharp
var result = StorageTradeApi.Open(options);
if (result.Opened)
{
    // 仅在这里执行“魔法交易已打开”的后续逻辑。
    return;
}
if (result.CanFallbackToVanilla)
{
    OpenOriginalTrade(); // 调用方自己原有的开窗逻辑
    return;
}
// 已有窗口、正在开窗或参数/交易状态无效：不要再覆盖全局交易。
// 按 result.Status 决定提示或结束任务。
```

禁止仅凭“没有异常”、`Status != Opened` 或 `Reason == null` 决定成功或回退。

## 接入示例

以下变量均为调用方已取得的游戏对象，代码放在到达交互位置后的动作中。

普通商人，使用默认全图合格网络：

```csharp
var options = new StorageTradeOptions { Negotiator = actor, Trader = merchant };
var result = StorageTradeApi.Open(options);
```

摊位，使用调用方选定的核心；`StallStillValid()` 应检查营业状态及商人与摊位的绑定关系：

```csharp
var options = new StorageTradeOptions
{
    Negotiator = actor,
    Trader = merchant,
    Mode = StorageTradeMode.Ground,
    GroundTarget = stall,
    NetworkScope = StorageTradeNetworkScope.SpecifiedCores,
    Cores = new[] { boundCore },
    IsStillValid = () => StallStillValid()
};
var result = StorageTradeApi.Open(options);
```

落地穿梭机，即使商人继承 `TradeShip`，也明确使用地面模式；`ShuttleStillHasTrader()` 检查穿梭机仍然对应传入的同一个商人：

```csharp
var options = new StorageTradeOptions
{
    Negotiator = actor,
    Trader = landedTrader,
    Mode = StorageTradeMode.Ground,
    GroundTarget = shuttle,
    IsStillValid = () => ShuttleStillHasTrader()
};
var result = StorageTradeApi.Open(options);
```

轨道商船：

```csharp
var options = new StorageTradeOptions
{
    Negotiator = actor,
    Trader = ship,
    Mode = StorageTradeMode.Orbital,
    CommsConsole = console
};
var result = StorageTradeApi.Open(options);
```

