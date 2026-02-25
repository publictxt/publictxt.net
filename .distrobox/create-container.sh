#!/bin/bash
# Create .NET development distrobox container

set -e

echo "🚀 Creating .NET development container..."
echo ""

# Create the container using Fedora toolbox image
# distrobox create \
#   --name dotnetbox \
#   --image registry.fedoraproject.org/fedora-toolbox:latest \
#   --yes

# Create the container using Universal Blue Fedora toolbox image
distrobox create \
  --name dotnetbox \
  --image ghcr.io/ublue-os/fedora-toolbox:latest \
  --yes

echo ""
echo "✅ Container 'dotnetbox' created successfully!"
echo ""
echo "Next steps:"
echo "  1. Enter the container: distrobox enter dotnetbox"
echo "  2. Run setup script: ./.distrobox/setup.sh"
echo ""
