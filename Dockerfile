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
#       "maf-doctor": {
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
# SDK 10.0 required because the csproj multi-targets net8.0/net9.0/net10.0
# (multi-version test parity with the analyzer NuGet). SDK 8.0 — which we used
# through the alphas — can't restore net9.0/net10.0 TFMs and fails NETSDK1045.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Repo-root build-influencing files. CRITICAL: csproj uses Central Package
# Management (CPM) — package versions live in Directory.Packages.props, NOT
# in the csproj. Without these COPYs, restore fails NU1015 ("PackageReference
# items do not have a version specified"). global.json pins the SDK band.
COPY Directory.Build.props Directory.Packages.props global.json ./

# Copy the whole MCP server project in one shot — `.dockerignore` already
# excludes bin/obj/test-projects so this stays lean. Switched from per-folder
# COPYs after the docker-publish v1.0.0 run failed because the Commands/
# folder wasn't enumerated. Folder-by-folder COPYs are fragile — any new
# subfolder silently breaks the image build. Single COPY is robust.
COPY src/maf-autopilot/ src/maf-autopilot/

# Embedded resources required for `<EmbeddedResource>` in the csproj.
# The csproj embeds 4 files from docs/steering/ (init drops them into user repos);
# .dockerignore explicitly un-excludes that subfolder. Plus .github/skills,
# .github/instructions, guides/, and root README.md.
COPY .github/ .github/
COPY guides/ guides/
COPY docs/steering/ docs/steering/
COPY README.md ./

# Publish a single TFM (net10.0 — matches the runtime image below). Required
# because `dotnet publish` on a multi-TFM csproj without `--framework` fails.
RUN dotnet publish src/maf-autopilot/maf-autopilot.csproj \
    --configuration Release \
    --framework net10.0 \
    --output /app \
    --self-contained false \
    -p:UseAppHost=false

# ---------- Stage 2 — runtime ----------
# The runtime image is much smaller than the SDK image (~150 MB vs ~900 MB).
# Must match the --framework TFM published above.
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
