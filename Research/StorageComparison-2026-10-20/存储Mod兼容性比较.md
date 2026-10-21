# 存储 Mod 兼容性比较

调查日期：2026-10-20。比较对象为当前工作区的「魔法存储」和下列 10 个 Steam 创意工坊 Mod。

后续状态：本报告保留调查时的分析。用户随后要求的飞船穿衣适配器、墨诺灵扣料、有效网络资源统计及公共物资事务改造已经实施，当前状态见文末；相关旧风险描述不再代表修改后的代码。

## 结论

魔法存储采用「真实 Thing 容器 + 网络内预留与转移 + 实体输入/输出口 + 按功能接入」的路线。相较于改写全局 StartJob、Reachability、ReservationManager 或物品生成流程的方案，它对普通搬运任务的影响范围较小，且不需要把物品简化成 Def 与数量再重建。但是，这种选择也意味着兼容功能必须逐项实现：资源栏统计、原版配方计数、材料搜索、实际扣料、交易和第三方自动机器，并不是同一条接口。

不能由此断言我们的 Mod 最兼容。当前代码至少存在三个值得优先处理或验证的边界：原版带品质/耐久/材质等筛选的产物计数可能漏掉内部库存；Monolyn 兼容扣料没有跨材料的原子性；输入口吸收物品时对两套原版预留管理器的检查不一致。施工、交易和运输功能也有各自的外部 API 与原版流程依赖。

在这 10 个样本中，OgreStack 与 Stack XXL 没有发现主程序集的 Harmony 方法补丁，但它们全局修改 ThingDef.stackLimit，仍会改变所有消费者的前提；Quantum 的补丁数量少，却接管读档时的全局 Spawn，并有重建 Thing 的路径。评价“侵入程度”应看改变了什么语义、影响哪些调用者，不能只数补丁。

## 调查范围与证据

先阅读本项目当前源码，再完成检索、SteamCMD 下载、内置浏览器留言翻页和下载版 DLL 反编译。未修改生产 C#/XML，未启动游戏，也未执行部署。

- 下载工具：`D:\steamcmd\steamcmd.exe`，RimWorld app ID 为 `294100`，匿名下载成功。下载根目录为 `D:\steamcmd\steamapps\workshop\content\294100`。
- “最新版”指调查时 SteamCMD 实际取得的工坊发布版本。按各包的 About/LoadFolders 选择运行 DLL；并非把 GitHub 开发分支当作已发布版。
- 版本时间、下载路径、主 DLL SHA256 和 Steam manifest 见 [download-manifest.json](D:/project/魔法存储/Research/StorageComparison-2026-10-20/download-manifest.json)。表中时间为北京时间。
- 使用 ilspycmd 反编译实际主 DLL；另检查了 Weapon Storage 与 Change Dresser 随包附带的 Mending 兼容 DLL。反编译器出现的 `Unknown result type`、属性参数解码失败等注释，可能来自缺少游戏引用，不作为 Mod 实际报错证据。
- 留言使用内置电脑工具浏览器逐页查看。只读前 5 页；不足 5 页的读完全部。合计 **36 页、1659 个留言条目**，含审核隐藏占位；隐藏内容无法分析。这里不是全部历史留言的统计抽样，也不能计算故障率。
- 原版机制通过 RimSage/RimSage2 核对。关键 1.7 方法原文保存在 [原版核对.md](D:/project/魔法存储/Research/StorageComparison-2026-10-20/原版核对.md)。1.6 样本按其自身代码分析，避免套用 1.7 的接口变化。
- 下文将「代码事实」「留言反馈」「机制推断」分开。留言中的大整合包问题缺少隔离复现时，不视为已经确认的 Mod 两两冲突。没有做运行时复现或性能基准。

选择排除了 Adaptive Storage Framework 与仅增加普通架子外观/容量的内容。Change Dresser 因包含自动换装、征召切装和服装回收而纳入；OgreStack、Stack XXL 作为全局堆叠路线的对照样本纳入。Project RimFactory 是大型自动化包，下面区分其存储模块和整个包的影响。

## 样本、版本与留言覆盖

| Mod / 工坊链接 | 本次发布更新时间 | 本次所审 DLL 的游戏目标 | 实读留言页 | 每页条目 |
|---|---|---|---|---|
| [【CA】数字存储 4.0](https://steamcommunity.com/sharedfiles/filedetails/?id=3623684313) | 2026-10-18 18:49 | 1.6 | 1–5 | 50 / 50 / 50 / 50 / 50 |
| [Digital Storage Unit](https://steamcommunity.com/sharedfiles/filedetails/?id=3050694903) | 2025-08-09 16:35 | 1.6 | 1–3，全部 | 50 / 50 / 26 |
| [Project RimFactory Revived](https://steamcommunity.com/sharedfiles/filedetails/?id=2033979700) | 2026-04-01 18:40 | 1.6，包根 Assemblies | 1–5 | 50 / 50 / 50 / 50 / 50 |
| [[KV] Infinite Storage (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2932669764) | 2026-06-04 04:09 | 1.6 | 1–3，全部 | 50 / 50 / 8 |
| [Quantum Storage Redux (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2777221052) | 2025-06-26 04:40 | 1.6 | 1，全部 | 45 |
| [[KV] Weapon Storage (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2989305606) | 2025-07-26 03:36 | 1.6 | 1，全部 | 29 |
| [Item Teleporter](https://steamcommunity.com/sharedfiles/filedetails/?id=2792849912) | 2025-07-24 03:18 | 1.6 | 1–5，全部 | 50 / 50 / 50 / 50 / 21 |
| [OgreStack](https://steamcommunity.com/sharedfiles/filedetails/?id=1447140290) | 2026-10-18 07:45 | 1.7 | 1–5 | 50 / 50 / 50 / 50 / 50 |
| [Stack XXL](https://steamcommunity.com/sharedfiles/filedetails/?id=826366050) | 2026-10-13 08:39 | 1.7 使用 v1.5+ DLL | 1–5 | 50 / 50 / 50 / 50 / 50 |
| [[KV] Change Dresser](https://steamcommunity.com/sharedfiles/filedetails/?id=2880268357) | 2026-10-18 19:52 | 1.7 | 1–3，全部 | 50 / 50 / 30 |

上述前七项的最新下载包仍以 1.6 为最高支持版本，不能因为现在游戏已是 1.7，就把在 1.7 中加载旧 DLL 的 MissingMethod/类型错误归因于搬运 Mod。Project RimFactory 作者在留言中提到 1.7 开发版；其 [PR #950](https://github.com/zymex22/Project-RimFactory-Revived/pull/950) 也说明新 Harmony 验证暴露旧转译 IL 的问题。这是开发分支的修复背景，本次下载的工坊包仍是上表版本。

## 侵入方式总表

“高/中/低”是本次静态分析的范围判断，不是兼容性评分，也不是实测稳定性排名。

| Mod | 库存/搬运核心路线 | 原版影响面 | 主要冲突触发条件 |
|---|---|---|---|
| 魔法存储 | 真实 ThingOwner，网络预留；实体口、货架与专用供料 | 中；多项补丁有自己的建筑/会话范围，另有资源计数与施工语义 | 第三方只扫描地面、绕过网络直接扣物品，或与施工/交易流程接管重叠 |
| CA 数字存储 4.0 | 真实容器、多个原版来源接口、SlotGroup 适配及大量消费补丁 | 高；材料、食物、放置、交易、燃料等 | 搜索可见与实际扣料协议不一致；自动吸收和任务争夺；其他来源被重复统计 |
| Digital Storage Unit | 物品仍在地图上，隐藏堆叠，以端口改写访问 | 高；StartJob、寻路、预留、Position、材料搜索 | 搬运 Mod 改写目标队列或 Toil，绕过端口/预留假设 |
| PRF Revived | 地图隐藏实体存储与离图容器并存，配套端口/自动化 | 高；存储访问加上整个自动化包的补丁 | 各存储类型可见性不同、端口重定向、机器人和普通 Pawn 假设不同 |
| Infinite Storage | 自有字典/链表保存真实物品，搜索时放出物品 | 高；材料可用性、查料、预留、交易、保存 | 查询引发放物品；其他扫描器、预留者与回收逻辑交错 |
| Quantum Storage Redux | QThing 中间表示、重排/重建与量子区域 | 高；包含读档时全局 Spawn 替代 | 自定义 Comp/身份没有完整复制；特殊储物建筑与生成补丁 |
| Weapon Storage | 隐藏武器库存与装备登记、装备行为整合 | 高；全局装备入口并涉及战斗/交易 | 副手、多武器、CE 等系统共享装备生命周期 |
| Item Teleporter | 在任务开始时把真实材料移到工作/搬运端 | 高；StartJob 与标准任务目标结构 | 修改相同任务的搬运 Mod、自定义 Job、无 Pawn 自动机器 |
| OgreStack | 全局修改堆叠 Def | 方法补丁低；数据语义全局 | 单件独有数据被合堆、分类规则、其他堆叠设置覆盖 |
| Stack XXL | 全局修改堆叠 Def，适用范围与 Ogre 不同 | 方法补丁低；数据语义全局 | 单堆被独占预留、消费者误把堆数当件数、CE 等再改堆叠 |
| Change Dresser | 最新版为地图实体储物 + 自动换装/回收 | 中至高；存储较原生，装备策略和跨图调度更广 | 征召/服装 AI 同时选装，其他地图的衣柜被选作回收目标 |

## 逐项核对

### 1. 【CA】数字存储 4.0

**代码事实。** 核心保存真实 Thing，暴露 IThingHolder、IHaulSource、IHaulDestination、IApparelSource 等接口，并用 SlotGroup 适配层暴露内容；不能把当前 4.0 概括成“只有数字台账”。同时广泛补入材料搜索、食物任务、预留、物品放置、燃料、交易等入口。其资源统计还处理其他 Mod 的 IHaulSource，因此与另一个给相同外部来源追加统计的补丁共存时，有重复计数的机制风险，是否实际发生取决于双方去重与范围。

自动吸收具有任务目标、预留、近期取出物品的保护逻辑；当前包已出现 Pick Up And Haul 兼容处理。另有可配置的全局 Fleck/Designation 性能补丁，开启时会扩大到存储以外的渲染流程，不能只按存储功能评价它的挂接范围。

**留言反馈。** 第 1 页可见 2026-10-19 关于抽取端与核心优先级引起等待的反馈；10 月中旬有食物吸取循环、CE 弹药数量可见但炮塔无法补弹、乞丐任务需手动取出的反馈。作者 10 月 18 日提到一批兼容/修复。第 2 页有 10 月 4 日交易中心购入后银币未扣的报告。后面页包含较旧 3.0 的幽灵库存、营养换算、基因包、交易等问题，不能据此认定 4.0 仍全部存在。[留言](https://steamcommunity.com/sharedfiles/filedetails/comments/3623684313)

**机制推断与比较。** 它的优势是外部工作与机器更容易发现库存；代价是“发现物品→选择物品→预留→扣除→失败回收”有更多跨 Mod 接口。只让 CE 看到弹药数字，并不能自动满足 CE 实际取弹所需的位置、容器和任务协议。我们的实体输出货架对这类消费者更直接，但内部库存不会自动兼容所有消费入口。

**代码定位：** [SlotGroup 适配](D:/project/魔法存储/Research/StorageComparison-2026-10-20/Decompiled/3623684313/DigitalStorage.decompiled.cs:2824)、[材料搜索](D:/project/魔法存储/Research/StorageComparison-2026-10-20/Decompiled/3623684313/DigitalStorage.decompiled.cs:5283)、[核心与接口](D:/project/魔法存储/Research/StorageComparison-2026-10-20/Decompiled/3623684313/DigitalStorage.decompiled.cs:21463)。

### 2. Digital Storage Unit

**代码事实。** 核心是地图上的隐藏物品与访问端口。它挂接 Pawn_JobTracker.StartJob、Reachability.CanReach、ReservationManager.Reserve、Thing.Position，以及查料/显示等入口。StartJob 中特判 `HaulToInventory`；HaulToContainer 的处理依赖 Toil 的 `debugName == "GotoThing"`。部分输入格 Reserve 分支直接返回成功，未建立普通原版预留记录。

**留言反馈。** 第 2 页 2024-01-22 的反馈明确提到关闭 Common Sense 的顺手搬运/库存搬运限制后循环消失；另有输入输出重复搬运报告。第 1 页作者解释物品仍在地面、仍会腐坏；2025 年有 Odyssey/重力飞船搬迁物品散出的反馈。[留言](https://steamcommunity.com/sharedfiles/filedetails/comments/3050694903)

**机制推断与比较。** PUAH、Common Sense 一类系统会改变拾取顺序、携带位置、目标队列和下一段任务。端口已把目标当成可远程访问，搬运 Mod 却把物品提前塞入背包时，双方保存的位置/数量假设就可能失效。依赖 Toil 名称也比明确接口脆弱。我们的输入口让原版完成放置后再吸收，不改全局任务与寻路，绕开这类重定向；但仍要保证不吸走其他系统正在操作的地面物品。

**代码定位：** [StartJob](D:/project/魔法存储/Research/StorageComparison-2026-10-20/Decompiled/3050694903/DigitalStorageUnit.decompiled.cs:2718)、[寻路](D:/project/魔法存储/Research/StorageComparison-2026-10-20/Decompiled/3050694903/DigitalStorageUnit.decompiled.cs:2814)、[预留](D:/project/魔法存储/Research/StorageComparison-2026-10-20/Decompiled/3050694903/DigitalStorageUnit.decompiled.cs:2944)。

### 3. Project RimFactory Revived

**代码事实。** 同包中存在不同存储路线：Mass Storage 是地图实体隐藏存储；Cold/Archival 一类使用离图容器，不能把它们的兼容性混为一谈。端口与 StartJob、物品位置、合堆、产物计数、资源计数等挂接配合。Cold Storage 代码明确不允许 Advanced IO。

重力飞船放置补丁尤其值得注意：前缀临时改全局 `ThingDef.building.maxItemsInCell`，后缀恢复并刷新存储。这是共享 Def 状态；该补丁没有对应 Finalizer，若中间抛异常，存在未完成恢复的路径。这是代码层面的异常安全风险，不等同于已在游戏复现。

**留言反馈。** 第 1 页作者在 2026-10-03 说明 Cold/Archival 内容不会供普通原版建造直接使用，需要输出；也有 Advanced IO 与 ASU 的限制说明。前 5 页有机器人、自动工作台和其他 Mod WorkGiver 对人类 Pawn 组件的假设问题。Better Workbench Management 的窗口冲突在较旧版本已被作者标注修复，不能列为本次 DLL 的既定缺陷。[留言](https://steamcommunity.com/sharedfiles/filedetails/comments/2033979700)

**机制推断与比较。** PRF 扩展了工作者、机器与物料调度三套体系，冲突来源比存储模块单独更多。一个自动机械臂能访问原版地面，不代表它能消费 Cold Storage。我们同样需要承认网络库存与实体输出之间的边界；专用供料可控，但需要为新的消费协议单独接入。

**代码定位：** [Cold Storage](D:/project/魔法存储/Research/StorageComparison-2026-10-20/Decompiled/2033979700/ProjectRimFactory.decompiled.cs:826)、[重力飞船放置](D:/project/魔法存储/Research/StorageComparison-2026-10-20/Decompiled/2033979700/ProjectRimFactory.decompiled.cs:17080)、[任务重定向](D:/project/魔法存储/Research/StorageComparison-2026-10-20/Decompiled/2033979700/ProjectRimFactory.decompiled.cs:17243)。

### 4. [KV] Infinite Storage (Continued)

**代码事实。** 用自有字典/链表保存真实 Thing，并在材料可用性查询或查料过程中放出物品。DoBill 的前置处理放出候选材料，后置处理再回收未选择部分；交易与保存前也有放出/回收流程。部分 CanReserve/Reserve/Release 分支绕过普通预留。另有 ListerThings.ThingsInGroup 的返回列表追加路径，因此需要注意返回集合是否为原版共享缓存。

**兼容机制。** 这一路线使看似只读的“检查有没有材料”产生物品位置与所有权副作用。另一个 Mod 若在检查期间缓存列表、预留物品或开启第二次材料搜索，后续回收就可能与其持有的引用交错。若追加的是原版持有的列表而非副本，还会扩大到之后的调用者。这里是对具体路径的风险分析，不把反编译结果直接当作所有情况下必现的问题。

**留言与比较。** 已查看全部 3 页；留言本身不足以为某一个搬运 Mod 建立隔离因果关系，因此本项判断主要依靠代码。我们的库存查询与真正移动物品分离，并通过自己的预留与转移操作提取，避免为了“让一次查询成功”先把材料散到地图。但第三方若只会地面查料，也就不会自然识别内部库存。[留言](https://steamcommunity.com/sharedfiles/filedetails/comments/2932669764)

**代码定位：** [可用性查询放出物品](D:/project/魔法存储/Research/StorageComparison-2026-10-20/Decompiled/2932669764/InfiniteStorage.decompiled.cs:2421)、[列表补入](D:/project/魔法存储/Research/StorageComparison-2026-10-20/Decompiled/2932669764/InfiniteStorage.decompiled.cs:2474)、[查料](D:/project/魔法存储/Research/StorageComparison-2026-10-20/Decompiled/2932669764/InfiniteStorage.decompiled.cs:2775)。

### 5. Quantum Storage Redux (Continued)

**代码事实。** QThing 记录用于量子重排的信息，某些合并/放出路径通过 `ThingMaker.MakeThing` 创建新物品，并手工复制品质、耐久、数量、材质等；基因包、未完成物品、尸体有特别处理。它不是对所有自定义 Comp 的通用完整深拷贝。其 GenSpawn.Spawn 前缀在 `respawningAfterLoad` 时调用自带 Spawn 实现并跳过原版，范围不限于本 Mod 的存储建筑。

储物设置查找还对 `RimWorld.Building_Storage` 使用精确类型判断，其他储物子类不一定进入同一处理路径。

**留言反馈。** 唯一一页中，2025 年有仍有空间却提示无量子空间的反馈；有小型化建筑、异种胚芽和 Altered Carbon 数据异常报告。2024 年的基因包问题有作者修复回复；更旧的相邻平台互吸也有间距限制修复，不能直接算作最新仍存在。[留言](https://steamcommunity.com/sharedfiles/filedetails/comments/2777221052)

**机制推断与比较。** 模组物品若带充能、独立 ID、蓝图、灵魂、基因或其他 Comp 状态，只复制几个字段就可能丢失状态。全局读档生成实现还易随游戏版本改变而落后。我们的真实 ThingOwner 转移不需要重建这些状态，优势较明确；但仍依赖物品自己的 CanStackWith 和所属 Mod 对合堆语义的实现。

**代码定位：** [读档 Spawn 替代](D:/project/魔法存储/Research/StorageComparison-2026-10-20/Decompiled/2777221052/QuantumStorageRedux.decompiled.cs:313)、[QThing.Make](D:/project/魔法存储/Research/StorageComparison-2026-10-20/Decompiled/2777221052/QuantumStorageRedux.decompiled.cs:1077)、[储物设置识别](D:/project/魔法存储/Research/StorageComparison-2026-10-20/Decompiled/2777221052/QuantumStorageRedux.decompiled.cs:1234)。

### 6. [KV] Weapon Storage (Continued)

**代码事实。** 它不只是武器仓库，还维护装备分配并挂接装备、掉落、战斗等行为。`Pawn_EquipmentTracker.AddEquipment` 的高优先级前缀会在已有 Primary 时先 Remove；这个前缀没有“玩家且存在 Weapon Storage”限制。后缀才有玩家与仓库判断。因此即使没有正在使用仓库，装备入口的语义也可能改变。

附带 MendingWeaponStoragePatch 会在原版查料失败后搜索仓库并放出损坏武器供修补，说明储存、装备与修补三套协议需要显式整合。

**留言反馈。** 原作者在 2023-06-21 解释副手/双持需要重构，并提到停止 CE 支持。2025-07-23 的 MissingMethod 报告早于 7 月 26 日版本更新，不能拿旧 DLL 的方法缺失证明当前包与搬运 Mod 冲突；10 月的幽灵重复报告来自约 400 Mod 环境，缺少隔离证据。[留言](https://steamcommunity.com/sharedfiles/filedetails/comments/2989305606)

**机制推断与比较。** Dual Wield、Simple Sidearms、CE 或自动换装系统都可能参与同一次装备变更；全局提前移除 Primary 会改变对方期望的装备状态与事件顺序。我们的存储不重写全局装备入口，避免这项耦合，但也没有自动获得它的整套武器管理功能。

**代码定位：** [AddEquipment 前缀](D:/project/魔法存储/Research/StorageComparison-2026-10-20/Decompiled/2989305606/WeaponStorage.decompiled.cs:341)、[Mending 查料接入](D:/project/魔法存储/Research/StorageComparison-2026-10-20/Decompiled/2989305606/MendingWeaponStoragePatch.decompiled.cs:69)。

### 7. Item Teleporter

**代码事实。** 通过 StartJob 识别标准 DoBill/HaulToCell 等任务，搬动 targetQueueB/countQueue 所指的真实材料，或直接完成一次物品传送并跳过任务。代码已包含避免同 SlotGroup 之间重复搬运的判断，也有显示、腐坏等挂接。

**留言反馈。** 多页反复提到 PUAH、Common Sense、While You're Up 等搬运交互；2024-09-25 有反馈明确指向 Common Sense 拾取设置。较旧的同储存区循环与机械孕育器问题有后续修复，最新代码也可见同组保护。另有 Misc. Robots、其他自定义研究工作，以及 PRF 存储容器传送后其他物品散出的报告。[留言](https://steamcommunity.com/sharedfiles/filedetails/comments/2792849912)

**机制推断与比较。** 若两个 Mod 都在 StartJob 改 targetQueue/countQueue，一个把材料移走，另一个仍按旧队列分拆与预留，就可能循环、失败或留下过期引用。无 Pawn 的自动机器不经过这个入口，因此也可能完全不受益。我们的专用建筑供料路径更明确，但覆盖外部工作台的能力也相应较窄。

**代码定位：** [任务接管](D:/project/魔法存储/Research/StorageComparison-2026-10-20/Decompiled/2792849912/ItemTeleporter.decompiled.cs:89)。

### 8. OgreStack

**代码事实。** 当前主 DLL 未发现 Harmony 补丁；启动/设置逻辑直接修改全局 Def 堆叠上限，同时处理类别、黑名单与显示/资源相关设置。允许增加堆叠的范围可包含部分原来 stackLimit 为 1 的自定义物品，最终行为取决于类别与排除规则。

**留言反馈。** 前 5 页中多次出现其他 Mod 分类导致错误上限、RIMMSqol/Character Editor 等设置互相覆盖，以及带独有数据物品合堆的报告。2025-08-12 有带蓝图信息物品状态丢失的报告；更早的基因包、胚芽等问题已有黑名单修复记录，不能全部算到当前版。[留言](https://steamcommunity.com/sharedfiles/filedetails/comments/1447140290)

**机制推断与比较。** 没有方法补丁，不代表只影响仓库：所有运输、生产、消费系统都会看到新上限。若原 Mod 把“永不堆叠”当作隐含保证，自定义数据可能没有合堆保护。我们不主动放大 Def 上限，避开主动引入这类风险；若用户同时装了 OgreStack，我们仍读取改变后的 stackLimit，且不能替其他 Mod 修复 CanStackWith。

**代码定位：** [堆叠修改与适用规则](D:/project/魔法存储/Research/StorageComparison-2026-10-20/Decompiled/1447140290/OgreStack.decompiled.cs:985)。

### 9. Stack XXL

**代码事实。** 最新发布主 DLL 同样没有发现 Harmony/HugsLib 依赖，直接改 Def 堆叠上限。适用条件与 OgreStack 不同：除材料、科技植入物等条件外，普通物品路径通常要求原本已经可堆叠，不应认为两者会把相同物品全部变成可堆叠。

**留言反馈。** 2026-10-13 作者说明当前 1.7 已移除外部依赖，与本次 DLL 相符；旧 HugsLib 启动报错不适用于这版。10 月 16 日有 CE 弹药堆叠设置不生效报告。较旧留言包含仿生部件按一堆计数、单个大食物堆被一人预留导致其他人等待等。[留言](https://steamcommunity.com/sharedfiles/filedetails/comments/826366050)

**机制推断与比较。** “一堆”与“这一堆的 stackCount”被某消费者混用时，放大堆叠会暴露错误；大堆的独占预留还会降低多人并发。CE 自身的堆叠修改和设置加载顺序也可能覆盖结果，但仅凭一条反馈不能确定原因。我们的网络按数量分配内部物品，普通输出仍是实体堆；外部消费者仍可能独占输出堆，不能认为内部网络预留解决了所有外部并发问题。

**代码定位：** [适用条件](D:/project/魔法存储/Research/StorageComparison-2026-10-20/Decompiled/826366050/StackXXL.decompiled.cs:235)、[更新上限](D:/project/魔法存储/Research/StorageComparison-2026-10-20/Decompiled/826366050/StackXXL.decompiled.cs:309)。

### 10. [KV] Change Dresser

**代码事实。** 最新版 Building_Dresser 继承 Building_Storage，StoredApparel 来自 slotGroup.HeldThings；不能继续把当前版当成旧的离图隐藏衣柜。非普通架子的部分在自动换装、征召切换、服装评分和回收等功能。WorldComp.StoreApparel 先尝试当前地图，再遍历全局衣柜所在地图，因此存在跨图选择目标的逻辑。

随包 MendingChangeDresserPatch 中可见检查与日志，但没有发现实际带 HarmonyPatch 的查料补丁类型。不能因日志写着 TryFindBestBillIngredients，就断言新版已实现完整的修补兼容。

**留言反馈。** 第 1 页作者 2025 年说明重写后使用实体储物并支持 Odyssey；2026-08-01 有双殖民地收衣消失的报告，与代码跨图目标选择值得一起验证，但不能直接认定因果。第 2 页有 Awesome Inventory 衣物需求交互；更老的服装位置/允许区域异常有修复说明。旧存储序列化报告不代表新版实体架仍有同样问题。[留言](https://steamcommunity.com/sharedfiles/filedetails/comments/2880268357)

**机制推断与比较。** 储存本身原生，并不能消除换装系统的耦合：多个 Mod 同时判断最佳服装、卸装和征召时机，仍可能争夺同一件衣物或覆盖穿衣决策。我们的 Mod 未接管通用换装策略，耦合较小；也不提供同等服装管理能力。

**代码定位：** [当前实体储物结构](D:/project/魔法存储/Research/StorageComparison-2026-10-20/Decompiled/2880268357/ChangeDresser.decompiled.cs:144)、[跨地图回收目标](D:/project/魔法存储/Research/StorageComparison-2026-10-20/Decompiled/2880268357/ChangeDresser.decompiled.cs:1130)。

## 为什么“有库存”还会报缺料

原版及第三方通常存在以下几层，接通一层不会自动接通下一层：

| 层次 | 实际问题 | 魔法存储现状 |
|---|---|---|
| 数量显示 | 资源栏是否显示库存 | ResourceCounter 后缀补入内部真实库存数量 |
| 产物计数 | “做到 X 件”是否统计已有合格产品 | 本 Mod 制造系统有自己的计数；原版简单路径可能读资源计数，带筛选路径则不同 |
| 寻找材料 | 工作台/炮塔能否找到真实 Thing | 普通地面扫描不会自动发现内部容器；可使用实体输出或专门接入 |
| 预留 | 谁获得哪一件、多少数量 | 内部网络有预留；原版与第三方有自己的预留，不能互相冒充 |
| 提取/消费 | 能否拿到有正确所有者和位置的物品 | 网络转移与专用供料可处理；外部直接 SplitOff/Destroy 需显式契约 |
| 失败恢复 | 中断、异常、放置失败后物品在哪里 | 通用转移有回退/恢复队列；第三方接入仍需逐项检查 |

RimSage2 的 RecipeWorkerCounter.CountProducts 表明：简单资源产品可能直接用 ResourceCounter；但启用品质、耐久、材质或指定储存组等条件后，会走真实物品枚举、SlotGroup 或 IHaulSource 路径。我们的 StorageUnit 当前没有实现 IHaulSource，且没有全局把内部库存注入 ListerThings。因此：**资源栏正确，与原版筛选配方计数正确，是两个不同结论。** 对不属于 CountAsResource 的产品，也不能指望资源统计后缀补齐它。

财富又是另一条路径。1.7 WealthWatcher 通过 ThingOwnerUtility 递归真实持有关系统计物品；我们的 IThingHolder 可以被这条原生路径看到。Pawn 扫描补丁是在特定查询上下文过滤自己的容器，不等于全局隐藏所有容器内容，也不能据此断言内部库存不计财富。

## 我们当前代码的优势与风险

### 已有的结构性优势

1. **保留真实对象。** [Building_StorageUnit](D:/project/魔法存储/Source/MagicStorage/Buildings/Building_StorageUnit.cs:7) 实现 IThingHolder、IThingHolderTickable、ISuspendableThingHolder；不 tick 内部内容，明确表达暂停语义。[StorageInventory](D:/project/魔法存储/Source/MagicStorage/Storage/StorageInventory.cs:8) 维护真实容器和派生数量索引，合并遵守 CanStackWith 与当前堆叠上限。
2. **物品移动有恢复路径。** [StorageTransfer](D:/project/魔法存储/Source/MagicStorage/Storage/StorageTransfer.cs:20) 使用 Take/TryAdd 移动物品，失败时尝试回源或进入恢复处理，而不是用“增加数字、销毁原物品”代替移动。
3. **普通搬运入口较窄。** [输入口](D:/project/魔法存储/Source/MagicStorage/Buildings/Building_StorageReceiver.cs:9) 是真实 SlotGroup；Notify_ReceivedThing 不立即吞物品，等待放置完成后处理。不对全局 StartJob、Reachability 或 ReservationManager 做同类绕过。
4. **多数整合有范围。** 交易、运输、远行和孕育器补丁围绕自己的会话/建筑；建造材料选择器转译只替换已校验的单一调用，匹配失败保留原方法并记错，见 [StorageBuildMaterialPatches](D:/project/魔法存储/Source/MagicStorage/Storage/StorageBuildMaterialPatches.cs:20)。

### 需要重视的具体边界

| 优先级 | 代码事实 / 风险 | 影响与建议 |
|---|---|---|
| 高 | MonolynCompatibility 按成本逐项即时销毁；后续成本不足直接返回 false，没有回滚前面成本 | 可能发生建造失败但已经扣掉部分材料；先核对全部成本并预留，再统一提交更稳妥 |
| 高 | 同一兼容层直接从 Contents.Take，不走网络可用量/出库预留 | 可能消费已被其他内部任务声明的物品。RefreshStack 会通知并校验现有声明，降低悬空索引风险，但仍可能令进行中的任务失去材料 |
| 中 | 原版带筛选产物计数不一定枚举内部库存 | 自动生产可能继续生产已经足量的产品；需要限定目标与去重规则，再考虑 IHaulSource 或计数接入 |
| 中 | 输入口 ProcessIncoming 检查普通 reservationManager；输出货架还检查 physicalInteractionReservationManager | 输入口缺少后一套检查，存在吸走第三方仅做物理交互预留物品的路径；属于需隔离验证的竞争条件 |
| 中 | ConstructionSite 调用 Blueprint.MakeSolidThing 并自行完成部分替换/生成步骤 | 外部 Mod 若在原版 TryReplaceWithSolidThing 中加额外规则，专用施工路径可能绕开；低补丁数量不代表完整复用了原版行为 |
| 中 | 外部兼容通过类型名/方法名和反射定位 | 方法改签名或移位后兼容功能可能失效；应检查现有日志与降级行为，而非把外部版本变化都归因于库存本体 |
| 设计边界 | 暂停容器内容更新 | 有益于冷藏/性能；依赖 Comp 每 tick 更新的充能、衰变等 Mod 物品也会被暂停，需要明确支持政策 |
| 设计边界 | 地面物品消费者不自动看到网络内容 | 先用实体输出作为通用桥梁；真正需要直接消耗的系统再接入网络的预留、提取和失败恢复流程 |

Monolyn 的关键证据见 [ConsumeResources](D:/project/魔法存储/Source/MagicStorage/Compatibility/MonolynCompatibility.cs:36) 与 [直接取出并销毁](D:/project/魔法存储/Source/MagicStorage/Compatibility/MonolynCompatibility.cs:82)。其中不要求网络通电是代码明确采用的物理库存口径，本报告不把这一点单独当作 bug。真正的问题是跨材料失败与现有预留之间的约束。网络收到库存变化后会调用 [ValidateItem](D:/project/魔法存储/Source/MagicStorage/Networks/StorageNetwork.cs:189)，所以不能进一步声称它必然产生重复物品。

输入与输出的对照见 [输入吸收检查](D:/project/魔法存储/Source/MagicStorage/Buildings/Building_StorageReceiver.cs:74) 和 [输出预留检查](D:/project/魔法存储/Source/MagicStorage/Storage/StorageShelfSupply.cs:9)。施工路径见 [ConstructionSite](D:/project/魔法存储/Source/MagicStorage/Construction/ConstructionSite.cs:58)。

## 建议的兼容验证顺序

以下是下一步验证方案，本次没有实施代码修改或游戏测试。

| 场景 | 组合 / 触发方式 | 应验证的不变量 |
|---|---|---|
| 输入与搬运 | 分别开启 PUAH、Common Sense、While You're Up 的背包/顺路搬运选项，多 Pawn 向输入口交货 | 放置前后物品总量守恒；不吸走有效预留物品；任务不循环 |
| 输出被占用 | 输出堆正被原版或第三方操作，网络同时补货/回收 | 不破坏目标引用、预留和现有携带数量 |
| 多材料即时建造 | Monolyn 成本 A 足、B 不足；另一个任务已预留 A | 整体失败时零净扣料；不抢占已有有效声明 |
| 原版产物计数 | 普通资源产品、非资源产品、指定品质/耐久/材质、指定储物组，分别只有内部库存与只有输出库存 | “做到 X 件”的计数与约定一致且不重复计算 |
| 自动消费 | CE 补弹、自动燃料、PRF 机器等分别读取内部与实体输出 | 数量可见性与真实取用分开验证；失败不产生免费物品或重复扣料 |
| 特殊物品 | 基因包、充能物品、蓝图数据、最小化物品、带自定义 Comp 物品 | 多次入库/出库后身份与状态保留，合并遵守物品自身规则 |
| 施工 | 地板/基础、墙面附着物、已有地形、阻挡物以及第三方蓝图 | 与目标建造规则一致；失败不丢料、不错误清除物体 |
| 生命周期 | 建筑拆除、满地图无法放置、飞船搬迁、交易取消、任务中断 | 每件物品始终只有一个所有者；恢复队列可保存且无重复 |

如果只先做一项改进，应优先统一外部兼容的扣料协议：先完整检查与声明，再提取，成功后销毁/交付，失败恢复。它比继续增加“让某个界面看到库存”的补丁更能控制材料损失与重复扣料风险。若之后扩展 1.7 IHaulSource，应先确认计数去重、提取权限和预留语义，避免同时从资源后缀、来源枚举和实体输出重复曝光同一物品。

## 阅读结果的使用限制

本报告证明的是：调查时发布 DLL 的相关路径确实存在，以及留言中有人报告过相应现象。它不证明每一条反馈仍适用于当前版，也不证明某个未隔离的大整合包问题必由该 Mod 单独造成。性能部分仅比较索引、扫描、物品生成和全局挂接的结构，没有给出未经实测的 TPS/内存排名。

当前工作区已有开发内容，本报告比较的就是阅读时的工作区实现；未将其替换成某个发布版或 Git 提交。研究文件与反编译结果保存在本目录，原 Mod 代码没有因这次调查改变。

## 调查后的修复状态

根据用户后续要求，本地代码已经完成以下修改：

- 穿衣适配器在重力飞船搬迁期间保留暂存衣物，落地后再继续穿戴或退回，避免进入出发地图的恢复队列。
- 墨诺灵在全部材料确认并提取之前不销毁物品，避开已有网络和地面预留；不足或提取中断时恢复尚未消费的材料。
- 左上角资源统计与墨诺灵内部库存供料均要求有效网络。无核心、多核心或断开的单元仍保存实际物品，但不贡献这部分资源统计。
- 墨诺灵、燃料供给、淋璃莲炮台共用 [StorageMaterialTransaction](D:/project/魔法存储/Source/MagicStorage/Storage/StorageMaterialTransaction.cs:10)，统一预留、暂存、释放与恢复。新类仅参与运行时协调，暂存仍使用原有 StorageRecoveryBatch，未改变存档字段或保存格式。
- 消费、交付分别确认结果。燃料通知在增加燃料后抛异常时，对应材料仍会扣除；炮台实际接收后返回失败或抛异常时，不退款或重复补送。部分接收则按实际数量调整订单与剩余需求。

这次公共改造通过 1.6/1.7 编译及 11032 项测试断言。游戏边界使用测试替身，尚未进行真实游戏起降、第三方 Mod 运行或旧存档加载验证。已发生的第三方副作用不能由通用逻辑任意逆转；恢复仅针对仍未消费、未交付的真实物品。
