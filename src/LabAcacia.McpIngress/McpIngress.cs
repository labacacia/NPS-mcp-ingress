// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LabAcacia.McpIngress;

/// <summary>
/// Core MCP ↔ NWP dispatcher. Translates MCP JSON-RPC methods into calls on one or
/// more upstream NWP nodes.
/// <list type="bullet">
///   <item><c>resources/list</c>, <c>resources/read</c> → Memory Node <c>/query</c></item>
///   <item><c>tools/list</c>, <c>tools/call</c> → Action Node <c>/actions</c> + <c>/invoke</c></item>
/// </list>
/// Each upstream gets a URI scheme prefix derived from <see cref="NwpUpstream.Name"/>
/// (<c>nwp://{name}/</c>) so MCP clients can address resources/tools unambiguously.
/// </summary>
public sealed class McpIngress
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly McpIngressOptions _options;
    private readonly IReadOnlyDictionary<string, NwpUpstreamClient> _clients;
    private readonly ILogger _log;

    public McpIngress(
        McpIngressOptions options,
        IReadOnlyDictionary<string, NwpUpstreamClient> clients,
        ILogger<McpIngress>? logger = null)
    {
        _options = options;
        _clients = clients;
        _log     = (ILogger?)logger ?? NullLogger.Instance;
    }

    /// <summary>Dispatches one JSON-RPC request. Returns a populated <see cref="JsonRpcResponse"/>.</summary>
    public async Task<JsonRpcResponse> DispatchAsync(JsonRpcRequest req, CancellationToken ct = default)
    {
        try
        {
            return req.Method switch
            {
                "initialize"           => Ok(req.Id, HandleInitialize()),
                "resources/list"       => Ok(req.Id, await HandleResourceList(ct)),
                "resources/read"       => Ok(req.Id, await HandleResourceRead(req.Params, ct)),
                "tools/list"           => Ok(req.Id, await HandleToolList(ct)),
                "tools/call"           => Ok(req.Id, await HandleToolCall(req.Params, ct)),
                "ping"                 => Ok(req.Id, JsonSerializer.SerializeToElement(new { }, Json)),
                _                      => Err(req.Id, JsonRpcErrorCodes.MethodNotFound,
                                              $"Method '{req.Method}' is not supported by this ingress."),
            };
        }
        catch (IngressException bex)
        {
            return Err(req.Id, bex.Code, bex.Message);
        }
        catch (JsonException jex)
        {
            return Err(req.Id, JsonRpcErrorCodes.InvalidParams, jex.Message);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ingress dispatch failed for method {Method}", req.Method);
            return Err(req.Id, JsonRpcErrorCodes.InternalError, ex.Message);
        }
    }

    // ── initialize ───────────────────────────────────────────────────────────

    private JsonElement HandleInitialize() =>
        JsonSerializer.SerializeToElement(new McpInitializeResult
        {
            ServerInfo = new McpServerInfo
            {
                Name    = _options.ServerName,
                Version = _options.ServerVersion,
            },
            Capabilities = new McpServerCapabilities
            {
                Resources = new McpResourceCapabilities(),
                Tools     = new McpToolCapabilities(),
            },
        }, Json);

    // ── resources/list ───────────────────────────────────────────────────────

    private async Task<JsonElement> HandleResourceList(CancellationToken ct)
    {
        var list = new List<McpResource>();
        foreach (var (name, client) in _clients)
        {
            var nwm = await ReadNwm(client, ct);
            if (nwm?.NodeType != "memory") continue;

            list.Add(new McpResource
            {
                Uri         = $"nwp://{name}/",
                Name        = nwm.DisplayName ?? name,
                Description = $"NWP Memory Node '{name}' — use resources/read to query.",
                MimeType    = "application/json",
            });
        }
        return JsonSerializer.SerializeToElement(new McpResourceListResult { Resources = list }, Json);
    }

    // ── resources/read ───────────────────────────────────────────────────────

    private async Task<JsonElement> HandleResourceRead(JsonElement? rawParams, CancellationToken ct)
    {
        var p = rawParams?.Deserialize<McpResourceReadParams>(Json)
            ?? throw new IngressException(JsonRpcErrorCodes.InvalidParams, "resources/read requires `uri`.");

        if (!Uri.TryCreate(p.Uri, UriKind.Absolute, out var uri) || uri.Scheme != "nwp")
            throw new IngressException(JsonRpcErrorCodes.InvalidParams,
                $"Resource URI '{p.Uri}' must be nwp://<name>/");

        var upstreamName = uri.Host;
        if (!_clients.TryGetValue(upstreamName, out var client))
            throw new IngressException(JsonRpcErrorCodes.ResourceNotFound,
                $"Unknown upstream '{upstreamName}'.");

        // Build a QueryFrame asking for at most `ResourceReadLimit` rows.
        var queryBody = JsonSerializer.SerializeToElement(
            new { limit = _options.ResourceReadLimit }, Json);

        using var resp = await client.PostQueryAsync(queryBody, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new IngressException(JsonRpcErrorCodes.UpstreamError,
                $"Upstream '{upstreamName}' returned {(int)resp.StatusCode}: {err}");
        }

        var text = await resp.Content.ReadAsStringAsync(ct);
        var result = new McpResourceReadResult
        {
            Contents = new[]
            {
                new McpResourceContent
                {
                    Uri      = p.Uri,
                    MimeType = "application/json",
                    Text     = text,
                },
            },
        };
        return JsonSerializer.SerializeToElement(result, Json);
    }

    // ── tools/list ───────────────────────────────────────────────────────────

    private async Task<JsonElement> HandleToolList(CancellationToken ct)
    {
        var tools = new List<McpTool>();

        foreach (var (name, client) in _clients)
        {
            var nwm = await ReadNwm(client, ct);
            if (nwm?.NodeType is not ("action" or "complex")) continue;

            // Fetch /actions
            using var resp = await client.GetActionsAsync(ct);
            if (!resp.IsSuccessStatusCode) continue;

            var body = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("actions", out var actions)) continue;

            foreach (var prop in actions.EnumerateObject())
            {
                var desc = prop.Value.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String
                    ? d.GetString() : null;

                tools.Add(new McpTool
                {
                    Name        = $"{name}__{prop.Name.Replace('.', '_')}",
                    Description = desc,
                    // Empty schema — the NWP node validates params server-side.
                    InputSchema = JsonSerializer.SerializeToElement(
                        new { type = "object", additionalProperties = true }, Json),
                });
            }
        }

        return JsonSerializer.SerializeToElement(new McpToolListResult { Tools = tools }, Json);
    }

    // ── tools/call ───────────────────────────────────────────────────────────

    private async Task<JsonElement> HandleToolCall(JsonElement? rawParams, CancellationToken ct)
    {
        var p = rawParams?.Deserialize<McpToolCallParams>(Json)
            ?? throw new IngressException(JsonRpcErrorCodes.InvalidParams, "tools/call requires `name`.");

        // Tool name format: {upstream}__{action_id_with_underscores}
        var sep = p.Name.IndexOf("__", StringComparison.Ordinal);
        if (sep <= 0)
            throw new IngressException(JsonRpcErrorCodes.ToolNotFound,
                $"Tool name '{p.Name}' is malformed (expected '<upstream>__<action>').");

        var upstreamName = p.Name[..sep];
        var actionId     = p.Name[(sep + 2)..].Replace('_', '.');

        if (!_clients.TryGetValue(upstreamName, out var client))
            throw new IngressException(JsonRpcErrorCodes.ToolNotFound,
                $"Unknown upstream '{upstreamName}'.");

        var invokeBody = JsonSerializer.SerializeToElement(new
        {
            action_id = actionId,
            @params   = p.Arguments,
        }, Json);

        using var resp = await client.PostInvokeAsync(invokeBody, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        // Forward the NWP response verbatim as a text content block. `isError` flips
        // when the upstream returned anything in the 4xx/5xx range.
        var result = new McpToolCallResult
        {
            IsError = !resp.IsSuccessStatusCode,
            Content = new[]
            {
                new McpContent { Type = "text", Text = body },
            },
        };
        return JsonSerializer.SerializeToElement(result, Json);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<NwmSnapshot?> ReadNwm(NwpUpstreamClient client, CancellationToken ct)
    {
        try
        {
            using var resp = await client.GetNwmAsync(ct);
            if (!resp.IsSuccessStatusCode) return null;
            var body = await resp.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<NwmSnapshot>(body, NwpUpstreamClient.Json);
        }
        catch
        {
            return null;
        }
    }

    private static JsonRpcResponse Ok(JsonElement? id, JsonElement result) =>
        new() { Id = id, Result = result };

    private static JsonRpcResponse Err(JsonElement? id, int code, string message) =>
        new() { Id = id, Error = new JsonRpcError { Code = code, Message = message } };

    /// <summary>Minimal NWM projection used to pick the right upstream role.</summary>
    private sealed record NwmSnapshot
    {
        public string? NodeType    { get; init; }
        public string? DisplayName { get; init; }
    }
}

/// <summary>Internal exception carrying a JSON-RPC error code.</summary>
internal sealed class IngressException(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}
