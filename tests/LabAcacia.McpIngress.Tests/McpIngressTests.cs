// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using System.Text.Json;
using LabAcacia.McpIngress;
using Xunit;

namespace LabAcacia.McpIngress.Tests;

/// <summary>
/// Unit tests for <see cref="global::LabAcacia.McpIngress.McpIngress"/>. The upstream NWP
/// node is replaced with a <see cref="StubHandler"/> so we can run without any real
/// HTTP server or network I/O.
/// </summary>
public sealed class McpIngressTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── initialize ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Initialize_ReturnsProtocolAndServerInfo()
    {
        var (ingress, _) = BuildIngressWithMemoryNode();

        var resp = await ingress.DispatchAsync(new JsonRpcRequest
        {
            Method = "initialize",
            Id     = JsonDocument.Parse("1").RootElement,
        });

        Assert.Null(resp.Error);
        Assert.NotNull(resp.Result);
        var protoVer = resp.Result!.Value.GetProperty("protocolVersion").GetString();
        Assert.Equal(McpProtocol.Version, protoVer);
        Assert.Equal("LabAcacia.McpIngress.Tests",
            resp.Result!.Value.GetProperty("serverInfo").GetProperty("name").GetString());
    }

    // ── resources/list ───────────────────────────────────────────────────────

    [Fact]
    public async Task ResourcesList_ReturnsMemoryNodeEntries()
    {
        var (ingress, _) = BuildIngressWithMemoryNode();

        var resp = await ingress.DispatchAsync(new JsonRpcRequest
        {
            Method = "resources/list",
            Id     = JsonDocument.Parse("1").RootElement,
        });

        Assert.Null(resp.Error);
        var resources = resp.Result!.Value.GetProperty("resources");
        Assert.Equal(1, resources.GetArrayLength());
        Assert.Equal("nwp://products/", resources[0].GetProperty("uri").GetString());
    }

    [Fact]
    public async Task ResourcesList_IgnoresActionNodes()
    {
        var (ingress, _) = BuildIngressWithActionNode();

        var resp = await ingress.DispatchAsync(new JsonRpcRequest
        {
            Method = "resources/list",
            Id     = JsonDocument.Parse("1").RootElement,
        });

        Assert.Null(resp.Error);
        var resources = resp.Result!.Value.GetProperty("resources");
        Assert.Equal(0, resources.GetArrayLength());
    }

    // ── resources/read ───────────────────────────────────────────────────────

    [Fact]
    public async Task ResourcesRead_CallsUpstreamQuery_AndReturnsTextContent()
    {
        var (ingress, handler) = BuildIngressWithMemoryNode();

        var resp = await ingress.DispatchAsync(new JsonRpcRequest
        {
            Method = "resources/read",
            Id     = JsonDocument.Parse("1").RootElement,
            Params = JsonSerializer.SerializeToElement(new { uri = "nwp://products/" }, Json),
        });

        Assert.Null(resp.Error);
        var contents = resp.Result!.Value.GetProperty("contents");
        Assert.Equal(1, contents.GetArrayLength());
        Assert.Contains("\"count\"", contents[0].GetProperty("text").GetString());

        // Upstream must have been called on /query
        Assert.Contains(handler.Requests, r => r.RequestUri!.AbsolutePath.EndsWith("/query"));
    }

    [Fact]
    public async Task ResourcesRead_UnknownUpstream_ReturnsResourceNotFound()
    {
        var (ingress, _) = BuildIngressWithMemoryNode();

        var resp = await ingress.DispatchAsync(new JsonRpcRequest
        {
            Method = "resources/read",
            Id     = JsonDocument.Parse("1").RootElement,
            Params = JsonSerializer.SerializeToElement(new { uri = "nwp://unknown/" }, Json),
        });

        Assert.NotNull(resp.Error);
        Assert.Equal(JsonRpcErrorCodes.ResourceNotFound, resp.Error!.Code);
    }

    [Fact]
    public async Task ResourcesRead_BadUri_ReturnsInvalidParams()
    {
        var (ingress, _) = BuildIngressWithMemoryNode();

        var resp = await ingress.DispatchAsync(new JsonRpcRequest
        {
            Method = "resources/read",
            Id     = JsonDocument.Parse("1").RootElement,
            Params = JsonSerializer.SerializeToElement(new { uri = "http://example.com" }, Json),
        });

        Assert.NotNull(resp.Error);
        Assert.Equal(JsonRpcErrorCodes.InvalidParams, resp.Error!.Code);
    }

    // ── tools/list ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ToolsList_ReturnsActionNodeActions()
    {
        var (ingress, _) = BuildIngressWithActionNode();

        var resp = await ingress.DispatchAsync(new JsonRpcRequest
        {
            Method = "tools/list",
            Id     = JsonDocument.Parse("1").RootElement,
        });

        Assert.Null(resp.Error);
        var tools = resp.Result!.Value.GetProperty("tools");
        Assert.Equal(2, tools.GetArrayLength());

        var toolNames = new[] { tools[0].GetProperty("name").GetString(), tools[1].GetProperty("name").GetString() };
        Assert.Contains("orders__orders_create", toolNames);
        Assert.Contains("orders__orders_cancel", toolNames);
    }

    [Fact]
    public async Task ToolsList_IgnoresMemoryNodes()
    {
        var (ingress, _) = BuildIngressWithMemoryNode();

        var resp = await ingress.DispatchAsync(new JsonRpcRequest
        {
            Method = "tools/list",
            Id     = JsonDocument.Parse("1").RootElement,
        });

        Assert.Null(resp.Error);
        Assert.Equal(0, resp.Result!.Value.GetProperty("tools").GetArrayLength());
    }

    // ── tools/call ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ToolsCall_PostsToInvoke_AndReturnsText()
    {
        var (ingress, handler) = BuildIngressWithActionNode();

        var resp = await ingress.DispatchAsync(new JsonRpcRequest
        {
            Method = "tools/call",
            Id     = JsonDocument.Parse("1").RootElement,
            Params = JsonSerializer.SerializeToElement(new
            {
                name      = "orders__orders_create",
                arguments = new { sku = "ABC-123" },
            }, Json),
        });

        Assert.Null(resp.Error);
        Assert.False(resp.Result!.Value.GetProperty("isError").GetBoolean());

        var (_, body) = handler.RequestBodies.Single(b => b.Path.EndsWith("/invoke"));
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("orders.create", doc.RootElement.GetProperty("action_id").GetString());
    }

    [Fact]
    public async Task ToolsCall_MalformedName_ReturnsToolNotFound()
    {
        var (ingress, _) = BuildIngressWithActionNode();

        var resp = await ingress.DispatchAsync(new JsonRpcRequest
        {
            Method = "tools/call",
            Id     = JsonDocument.Parse("1").RootElement,
            Params = JsonSerializer.SerializeToElement(new { name = "missingseparator" }, Json),
        });

        Assert.NotNull(resp.Error);
        Assert.Equal(JsonRpcErrorCodes.ToolNotFound, resp.Error!.Code);
    }

    [Fact]
    public async Task ToolsCall_UnknownUpstream_ReturnsToolNotFound()
    {
        var (ingress, _) = BuildIngressWithActionNode();

        var resp = await ingress.DispatchAsync(new JsonRpcRequest
        {
            Method = "tools/call",
            Id     = JsonDocument.Parse("1").RootElement,
            Params = JsonSerializer.SerializeToElement(new { name = "unknown__foo_bar" }, Json),
        });

        Assert.NotNull(resp.Error);
        Assert.Equal(JsonRpcErrorCodes.ToolNotFound, resp.Error!.Code);
    }

    [Fact]
    public async Task ToolsCall_UpstreamReturns404_SetsIsError()
    {
        var (ingress, handler) = BuildIngressWithActionNode();
        handler.InvokeStatus = HttpStatusCode.NotFound;
        handler.InvokeBody   = """{"status":"NPS-CLIENT-NOT-FOUND","error":"NWP-ACTION-NOT-FOUND"}""";

        var resp = await ingress.DispatchAsync(new JsonRpcRequest
        {
            Method = "tools/call",
            Id     = JsonDocument.Parse("1").RootElement,
            Params = JsonSerializer.SerializeToElement(new { name = "orders__orders_create" }, Json),
        });

        Assert.Null(resp.Error);
        Assert.True(resp.Result!.Value.GetProperty("isError").GetBoolean());
    }

    // ── Unknown method ───────────────────────────────────────────────────────

    [Fact]
    public async Task UnknownMethod_ReturnsMethodNotFound()
    {
        var (ingress, _) = BuildIngressWithMemoryNode();

        var resp = await ingress.DispatchAsync(new JsonRpcRequest
        {
            Method = "totally/unknown",
            Id     = JsonDocument.Parse("1").RootElement,
        });

        Assert.NotNull(resp.Error);
        Assert.Equal(JsonRpcErrorCodes.MethodNotFound, resp.Error!.Code);
    }

    // ── Construction guards ──────────────────────────────────────────────────

    [Fact]
    public void Options_DuplicateUpstreamNames_Throws()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Assert.Throws<InvalidOperationException>(() =>
            services.AddMcpIngress(o => o.Upstreams = new[]
            {
                new NwpUpstream { Name = "a", BaseUrl = new Uri("https://a.test") },
                new NwpUpstream { Name = "a", BaseUrl = new Uri("https://b.test") },
            }));
    }

    [Fact]
    public void Options_EmptyUpstreams_Throws()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Assert.Throws<InvalidOperationException>(() =>
            services.AddMcpIngress(_ => { /* leave Upstreams empty */ }));
    }

    // ── Test fixtures ────────────────────────────────────────────────────────

    private static (global::LabAcacia.McpIngress.McpIngress, StubHandler) BuildIngressWithMemoryNode()
    {
        var handler = StubHandler.ForMemoryNode();
        var opts    = new McpIngressOptions
        {
            ServerName = "LabAcacia.McpIngress.Tests",
            Upstreams  = new[] { new NwpUpstream { Name = "products", BaseUrl = new Uri("https://memory.test/products") } },
        };
        var client = new NwpUpstreamClient(new HttpClient(handler), opts.Upstreams[0]);
        var clients = new Dictionary<string, NwpUpstreamClient> { ["products"] = client };
        return (new global::LabAcacia.McpIngress.McpIngress(opts, clients), handler);
    }

    private static (global::LabAcacia.McpIngress.McpIngress, StubHandler) BuildIngressWithActionNode()
    {
        var handler = StubHandler.ForActionNode();
        var opts    = new McpIngressOptions
        {
            ServerName = "LabAcacia.McpIngress.Tests",
            Upstreams  = new[] { new NwpUpstream { Name = "orders", BaseUrl = new Uri("https://action.test/orders") } },
        };
        var client = new NwpUpstreamClient(new HttpClient(handler), opts.Upstreams[0]);
        var clients = new Dictionary<string, NwpUpstreamClient> { ["orders"] = client };
        return (new global::LabAcacia.McpIngress.McpIngress(opts, clients), handler);
    }
}

// ── Stub upstream ────────────────────────────────────────────────────────────

/// <summary>
/// In-memory HTTP handler that mimics an NWP Memory or Action Node. The type exposes
/// hook points (InvokeStatus/InvokeBody) so individual tests can flip behaviour.
/// </summary>
internal sealed class StubHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = new();

    /// <summary>Captured request bodies keyed by the full request URI — safe to read
    /// after <c>HttpClient</c> has disposed the original <see cref="HttpContent"/>.</summary>
    public List<(string Path, string Body)> RequestBodies { get; } = new();

    public string NwmBody     { get; set; } = string.Empty;
    public string ActionsBody { get; set; } = string.Empty;
    public string QueryBody   { get; set; } = string.Empty;

    public HttpStatusCode InvokeStatus { get; set; } = HttpStatusCode.OK;
    public string         InvokeBody   { get; set; } = """{"anchor_ref":null,"count":1,"data":[{"ok":true}],"token_est":0}""";

    public static StubHandler ForMemoryNode() => new()
    {
        NwmBody    = """{"nwp":"0.4","node_id":"urn:nps:node:test:products","node_type":"memory","display_name":"Products"}""",
        QueryBody  = """{"anchor_ref":"sha256:x","count":2,"data":[{"id":1},{"id":2}],"token_est":4}""",
    };

    public static StubHandler ForActionNode() => new()
    {
        NwmBody     = """{"nwp":"0.4","node_id":"urn:nps:node:test:orders","node_type":"action","display_name":"Orders"}""",
        ActionsBody = """
        {
          "actions": {
            "orders.create": { "description": "Create an order", "async": true },
            "orders.cancel": { "description": "Cancel an order", "async": false }
          }
        }
        """,
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        // Capture the request body eagerly — HttpClient disposes HttpContent after the
        // response is produced, so tests that inspect it post-dispatch would otherwise
        // hit ObjectDisposedException.
        if (request.Content is not null)
        {
            var body = await request.Content.ReadAsStringAsync(ct);
            RequestBodies.Add((path, body));
        }
        Requests.Add(request);

        return path switch
        {
            var p when p.EndsWith("/.nwm")   => Text(NwmBody),
            var p when p.EndsWith("/actions") => Text(ActionsBody),
            var p when p.EndsWith("/query")   => Text(QueryBody),
            var p when p.EndsWith("/invoke")  => new HttpResponseMessage(InvokeStatus)
            {
                Content = new StringContent(InvokeBody, Encoding.UTF8, "application/nwp-capsule"),
            },
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
    }

    private static HttpResponseMessage Text(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };
}
