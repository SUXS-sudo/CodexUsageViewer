# 上传与发布

## 提交到 GitHub 仓库

建议仓库名：`CodexUsageViewer`。

建议 Description：

> Windows 桌面 Codex 额度悬浮窗与任务 Token 统计工具，支持用量趋势、最近任务排行和 CSV 导出。

建议 Topics：`codex`、`token-usage`、`windows`、`wpf`、`csharp`、`usage-tracker`。

上传本目录下的源码、文档与配置文件。README.md、LICENSE、build.ps1 应直接位于仓库根目录，不要把外层整个目录当成一个额外的子目录。

推荐通过 Git 提交，确保 `.gitignore`、`.gitattributes` 和 `.editorconfig` 也一并上传。不要上传生成的 `build/`、编译器、日志、缓存、备份、真实会话记录或导出的 CSV。README 的截图已经使用模拟数据，可直接公开。

如果使用 GitHub 网页上传，检查上述三个点开头的配置文件是否一并提交。

## 发布可执行文件

源码仓库不需要提交 exe。为需要直接运行的用户，在 Releases 中单独发布 Windows 压缩包。

1. 在仓库根目录运行 `build.ps1`、`test.ps1`。
2. 创建版本为 `v2.1.1` 的 Release。
3. 将独立的 `CodexUsageViewer-v2.1.1-win-x64.zip` 作为附件上传。
4. 发布说明写明需要 Windows x64、.NET Framework 4.8 和已登录的本机 Codex。

源码目录之外准备的 Windows 压缩包只包含程序、图标、MIT 许可证和运行说明，不包含任何本机额度缓存或任务数据。

## 可直接使用的发布介绍

Codex Usage Viewer 是一个 Windows 桌面工具，将 Codex 剩余额度悬浮窗和任务 Token 统计放在一起。支持按最近活动查看任务、每日趋势、输入输出与缓存明细、日期筛选、子代理记录和 CSV 导出。采用柔和绿色与暖白界面，额度由本机官方 Codex App Server 查询，任务用量从本机记录读取。

本版本使用 MIT 许可证，附源码和构建说明。Token 统计仅覆盖本机留存记录，不等同于套餐额度或实际账单。
