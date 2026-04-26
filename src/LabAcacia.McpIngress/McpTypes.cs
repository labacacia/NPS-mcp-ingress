// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace LabAcacia.McpIngress;

/// <summary>
/// Supported MCP protocol version this ingress implements.
/// See <see href="https://modelcontextprotocol.io/specification"/>.
/// </summary>
public static class McpProtocol
{
    public const string Version = "2024-11-05";
}

// ── initialize ───────────────────────────────────────────────────────────────

public sealed record McpInitializeResult
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; init; } = McpProtocol.Version;

    [JsonPropertyName("serverInfo")]
    public required McpServerInfo ServerInfo { get; init; }

    [JsonPropertyName("capabilities")]
    public required McpServerCapabilities Capabilities { get; init; }
}

public sealed record McpServerInfo
{
    [JsonPropertyName("name")]    public required string Name    { get; init; }
    [JsonPropertyName("version")] public required string Version { get; init; }
}

public sealed record McpServerCapabilities
{
    [JsonPropertyName("resources")]
    public McpResourceCapabilities? Resources { get; init; }

    [JsonPropertyName("tools")]
    public McpToolCapabilities? Tools { get; init; }
}

public sealed record McpResourceCapabilities
{
    [JsonPropertyName("subscribe")]   public bool Subscribe   { get; init; }
    [JsonPropertyName("listChanged")] public bool ListChanged { get; init; }
}

public sealed record McpToolCapabilities
{
    [JsonPropertyName("listChanged")] public bool ListChanged { get; init; }
}

// ── resources/list, resources/read ───────────────────────────────────────────

public sealed record McpResource
{
    [JsonPropertyName("uri")]         public required string Uri { get; init; }
    [JsonPropertyName("name")]        public required string Name { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("mimeType")]    public string? MimeType { get; init; }
}

public sealed record McpResourceListResult
{
    [JsonPropertyName("resources")]
    public required IReadOnlyList<McpResource> Resources { get; init; }
}

public sealed record McpResourceReadParams
{
    [JsonPropertyName("uri")]
    public required string Uri { get; init; }
}

public sealed record McpResourceReadResult
{
    [JsonPropertyName("contents")]
    public required IReadOnlyList<McpResourceContent> Contents { get; init; }
}

public sealed record McpResourceContent
{
    [JsonPropertyName("uri")]      public required string Uri { get; init; }
    [JsonPropertyName("mimeType")] public string? MimeType { get; init; }

    /// <summary>Textual payload. Exactly one of <see cref="Text"/> or <see cref="Blob"/> is set.</summary>
    [JsonPropertyName("text")]     public string? Text { get; init; }

    /// <summary>Base64-encoded binary payload.</summary>
    [JsonPropertyName("blob")]     public string? Blob { get; init; }
}

// ── tools/list, tools/call ───────────────────────────────────────────────────

public sealed record McpTool
{
    [JsonPropertyName("name")]        public required string Name { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }

    /// <summary>JSON-Schema of the tool's input arguments.</summary>
    [JsonPropertyName("inputSchema")]
    public required JsonElement InputSchema { get; init; }
}

public sealed record McpToolListResult
{
    [JsonPropertyName("tools")]
    public required IReadOnlyList<McpTool> Tools { get; init; }
}

public sealed record McpToolCallParams
{
    [JsonPropertyName("name")]      public required string Name { get; init; }
    [JsonPropertyName("arguments")] public JsonElement? Arguments { get; init; }
}

public sealed record McpToolCallResult
{
    [JsonPropertyName("content")]
    public required IReadOnlyList<McpContent> Content { get; init; }

    [JsonPropertyName("isError")]
    public bool IsError { get; init; }
}

public sealed record McpContent
{
    /// <summary>Content type: <c>"text"</c>, <c>"image"</c>, or <c>"resource"</c>.</summary>
    [JsonPropertyName("type")] public required string Type { get; init; }

    [JsonPropertyName("text")] public string? Text { get; init; }
}
