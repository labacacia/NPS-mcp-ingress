// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http.Json;
using System.Text.Json;

namespace LabAcacia.McpBridge;

/// <summary>
/// Thin typed client for a single upstream NWP node. Knows how to read <c>/.nwm</c>,
/// <c>/query</c>, <c>/.schema</c>, <c>/actions</c>, and <c>/invoke</c>.
/// </summary>
public sealed class NwpUpstreamClient
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly NwpUpstream _up;

    public NwpUpstreamClient(HttpClient http, NwpUpstream upstream)
    {
        _http = http;
        _up   = upstream;
    }

    public NwpUpstream Upstream => _up;

    // ── Requests ─────────────────────────────────────────────────────────────

    public Task<HttpResponseMessage> GetNwmAsync(CancellationToken ct = default) =>
        SendAsync(HttpMethod.Get, "/.nwm", body: null, ct);

    public Task<HttpResponseMessage> GetActionsAsync(CancellationToken ct = default) =>
        SendAsync(HttpMethod.Get, "/actions", body: null, ct);

    public Task<HttpResponseMessage> PostQueryAsync(JsonElement body, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, "/query", body, ct);

    public Task<HttpResponseMessage> PostInvokeAsync(JsonElement body, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, "/invoke", body, ct);

    // ── Core sender (reused by all upstream calls) ───────────────────────────

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string subPath, JsonElement? body, CancellationToken ct)
    {
        var url = new Uri(_up.BaseUrl.ToString().TrimEnd('/') + subPath);
        using var req = new HttpRequestMessage(method, url);

        if (body is not null)
        {
            req.Content = JsonContent.Create(body.Value, options: Json);
            req.Content.Headers.ContentType!.MediaType = "application/nwp-frame";
        }

        if (!string.IsNullOrEmpty(_up.AgentNid))
            req.Headers.Add("X-NWP-Agent", _up.AgentNid);
        if (!string.IsNullOrEmpty(_up.AuthHeader))
            req.Headers.TryAddWithoutValidation("Authorization", _up.AuthHeader);

        return await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct);
    }
}
