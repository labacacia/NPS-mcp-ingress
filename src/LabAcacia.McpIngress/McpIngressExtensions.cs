// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LabAcacia.McpIngress;

/// <summary>DI + pipeline extensions for the MCP ingress.</summary>
public static class McpIngressExtensions
{
    /// <summary>
    /// Register an <see cref="McpIngress"/> with the given upstream configuration.
    /// Each upstream gets its own typed <c>HttpClient</c> via <c>IHttpClientFactory</c>.
    /// </summary>
    public static IServiceCollection AddMcpIngress(
        this IServiceCollection services,
        Action<McpIngressOptions> configure)
    {
        var opts = new McpIngressOptions { Upstreams = Array.Empty<NwpUpstream>() };
        configure(opts);
        if (opts.Upstreams.Count == 0)
            throw new InvalidOperationException("McpIngressOptions.Upstreams MUST contain at least one entry.");

        // Duplicate-name guard — upstream names become URI hosts, so they must be unique.
        var dup = opts.Upstreams.GroupBy(u => u.Name).FirstOrDefault(g => g.Count() > 1);
        if (dup is not null)
            throw new InvalidOperationException($"Duplicate upstream name '{dup.Key}' in McpIngressOptions.Upstreams.");

        services.AddSingleton(opts);
        services.AddHttpClient();
        services.AddSingleton<McpIngress>(sp =>
        {
            var http = sp.GetRequiredService<IHttpClientFactory>();
            var clients = opts.Upstreams.ToDictionary(
                u => u.Name,
                u => new NwpUpstreamClient(http.CreateClient($"mcp-ingress:{u.Name}"), u));
            return new McpIngress(opts, clients,
                sp.GetService<Microsoft.Extensions.Logging.ILogger<McpIngress>>());
        });

        return services;
    }

    /// <summary>
    /// Map the MCP JSON-RPC endpoint. Default path is <c>/mcp</c>. The endpoint accepts
    /// POST with a JSON-RPC 2.0 request body and returns a JSON-RPC response.
    /// </summary>
    public static IEndpointConventionBuilder MapMcpIngress(
        this IEndpointRouteBuilder endpoints,
        string path = "/mcp")
    {
        return endpoints.MapPost(path, async (HttpContext ctx, McpIngress ingress) =>
        {
            if (!ctx.Request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) ?? true)
            {
                ctx.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                return;
            }

            JsonRpcRequest? req;
            try
            {
                req = await JsonSerializer.DeserializeAsync<JsonRpcRequest>(
                    ctx.Request.Body, McpIngress.Json, ctx.RequestAborted);
            }
            catch (JsonException jex)
            {
                await WriteResponse(ctx, new JsonRpcResponse
                {
                    Error = new JsonRpcError { Code = JsonRpcErrorCodes.ParseError, Message = jex.Message },
                });
                return;
            }

            if (req is null || string.IsNullOrEmpty(req.Method))
            {
                await WriteResponse(ctx, new JsonRpcResponse
                {
                    Error = new JsonRpcError { Code = JsonRpcErrorCodes.InvalidRequest, Message = "invalid JSON-RPC request" },
                });
                return;
            }

            var resp = await ingress.DispatchAsync(req, ctx.RequestAborted);

            // Notifications (id == null) MUST NOT produce a response per JSON-RPC 2.0.
            if (req.Id is null)
            {
                ctx.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }

            await WriteResponse(ctx, resp);
        });
    }

    private static async Task WriteResponse(HttpContext ctx, JsonRpcResponse resp)
    {
        ctx.Response.StatusCode  = StatusCodes.Status200OK;
        ctx.Response.ContentType = "application/json";
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(resp, McpIngress.Json));
        await ctx.Response.Body.WriteAsync(bytes);
    }
}
