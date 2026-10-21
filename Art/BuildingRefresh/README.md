# 建筑贴图优化样稿

本批最初包含核心和工作台以外十种建筑的单视角审核稿，后续补齐工作台、核心基准及旋转方向。SVG 为可编辑源文件，PNG 为透明背景导出。现在已通过 `GameExport/` 将全部十二种建筑、44 张游戏 PNG 接入 `Textures` 和现有 XML。存储线未处理。

后续已为五种可旋转建筑补齐四方向，详见下方「方向图」。

统一方向：浅灰蓝外壳、`#C5E5F5` 装饰色、纯黑且等宽的外围描边；内部以较深的结构面区别材质，减少细碎高光、重复屏幕和层叠面板边框。各建筑使用不同的功能部件。

外围描边统一减为上一版的 **2/3**：原基准为 `12×2/404` 格，现在为约 `0.039604` 格，游戏导出时约 `10.1386` 像素（256 像素/格）。包括核心、五种建筑的全部方向及培育器分层。核心原稿 v27 保持不变，当前版本另存为 `StorageCore-refresh-v1.svg`。`outline-manifest.json` 记录换算结果；`BuildingRefresh-outline-review.png` 按统一的横向格子比例排列十二种建筑。

描边直接在 **SVG** 中生成：双倍宽度路径配合同轮廓遮罩，去掉内部半边，黑边只向外延伸。游戏 SVG 使用不随缩放变化的线宽；PIL 只用于排版检查图和培育器发光遮罩，不生成黑边。普通机壳使用直线接角，存储单元、衣物适配器、货架、传送器和燃料侧泵收小切角；核心圆角方形、储罐和传送器圆环保留。`casing_geometry.py` 让相邻色面共用端点。

| 建筑 | 本次调整 |
| --- | --- |
| 存储单元 | 保留双匣盖、原底盘范围；加强匣盖间隙和拉手，简化指示条 |
| 存储接收器 | 深色后壁、完整承接面、连续槽壁与前唇 |
| 自动补货架 | 保留两层开放货架和固定方向；加强层间暗部 |
| 物资补给器 | 低矮后部机壳与出料前唇；保留深蓝色箭头 |
| 建筑供给单元 | 保留凸起舱口、内凹舱门、一条接缝与下沿天蓝饰条；去掉两侧长条、上方标记和下方小面板 |
| 衣物适配器 | 保留衣物舱与悬挂衣物；右侧改为单个检修舱 |
| 燃料供给器 | 圆柱储罐和侧泵形成主体轮廓，减少通用底板 |
| 涅瓦莲质量驱动器提取口 | 厚实八角底座、深井和两侧锁扣 |
| 远行队传送器 | 保留原螺旋路径与裁剪；简化机壳和金属环 |
| 宇宙培育器 | 保留 3×2 平台、居中底座、罐体与顶盖坐标；仅整理材质和细节 |

## 检查图

- `BuildingRefresh-overview.png`：十种样稿总览。
- `BuildingRefresh-comparison-1.png` / `2.png`：左为原稿、右为优化稿，角落附小尺寸预览。检查图按透明内容范围排版，不用于游戏坐标或偏移计算。
- `manifest.json`：原稿、样稿、占地和调整说明的对应表。
- `validation.json`：PNG 尺寸及透明内容范围。

## 方向图

`Directions/` 包含存储单元、宇宙工作台、衣物适配器、物资补给器、宇宙培育器的南、北、东、西方向，共 20 张 SVG 与 20 张透明 PNG。其中 5 张是已确认视角的原样副本，新增其余 15 张；固定朝向建筑和存储线不增加方向。游戏使用下节校准后的导出，审核图内的「未并入游戏」文字仅表示当时生成状态。

- [全部方向总览](Directions/BuildingRefresh-all-directions.png)
- [存储单元](Directions/StorageUnit-directions-review.png)
- [宇宙工作台](Directions/CosmicWorkbench-directions-review.png)
- [衣物适配器](Directions/StorageApparelAdapter-directions-review.png)
- [物资补给器](Directions/TransportSupplier-directions-review.png)
- [宇宙培育器](Directions/CosmicMechGestator-directions-review.png)

工作台仅对工作面布局变换方向，重新生成竖直侧面和全局倒角明暗；各色面使用同一组顶点。衣物保持正立。培育器采用已确认的各向平台尺寸和罐体中心，以最新版玻璃、罐底与顶盖替换旧材质；罐体不随底盘转倒。物资补给器保持深蓝色箭头并指向输出方向。所有方向的黑色轮廓仅向外延伸，按核心基准换算粗细，留足透明边距。

重新生成方向图：依次运行 `build_directions.py`、`render_directions.cjs`、`preview_directions.py`。`verify_outside_outline.cjs Directions` 检查实际导出的 20 张 PNG，确保黑边未覆盖内部不透明像素；`Directions/validation.json` 记录尺寸和透明边界。完整性检查同时确认原先五个视角的 SVG/PNG 字节不变。

## 重新导出

依次运行 `build_drafts.py`、`render_drafts.cjs`、`preview_drafts.py`、`preview_outlines.py`。审核稿生成器只写入本目录，构建后自动进行描边换算，也可单独运行 `normalize_outlines.py`。

## 游戏接入

依次运行 `prepare_game_exports.py`、`render_game_exports.cjs`、`install_game_exports.py`、`preview_dense_layout.py`，最后按项目要求申请沙箱外权限执行根目录 `deploy.ps1`。前四步在工作区内完成校验与安装，部署之后不再执行额外核对。

- 普通建筑：23 张 PNG，按 256 像素/格、画布外部 16 像素透明边距校准。黑边外沿每侧额外向占地内留 **12 像素（0.046875 格）**，密排间隙为 **24 像素（0.09375 格）**；不是只增加透明画布。中心偏移为零，XML 绘制尺寸保持不变。
- 培育器：21 张 PNG（四方向的底座、玻璃、顶盖、两种发光遮罩，加菜单图标）。按用户后续要求，主体从原稿的 80% 调回 **90%**，允许更饱满地占用格子；平台台面范围对齐占地，前侧立面和高处顶盖保留自然透视突出。平台中心、各层比例、黑边粗细不变。进度条、成形机械体及阴影同步调整，绘制画布大小不变。四方向分层重组均经过校验。
- 五种旋转建筑显式提供西向图，XML 关闭缺图镜像回退。固定建筑维持单图，未改 C# 或玩法参数。
- `GameExport/GameIntegration-review.png` 为游戏导出比例总览；`GameExport/DenseLayout-review.png` 用真实游戏比例重复排列全部 27 个视角，蓝框为占地。`dense-layout-validation.json` 记录实际间隙及普通建筑无像素相交的检查，`integration-report.json` 记录目标路径、尺寸、锚点和分层误差。
