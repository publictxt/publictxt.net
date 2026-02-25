#!/bin/bash
# Install .NET 10 SDK (LTS)
# Note: .NET 10 is not yet available in Fedora repos, so we use Microsoft's installer

set -e

echo "========================================="
echo "📦 Installing .NET 10 SDK (LTS)"
echo "========================================="
echo ""
echo "Note: Using Microsoft's installer (not yet in Fedora repos)"
echo ""

# Download and run Microsoft's dotnet-install script
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 10.0

# Add to PATH for current session
export DOTNET_ROOT=$HOME/.dotnet
export PATH=$PATH:$HOME/.dotnet:$HOME/.dotnet/tools

# Add to shell profile for persistence
if ! grep -q "DOTNET_ROOT" ~/.bashrc; then
    echo "" >> ~/.bashrc
    echo "# .NET SDK Configuration" >> ~/.bashrc
    echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
    echo 'export PATH=$PATH:$HOME/.dotnet:$HOME/.dotnet/tools' >> ~/.bashrc
fi

echo ""
echo "Verifying installation..."
$HOME/.dotnet/dotnet --version
$HOME/.dotnet/dotnet --list-sdks

echo ""
echo "========================================="
echo "✅ .NET 10 installed successfully!"
echo "========================================="
echo ""
echo "📝 Notes:"
echo "  • .NET 10 installed in ~/.dotnet"
echo "  • Coexists with system .NET 9"
echo "  • Restart shell or run: source ~/.bashrc"
echo ""