# Changelog

## 1.1.0

- 支持将包目录作为独立 Git 仓库根目录，通过 Package Manager 的 Git URL 安装。
- 通用项目隐藏整个框架配置区域；项目适配器仍可提供脚本与 UI 层级配置。
- 显式引用 Unity 2022.3 的 UGUI 程序集，并声明图片编解码、JSON 序列化模块依赖。
- 添加不依赖业务资源或 AI 的 Basic UGUI Schema 示例。
- 保留高精度生成、并行资源评分、九宫格像素缓存、AI 等待期间预计算和分阶段计时。

## 1.0.1

- 分离可移植 Editor 核心与 Lxy 项目适配器。
- 支持本地效果图、Figma 节点及 UISchema 生成 UGUI Prefab。
