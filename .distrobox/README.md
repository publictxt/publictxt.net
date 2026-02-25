# .NET Development Distrobox

Simple distrobox setup for .NET development

## Quick Start

```bash
# 1. Create container
./.distrobox/create-container.sh

# 2. Enter container
distrobox enter dotnetbox

# 3. Run setup (installs .NET 9 + Avalonia dependencies)
./.distrobox/setup.sh

# 4. Optional: Install VS Code (recommended)
./.distrobox/install-vscode.sh

# 5. Optional: Install .NET 10 LTS
./.distrobox/install-dotnet10.sh
```

**What you get:**
- .NET 9 SDK (from Fedora repos)
- .NET 10 SDK (optional, LTS from Microsoft)
- Avalonia GUI dependencies


## Troubleshooting

**GUI apps not displaying:**
```bash
echo $DISPLAY  # Should show :0 or similar
```

## Files

- `create-container.sh` - Creates container with Universal Blue Fedora image
- `setup.sh` - Installs .NET 9 + Avalonia dependencies
- `install-dotnet10.sh` - Optional: Installs .NET 10 LTS
- `install-vscode.sh` - Optional: Installs VS Code
