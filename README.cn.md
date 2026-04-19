[English Version](./README.md) | 中文版

# LabAcacia.McpBridge

[![NuGet](https://img.shields.io/nuget/v/LabAcacia.McpBridge.svg)](https://www.nuget.org/packages/LabAcacia.McpBridge)

一个 **ASP.NET Core 库**，把一个或多个 **NPS NWP 节点** 包装成单一的
[Model Context Protocol](https://modelcontextprotocol.io)（MCP）服务器。任何
支持 MCP 的客户端 —— Claude Desktop、IDE Agent、Anthropic SDK —— 都能直接
读写 NWP Memory Node、调用 NWP Action Node，完全不需要了解 NPS。

- **协议**：JSON-RPC 2.0 over HTTP POST（MCP `2024-11-05`）
- **目标框架**：.NET 10 + ASP.NET Core
- **NWP 规范**：`spec/NPS-2-NWP.md` v0.5（Memory Node `§5`、Action Node `§7`）

---

## 功能矩阵

| MCP 方法         | 对应 NWP 调用                          | 说明                                                                   |
| ---------------- | -------------------------------------- | ---------------------------------------------------------------------- |
| `initialize`     | —                                      | 返回 server info + capabilities（`resources`、`tools`）                |
| `resources/list` | 对所有 upstream `GET /.nwm`            | 仅为 `node_type == "memory"` 的 upstream 暴露资源                      |
| `resources/read` | `POST /query`，`{ "limit": N }`        | 把 CapsFrame 响应体作为一条 MCP 文本内容返回（MIME `application/json`）|
| `tools/list`     | `GET /.nwm` + `GET /actions`           | 把 `action` / `complex` 节点的每个 action 注册为一个工具              |
| `tools/call`     | `POST /invoke`                         | 把 `arguments` 原样作为 action `params`；非 2xx 返回 `isError=true`    |
| `ping`           | —                                      | 心跳                                                                   |

**命名约定：**

- 资源 URI：`nwp://<upstream-name>/`
- 工具名：`<upstream-name>__<把 . 替换为 _ 的 action id>`
  - MCP 工具名禁止出现 `.`，所以 upstream `orders` 上的 `orders.create`
    会被暴露为 `orders__orders_create`。

---

## 安装

```bash
dotnet add package LabAcacia.McpBridge
```

---

## 快速开始

```csharp
using LabAcacia.McpBridge;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpBridge(o =>
{
    o.ServerName    = "my-agent-gateway";
    o.ServerVersion = "1.0.0";
    o.Upstreams     = new[]
    {
        new NwpUpstream
        {
            Name       = "products",
            BaseUrl    = new Uri("https://memory.example.com/products"),
            AgentNid   = "urn:nps:nid:agent:my-gateway",
            AuthHeader = "Bearer <service-token>",
        },
        new NwpUpstream
        {
            Name    = "orders",
            BaseUrl = new Uri("https://action.example.com/orders"),
        },
    };
});

var app = builder.Build();
app.MapMcpBridge("/mcp");   // POST /mcp — JSON-RPC 2.0
app.Run();
```

把任意 MCP 客户端指向 `https://<host>/mcp` 即可，Agent 代码无需变更。

---

## 配置

`McpBridgeOptions`：

| 字段                | 默认值                 | 作用                                                               |
| ------------------- | ---------------------- | ------------------------------------------------------------------ |
| `ServerName`        | `labacacia-mcp-bridge` | `initialize` 返回的 `serverInfo.name`                              |
| `ServerVersion`     | `0.1.0`                | `initialize` 返回的 `serverInfo.version`                           |
| `Upstreams`         | *（必填，≥1）*         | 要暴露的 NWP 节点列表                                              |
| `ResourceReadLimit` | `100`                  | MCP 客户端调用 `resources/read` 时传给 `/query` 的 `limit` 值      |

`NwpUpstream`：

| 字段         | 作用                                                                 |
| ------------ | -------------------------------------------------------------------- |
| `Name`       | 生成 URI / 工具名里的 host 段，必须全局唯一                          |
| `BaseUrl`    | NWP 节点根 URL，下面挂 `/.nwm`、`/actions`、`/query`、`/invoke`       |
| `AgentNid`   | 每次调用都附带为 `X-NWP-Agent` 头（可选）                            |
| `AuthHeader` | 每次调用都原样填到 `Authorization` 头（可选）                        |

---

## 错误码映射

错误以 JSON-RPC 2.0 error 形式返回。除了标准错误码（`-32700` 解析、`-32600`
无效请求、`-32601` 方法不存在、`-32602` 参数无效、`-32603` 内部错误）外，
本桥定义：

| 错误码    | 含义                                             |
| --------- | ------------------------------------------------ |
| `-32000`  | 上游 NWP 节点返回了非 2xx 状态                   |
| `-32001`  | Resource URI 对不上任何已配置的 upstream         |
| `-32002`  | 工具名对不上任何已配置的 upstream                |

JSON-RPC *通知*（没有 `id` 的请求）按规范返回 HTTP `204 No Content`。

---

## 测试

```bash
dotnet test compat/mcp-bridge/tests/LabAcacia.McpBridge.Tests/LabAcacia.McpBridge.Tests.csproj
```

测试用 `HttpMessageHandler` 桩假冒 NWP 后端，跑通所有 MCP 处理函数 —— 不依
赖网络。

---

## 许可证

Apache 2.0。根目录 `LICENSE` 与 `NOTICE`。
