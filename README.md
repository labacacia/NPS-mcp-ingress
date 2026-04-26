English | [中文版](./README.cn.md)

# LabAcacia.McpIngress

[![NuGet](https://img.shields.io/nuget/v/LabAcacia.McpIngress.svg)](https://www.nuget.org/packages/LabAcacia.McpIngress)

An **ASP.NET Core library** that turns one or more **NPS NWP nodes** into a single
[Model Context Protocol](https://modelcontextprotocol.io) (MCP) server. MCP-speaking
clients — Claude Desktop, IDE agents, the Anthropic SDK — can read from NWP Memory
Nodes and invoke NWP Action Nodes without knowing anything about NPS.

- **Protocol**: JSON-RPC 2.0 over HTTP POST (MCP `2024-11-05`).
- **Target**: .NET 10, ASP.NET Core.
- **NWP spec**: `spec/NPS-2-NWP.md` v0.5 (Memory Node `§5`, Action Node `§7`).

---

## What it does

| MCP method       | NWP call                                | Notes                                                                   |
| ---------------- | --------------------------------------- | ----------------------------------------------------------------------- |
| `initialize`     | —                                       | Returns server info + capabilities (`resources`, `tools`).              |
| `resources/list` | `GET /.nwm` on every configured upstream | Emits one resource per upstream where `node_type == "memory"`.         |
| `resources/read` | `POST /query` with `{ "limit": N }`     | Wraps the CapsFrame body as one MCP text content (MIME `application/json`). |
| `tools/list`     | `GET /.nwm` + `GET /actions`             | Emits one tool per action on upstreams with `node_type in ("action", "complex")`. |
| `tools/call`     | `POST /invoke`                           | Forwards `arguments` as the action `params`; `isError=true` on non-2xx. |
| `ping`           | —                                        | Keep-alive.                                                             |

Addressing conventions:

- Resource URIs: `nwp://<upstream-name>/`
- Tool names:    `<upstream-name>__<action-id-with-dots-replaced-by-underscores>`
  - Dots are forbidden in MCP tool names, so `orders.create` on upstream `orders`
    becomes `orders__orders_create`.

---

## Install

```bash
dotnet add package LabAcacia.McpIngress
```

---

## Quick start

```csharp
using LabAcacia.McpIngress;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpIngress(o =>
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
app.MapMcpIngress("/mcp");   // POST /mcp — JSON-RPC 2.0
app.Run();
```

Point any MCP client at `https://<host>/mcp`. No agent code needs to be changed.

---

## Configuration

`McpIngressOptions`:

| Property            | Default              | Purpose                                                            |
| ------------------- | -------------------- | ------------------------------------------------------------------ |
| `ServerName`        | `labacacia-mcp-bridge` | `serverInfo.name` returned by `initialize`.                      |
| `ServerVersion`     | `0.1.0`              | `serverInfo.version` returned by `initialize`.                     |
| `Upstreams`         | *(required, ≥1)*     | List of NWP nodes to expose.                                       |
| `ResourceReadLimit` | `100`                | `limit` passed to `/query` when an MCP client calls `resources/read`. |

`NwpUpstream`:

| Property     | Purpose                                                                 |
| ------------ | ----------------------------------------------------------------------- |
| `Name`       | Host segment in generated URIs / tool names. Must be unique.            |
| `BaseUrl`    | Root URL where `/.nwm`, `/actions`, `/query`, `/invoke` are mounted.   |
| `AgentNid`   | Sent as `X-NWP-Agent` on every call (optional).                         |
| `AuthHeader` | Sent verbatim as the `Authorization` header on every call (optional).   |

---

## Error mapping

Errors are returned as JSON-RPC 2.0 errors. In addition to the standard codes
(`-32700` parse, `-32600` invalid request, `-32601` method not found, `-32602`
invalid params, `-32603` internal), the ingress defines:

| Code      | Meaning                                             |
| --------- | --------------------------------------------------- |
| `-32000`  | Upstream NWP node returned a non-success status.    |
| `-32001`  | Resource URI did not resolve to a configured upstream. |
| `-32002`  | Tool name did not resolve to a configured upstream. |

JSON-RPC *notifications* (requests without `id`) receive HTTP `204 No Content`
per the spec.

---

## Testing

```bash
dotnet test compat/mcp-bridge/tests/LabAcacia.McpIngress.Tests/LabAcacia.McpIngress.Tests.csproj
```

The test suite exercises every MCP handler against a fake NWP backend
(`HttpMessageHandler` stubs) — no network required.

---

## License

Apache 2.0. See `LICENSE` and `NOTICE` at the repo root.
