// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

namespace LabAcacia.McpBridge;

/// <summary>
/// Declares one NWP node that the bridge should expose to MCP clients.
/// The node type is derived from the upstream <c>/.nwm</c> response.
/// </summary>
public sealed record NwpUpstream
{
    /// <summary>Human-readable tag, used to namespace MCP resource URIs and tool names.</summary>
    public required string Name { get; init; }

    /// <summary>Base URL of the NWP node (scheme + host + path prefix, no trailing slash).
    /// Example: <c>https://api.example.com/products</c>.</summary>
    public required Uri BaseUrl { get; init; }

    /// <summary>Optional Agent NID forwarded as <c>X-NWP-Agent</c>.</summary>
    public string? AgentNid { get; init; }

    /// <summary>Optional Bearer token or similar, forwarded as <c>Authorization</c>.</summary>
    public string? AuthHeader { get; init; }
}

/// <summary>Configuration for the bridge server.</summary>
public sealed class McpBridgeOptions
{
    /// <summary>Server name reported in <c>initialize</c>.</summary>
    public string ServerName { get; set; } = "LabAcacia.McpBridge";

    /// <summary>Server version reported in <c>initialize</c>.</summary>
    public string ServerVersion { get; set; } = "0.1.0-alpha.1";

    /// <summary>One or more NWP nodes to expose.</summary>
    public required IReadOnlyList<NwpUpstream> Upstreams { get; set; }

    /// <summary>
    /// Max rows a single <c>resources/read</c> call may return from a Memory Node.
    /// NWP default row page cap is 1000 — mirror it here. Default: 100.
    /// </summary>
    public uint ResourceReadLimit { get; set; } = 100;
}
