#!/bin/bash
# Install VSCode inside distrobox and export it to host
# Run this inside your dotnetbox container

set -e

echo "📦 Installing VSCode in distrobox container..."
echo ""

# Import Microsoft GPG key
echo "🔑 Adding Microsoft repository..."
sudo rpm --import https://packages.microsoft.com/keys/microsoft.asc

# Add VSCode repository
sudo tee /etc/yum.repos.d/vscode.repo > /dev/null <<'EOF'
[code]
name=Visual Studio Code
baseurl=https://packages.microsoft.com/yumrepos/vscode
enabled=1
gpgcheck=1
gpgkey=https://packages.microsoft.com/keys/microsoft.asc
EOF

# Install VSCode
echo "📦 Installing Visual Studio Code..."
sudo dnf install -y code

# Verify installation
if command -v code &> /dev/null; then
    echo ""
    echo "✅ VSCode installed successfully!"
    echo "   Version: $(code --version | head -n1)"
else
    echo "❌ VSCode installation failed"
    exit 1
fi

echo ""
echo "📤 Exporting VSCode to host..."
echo "   This creates a launcher on your host that runs VSCode from the container"
echo ""

# Export VSCode application
distrobox-export --app code

echo ""
echo "✅ Setup complete!"
echo ""
echo "Next steps:"
echo "  1. Exit the container: exit"
echo "  2. Launch VSCode from host: code"
echo "     (or find 'Visual Studio Code' in your application menu)"
echo "  3. VSCode will run inside the container with access to all tools"
echo "  4. Open your project from within VSCode"
echo ""
echo "Note: When you run 'code' from your host, it actually runs inside"
echo "    the distrobox container with full access to .NET, X11, etc."
echo "    UNLESS you have an existing VSCode installation on your host."
echo "    In that case, you may use the command: " 
echo "         'distrobox-enter <container-name> -- code ' "
echo "    to launch VSCode from the container explicitly."
echo ""