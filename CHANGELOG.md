English | [中文版](./CHANGELOG.cn.md)

# Changelog — MCP Ingress (`LabAcacia.McpIngress`)

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Until NPS reaches v1.0 stable, every repository in the suite is synchronized to the same pre-release version tag.

---

## [1.0.0-alpha.4] — 2026-04-30

### Synced

- Version bumped 1.0.0-alpha.3 → 1.0.0-alpha.4 in lockstep with the
  rest of the NPS suite. No functional changes in MCP Ingress itself.
- `LabAcacia.NPS.NWP` dependency follows to alpha.4, which carries
  the new `LabAcacia.NPS.NWP.Anchor` topology query types
  (NPS-CR-0002). MCP Ingress does not expose those query types over
  MCP at alpha.4 — they are .NET-only consumer surface for now.
- 15 tests still green.

### Summary

- Exposes NWP Memory / Action / Complex Nodes as MCP 2024-11-05 servers
  over JSON-RPC 2.0 (HTTP). External MCP clients can call the same
  Memory / Action / Complex Node surface that NPS clients call over
  NWP, no SDK on the client side required.

---

## [1.0.0-alpha.3] — 2026-04-26

### Renamed (BREAKING)

- Package renamed `LabAcacia.McpBridge` → `LabAcacia.McpIngress` per [NPS-CR-0001](https://github.com/labacacia/NPS-Dev/blob/dev/spec/cr/NPS-CR-0001-anchor-bridge-split.md). The new spec-level **Bridge Node** type (NWP §2A) carries the *NPS → external* direction; this package carries the **inverse** direction (external → NPS) and is therefore renamed `*Ingress`. The on-the-wire surface is identical to alpha.2; only the assembly name + namespace changed. Consumers update `<PackageReference Include="LabAcacia.McpBridge"/>` → `LabAcacia.McpIngress` and the `using LabAcacia.McpBridge;` import.
- The corresponding GitHub repository was renamed `labacacia/NPS-mcp-bridge` → `labacacia/NPS-mcp-ingress`. GitHub redirects the old URL automatically; existing clones can update with `git remote set-url origin https://github.com/labacacia/NPS-mcp-ingress.git`.
- Tests still pass at the same count as alpha.2 (no functional change beyond rename).

### Synced

- Version bumped 1.0.0-alpha.2 → 1.0.0-alpha.3 in lockstep with the rest of the NPS suite.

---

## [1.0.0-alpha.2] — 2026-04-19

### Changed

- Version bump to `1.0.0-alpha.2` for suite-wide synchronization. No functional changes since `1.0.0-alpha.1`.
- 15 tests green.

### Summary

- Exposes NWP Memory / Action / Complex Nodes as MCP 2024-11-05 servers over JSON-RPC 2.0 (HTTP).

---

## [1.0.0-alpha.1] — 2026-04-10

Initial release under the NPS suite `v1.0.0-alpha.1` umbrella tag.

[1.0.0-alpha.4]: https://github.com/labacacia/NPS-mcp-ingress/releases/tag/v1.0.0-alpha.4
[1.0.0-alpha.3]: https://github.com/labacacia/NPS-mcp-ingress/releases/tag/v1.0.0-alpha.3
[1.0.0-alpha.2]: https://github.com/LabAcacia/nps/releases/tag/v1.0.0-alpha.2
[1.0.0-alpha.1]: https://github.com/LabAcacia/nps/releases/tag/v1.0.0-alpha.1
