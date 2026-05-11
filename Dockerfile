# syntax=docker/dockerfile:1
#
# Containerised distribution of the maf-autopilot MCP server.
#
# Usage:
#   docker pull ghcr.io/joslat/maf-autopilot:latest
#   docker run --rm -i ghcr.io/joslat/maf-autopilot
#
# The container speaks MCP over stdio. Wire it up in your IDE's mcp.json with:
#   {
#     "servers": {
#       "maf-autopilot": {
#         "type": "stdio",
#         "command": "docker",
#         "args": ["run", "--rm", "-i", "ghcr.io/joslat/maf-autopilot:latest"]
#       }
#     }
#   }
#
# Image stays small because the registry YAML, migration guide, and skill
# documents are embedded into the .dll at build time (no extra COPY needed).

# ---------- Stage 1 — build ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only the files needed to restore — better layer caching.
COPY src/maf-autopilot/maf-autopilot.csproj src/maf-autopilot/
COPY src/maf-autopilot/*.cs src/maf-autopilot/
COPY src/maf-autopilot/Tools/ src/maf-autopilot/Tools/
COPY src/maf-autopilot/Prompts/ src/maf-autopilot/Prompts/
COPY src/maf-autopilot/Resources/ src/maf-autopilot/Resources/
COPY src/maf-autopilot/Data/ src/maf-autopilot/Data/
COPY src/maf-autopilot/Scaffolding/ src/maf-autopilot/Scaffolding/

# Embedded resources required for `<EmbeddedResource>` in the csproj.
COPY .github/ .github/
COPY guides/ guides/
COPY README.md ./

RUN dotnet publish src/maf-autopilot/maf-autopilot.csproj \
    --configuration Release \
    --output /app \
    --self-contained false \
    -p:UseAppHost=false

# ---------- Stage 2 — runtime ----------
# The runtime image is much smaller than the SDK image (~125 MB vs ~860 MB).
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app

# Copy build output. Embedded resources (registry.yaml, constraints.md,
# guide.md, all 12 skill *.md files) are inside the dll, no extra COPY needed.
COPY --from=build /app .

# OCI labels — useful for `docker inspect` and registry browsing.
LABEL org.opencontainers.image.title="maf-autopilot"
LABEL org.opencontainers.image.description="MCP server + Roslyn analyzers for Microsoft Agent Framework (MAF) — migration, audit, prompt-lint, cost auditor, scaffolder, simulator."
LABEL org.opencontainers.image.source="https://github.com/joslat/maf-autopilot"
LABEL org.opencontainers.image.licenses="MIT"

# Security: drop root. The MCP server speaks stdio only and writes nothing to the
# filesystem at runtime, so a non-root UID is sufficient. Mounted user workspaces
# (e.g. `docker run -v $PWD:/workspace`) need read access only; grant 1001:0 so the
# server can still read default-permissioned workspace mounts on POSIX hosts.
RUN groupadd -r maf && useradd -r -u 1001 -g maf -d /nonexistent -s /usr/sbin/nologin maf \
    && chown -R maf:maf /app
USER maf

ENTRYPOINT ["dotnet", "/app/maf-autopilot.dll"]
