// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace LabAcacia.McpIngress;

/// <summary>
/// JSON-RPC 2.0 request (<see href="https://www.jsonrpc.org/specification"/>).
/// MCP uses JSON-RPC over stdio or HTTP+SSE; this ingress targets HTTP.
/// </summary>
public sealed record JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    /// <summary>Request id. <c>null</c> indicates a notification (no response expected).</summary>
    [JsonPropertyName("id")]
    public JsonElement? Id { get; init; }

    [JsonPropertyName("method")]
    public required string Method { get; init; }

    [JsonPropertyName("params")]
    public JsonElement? Params { get; init; }
}

/// <summary>JSON-RPC 2.0 successful response envelope.</summary>
public sealed record JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("id")]
    public JsonElement? Id { get; init; }

    [JsonPropertyName("result")]
    public JsonElement? Result { get; init; }

    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; init; }
}

/// <summary>JSON-RPC 2.0 error object.</summary>
public sealed record JsonRpcError
{
    [JsonPropertyName("code")]
    public required int Code { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("data")]
    public JsonElement? Data { get; init; }
}

/// <summary>Standard JSON-RPC error codes plus MCP-specific ones.</summary>
public static class JsonRpcErrorCodes
{
    public const int ParseError     = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams  = -32602;
    public const int InternalError  = -32603;

    // MCP-ingress-specific application errors (use the range -32000..-32099)
    public const int UpstreamError  = -32000;
    public const int ResourceNotFound = -32001;
    public const int ToolNotFound   = -32002;
}
