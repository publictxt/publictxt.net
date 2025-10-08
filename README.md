# PublicTxt.net

PublicTxt.Net is an in-progress .NET implementation of the [PublicTxt](https://github.com/publictxt/publictext) idea, consisting of:
 - A core library for reading, writing, and syncing Markdown-based PublicTxt instances to Git repositories.
 - Feature based services
 - Infrastructure Projects:
   - Git Repository access and management
   - Local Database Options
 - Cross-platform clients:
   - Avalonia Desktop App for editing and aggregating knowledge locally. 
   - Blazor Web App for web-based access and collaboration.

## Solution Project Structure

- Core
  - PublicTxt.Core
- Features
    - PublicTxt.Wiki
    - PublicTxt.Community
    - PublicTxt.Blog
    - PublicTxt.MetaWeb
- Infrastructure
  - PublicTxt.Git
  - PublicTxt.Data

- Apps
  - PublicTxt.Blazor: Web client for interactive editing and viewing.
  - PublicTxt.Avalonia: Cross-platform desktop interface.
  - PublicTxt.Tools: CLI utilities for batch operations, publishing, and data conversion.
