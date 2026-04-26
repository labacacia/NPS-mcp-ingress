[English Version](./CHANGELOG.md) | 中文版

# 变更日志 —— MCP Ingress (`LabAcacia.McpIngress`)

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

在 NPS 达到 v1.0 稳定版之前，套件内所有仓库同步使用同一个预发布版本号。

---

## [1.0.0-alpha.3] —— 2026-04-26

### 重命名（破坏性）

- 包名 `LabAcacia.McpBridge` → `LabAcacia.McpIngress`，详见 [NPS-CR-0001](https://github.com/labacacia/NPS-Dev/blob/dev/spec/cr/NPS-CR-0001-anchor-bridge-split.md)。新的规范层 **Bridge Node** 类型（NWP §2A）承担 *NPS → 外部* 方向；本包承担**相反**方向（外部 → NPS），故改名 `*Ingress`。线上格式与 alpha.2 完全一致，只是 assembly 名 + 命名空间变了。消费方需更新 `<PackageReference Include="LabAcacia.McpBridge"/>` → `LabAcacia.McpIngress` 及 `using LabAcacia.McpBridge;` 导入。
- 对应 GitHub 仓库 `labacacia/NPS-mcp-bridge` 已重命名为 `labacacia/NPS-mcp-ingress`。GitHub 自动重定向旧 URL；已 clone 的本地仓库用 `git remote set-url origin https://github.com/labacacia/NPS-mcp-ingress.git` 更新即可。
- 测试通过数与 alpha.2 一致（除重命名外无功能变更）。

### 同步

- 版本由 1.0.0-alpha.2 升至 1.0.0-alpha.3，与 NPS 套件其余仓库保持一致。

---

## [1.0.0-alpha.2] —— 2026-04-19

### Changed

- 版本升级至 `1.0.0-alpha.2`，与套件同步。自 `1.0.0-alpha.1` 以来无功能变更。
- 15 测试 全绿。

### 简介

- 将 NWP Memory / Action / Complex Node 暴露为 MCP 2024-11-05 服务端（JSON-RPC 2.0 over HTTP）。

---

## [1.0.0-alpha.1] —— 2026-04-10

在 NPS 套件 `v1.0.0-alpha.1` 标签下首次发布。

[1.0.0-alpha.2]: https://github.com/LabAcacia/nps/releases/tag/v1.0.0-alpha.2
[1.0.0-alpha.1]: https://github.com/LabAcacia/nps/releases/tag/v1.0.0-alpha.1
