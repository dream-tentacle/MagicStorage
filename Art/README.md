# Art 最终稿

每张图只保留最终稿。工坊宣传图同时保留可编辑 SVG 和可上传 PNG，放在同一文件夹；其他图片有可编辑 SVG 的保留 SVG，没有 SVG 的保留最终 PNG。

- `Buildings/`：13 种建筑及存储线；旋转建筑保留各个方向。培育器 SVG 包含完整分层。
- `UI/`：最终界面图标。
- `Workshop/`：中英文单张介绍图、分组长图和玩家反馈宣传图。
- `Cover.png`：最终封面。

游戏实际使用的 PNG 在项目根目录 `Textures/`，Art 不再重复存放。
工坊生成脚本、宣传文案与英文配置保留在 `Workshop/`，避免清理后丢失生成方式。
`Workshop/make_building_cards.py` 恢复自 Git 历史；运行 `python Art/Workshop/update_production_output.py` 可按原字体、透明背景和版式更新生产输出口及第三张分组图。PNG 与对应 SVG 同目录保存，同时导出到 `../Exports/Workshop/1.2.0/`。

其他依赖底稿、预览、报告及旧稿留在 `../Backups/` 的 Art 备份中。
