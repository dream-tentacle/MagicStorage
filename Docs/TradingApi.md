# 魔法存储公开交易接口（API 2）

RimWorld 1.6 与 1.7 使用相同的公开类型和签名，程序集、命名空间均为 `MagicStorage`。编译时引用对应游戏版本的 `MagicStorage.dll`，不要把本体 DLL 复制进补丁的 Assemblies。硬依赖补丁应在 About.xml 中声明 `mjcg.magicstorage` 并在其后加载。

公开入口：

```csharp
StorageTradeOpenResult StorageTradeApi.Open(StorageTradeOptions options)
StorageTradeInventoryResult StorageTradeApi.CreateInventory(StorageTradeOptions options)
```

`StorageTradeApi.ApiVersion` 当前为 `2`。API 2 新增不依赖窗口的库存与预留接口，以及地图外随行储存单元模式；保留 API 1 的 `Open` 签名、默认行为、原有属性与枚举数值。补丁按能力判断 `ApiVersion >= 2`，不要使用相等判断来拒绝未来版本。不要反射调用内部 `StorageTradeSession.Open`。

## 调用时机与职责

所有接口均在游戏主线程调用。地图内的 `Open` 在谈判者到达交互位置后的 Job/Toil 中调用；`Portable` 模式在载具等对象确认可以交易后调用。接口不负责创建右键选项、寻路、预留摊位或通讯台，也不负责检查模组自有的营业时间、费用、声望等限制。调用方负责这些业务规则，通过 `IsStillValid` 持续验证。

始终传入原有 `ITrader` 实例。魔法存储负责自己的物品列表、白银、预留和扣除；买卖交付仍调用这个商人的原有接口。买入物品、卖出所得白银如何交付由商人决定，不会自动改成存入核心。普通交易也不会被本接口自动打开。

## 配置

| 属性 | 规则 |
| --- | --- |
| `Negotiator` | 必填。地面、轨道模式要求已生成且位于地图上；`Portable` 要求未生成且未被摧毁。 |
| `Trader` | 必填，原始 `ITrader`。需仍可交易，并满足谈判者的交易许可。 |
| `Mode` | 默认 `Ground`。轨道交易明确设为 `Orbital`；地图外随行库存明确设为 `Portable`。不根据商人的继承关系推断模式。 |
| `GroundTarget` | 地面交易的实际交互对象。普通 Pawn 商人可省略，默认使用商人自身；自定义地面商人必须提供。目标必须存活、已生成并位于谈判者地图。Pawn 商人自身也必须仍位于同图且未被遣散。 |
| `CommsConsole` | 仅用于轨道交易；轨道模式还要求商人为当前地图的在轨 `PassingShip` 并实现 `ITrader`（包括原版 `TradeShip` 和米莉拉帝国商船），通讯台可用，谈判者能说话。地面模式不能传入。 |
| `IsStillValid` | `Portable` 必填，其他模式可选。开窗、刷新、成交及预留有效性检查时可能多次调用；必须快速、无副作用，不应开窗、改动物品或启动任务。闭包可以读取实时状态。返回 false 时不可成交；抛异常会记录日志并使本次库存/会话永久失效，关闭后可重新请求。 |
| `NetworkScope` | 默认 `AllEligible`；也可选择 `SpecifiedCores`。 |
| `Cores` | 仅搭配 `SpecifiedCores`，必须非空且不含 null，可传一个或多个核心，重复核心会去重。`AllEligible` 下传非空集合会被拒绝，避免误把指定范围扩大成全图。 |
| `Units` | 仅用于 `Portable`，必填、非空且不含 null，重复单元会去重。所有单元必须未生成、未被摧毁且属于玩家；任何一个失效则整个范围不可交易。其他模式不能传非空集合。 |

配置和核心集合在调用时复制，后续修改原配置不会改变窗口范围。委托仍读取它捕获的实时状态。

`Portable` 的 `Units` 同样在调用时复制；不能指定 `GroundTarget`、`CommsConsole` 或 `Cores`，`NetworkScope` 保持默认值。地图外不存在活动地图网络，库存层直接访问指定储存单元的真实容器。调用方必须在 `IsStillValid` 中持续核实载具仍在交易位置、商人与谈判者仍匹配、每个指定单元仍随行，以及业务所需的核心/供电条件。接口不推断载具类型，也不把起飞前网络中留在基地的单元加入交易。

`Portable` 通过 `Open` 开窗时使用原生式交易窗口，跳过依赖地图的筛选面板和动态交易窗口替换。商品分组和库存结算仍走共享逻辑。

## 不打开窗口的库存接口

拥有自己交易窗口或成交流程的模组调用 `CreateInventory`，无需重复实现魔法存储的扣货。它与 `Open` 使用相同的库存、资格检查与交易事务，但不修改 `TradeSession`、不添加窗口、不计算价格或自动成交。

返回状态使用独立的 `StorageTradeInventoryStatus`：`Ready`、`InvalidRequest`、`TraderUnavailable`、`NoEligibleInventory`、`Failed`。只有 `Ready` 带有需要释放的 `Inventory`；失败时 `Inventory` 为 null，`Reason` 提供诊断原因，不代表可以自动回退或打开另一个窗口。

`StorageTradeInventory` 的公开成员：

| 成员 | 用途 |
| --- | --- |
| `Items` | 最近一次刷新得到的真实物品引用只读列表，包括白银和合格的连接补货架商品，不包含商人库存。不要直接修改或从容器取出这些物品。 |
| `Available(Thing)` | 当前未被其他预留占用的可用数量。库存失效时返回 0；列表存在不保证数量不变。 |
| `UnavailableReason` | 当前交易参与者、库存范围或有效性回调的失效原因；null 表示当前可用。 |
| `Refresh()` | 释放本库存的所有预留，重新解析范围并构建列表，成功返回 true。不会扩大显式指定范围。 |
| `TryReserve(IEnumerable<StorageTradeItemRequest>, out StorageTradeReservation)` | 按真实物品堆及数量申请整批预留，失败时释放本次已经取得的预留。商品和支付用白银应放在同一批请求中。空批次允许；null 请求、非正数、重复物品堆或不属于本库存的物品均被拒绝。 |
| `LastFailureReason` | 最近一次刷新/预留失败的诊断文本或原因键。 |
| `Dispose()` | 使本库存失效并释放全部未结束预留，不扣货。 |

`StorageTradeItemRequest` 使用 `new StorageTradeItemRequest(item, count)` 创建。API 只预留调用方指定的物品堆，不按 DefName 重新匹配。地图网络使用既有网络预留；补货架和地图外单元也在库存实例之间共享交易预留。其他搬运/生产行为改变库存后，交付前会重新校验。

`StorageTradeReservation.IsValid` 检查整批剩余预留。`Deliver(Action<Thing, int>)` 先取出实际物品，再同步调用原商人的收货方法；回调必须接收、转移或消耗整份物品。它仅可执行一次，执行结束或抛异常时都会释放预留。回调未接收的剩余实物会恢复到来源；地图上无法恢复的物品使用既有恢复缓冲保存，地图外单元恢复时必要可暂时超出槽位上限以保留物品。

交付回调只能处理当前传入的物品；不要在回调中刷新/释放库存或改写本批其他来源物品。整个批次应在一次同步成交操作中使用，不跨游戏保存保留预留。已由商人接收的物品不回收，因此交付不是可撤销的跨商人事务：调用方应先完成全部价格、购买库存、费用及权限检查，再开始交付；发生部分交付后，不能重试整批或回退普通交易。

```csharp
var result = StorageTradeApi.CreateInventory(options);
if (result.Status != StorageTradeInventoryStatus.Ready)
    return; // 根据 result.Status / Reason 显示调用方提示

using (var inventory = result.Inventory)
{
    // 用 Items 和 Available 建立自己的列表，并完成价格、购买库存等检查。
    // requests 中包含本次卖出的真实商品堆，以及购买时支付的白银堆。
    StorageTradeReservation reservation;
    if (!inventory.TryReserve(requests, out reservation))
        return;
    using (reservation)
    {
        bool delivered = reservation.Deliver((thing, count) =>
            originalTrader.GiveSoldThingToTrader(thing, count, actor));
        // 由调用方记录实际交付结果，并通过原商人处理购买交付。
        // delivered == false 或异常后不得重新执行整笔交易。
    }
}
```

悬浮飞船一类接入只需提供原商人、船员、随船储存单元和实时有效性回调。若直接使用 `Open`，本体会自动完成清单和原版成交；若使用 `CreateInventory`，调用方保留自己的界面与成交编排。菜单和载具业务规则仍需由接入模组实现，本 API 不会自动给所有交易模组添加菜单。

### 已接入：GravShip Hover Trade

启用 `memer.gravshiphovertrade` 时，本体会在悬浮城镇的右键菜单和飞船按钮中追加魔法存储交易。适配层调用 API 2 的 `Portable` 模式，沿用该模组的 `GravshipTradeProxy`；买入物品和卖出所得白银仍交付到飞船货舱。

适配层按随船建筑的原始局部位置及朝向重建连接范围，使用地图网络相同的占地、四邻接与单核心规则；断开的单元、同一网络内有多个核心的单元，以及未随船的基地库存均不进入清单。直接读取模组提供的 `GravshipCargo.GetThings`，避免原版 `ThingPlacements` 旋转缓存提前固定交易前的落地物品清单。

开窗和成交时持续检查飞船仍悬浮在同一城镇、城镇可交易、谈判者仍在船上且符合社交/贸易许可，以及随船存储节点的成员、位置、朝向和玩家归属未变。魔法存储会话期间跳过对方追加普通货舱商品的补丁；普通悬浮交易仍使用对方原有清单与流程。此适配不增加新的公开 API，版本仍为 2。

## 地图内网络范围

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

为了保留 API 1 的状态值，`Portable` 通过 `Open` 调用时，指定单元失效也使用 `NoEligibleNetwork`；不打开窗口的 `CreateInventory` 使用独立的 `NoEligibleInventory` 状态。

## 接入示例

以下变量均为调用方已取得的游戏对象，代码放在到达交互位置后的动作中。

普通商人，使用默认全图合格网络：

```csharp
var options = new StorageTradeOptions { Negotiator = actor, Trader = merchant };
var result = StorageTradeApi.Open(options);
```

地图外随行库存（先由调用方固定本次的 `carriedUnits`，并在回调中持续核实归属）：

```csharp
var options = new StorageTradeOptions
{
    Negotiator = crewNegotiator,
    Trader = originalTrader,
    Mode = StorageTradeMode.Portable,
    Units = carriedUnits,
    IsStillValid = () => CarrierStillCanTradeWithTheseUnits()
};
var result = StorageTradeApi.Open(options);
// 如果接入方已有自己的窗口，改用 StorageTradeApi.CreateInventory(options)。
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

