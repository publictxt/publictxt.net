# PublicTxt.net
PublicTxt.Net is the .NET implementation of the PublicTxt concept. It provides:

 - A core library for reading, writing, and syncing Markdown-based PublicTxt instances to Git repositories.
 - Cross-platform clients:
   - Avalonia Desktop App for editing and aggregating knowledge locally. 
   - Blazor Web App for web-based access and collaboration.
 - Services for selective repository subscription, curation, and aggregation.

## Solution 

- Core
  - PublicTxt.Core
- Infrastructure
  - PublicTxt.Git
  - PublicTxt.Data
- Features
  - PublicTxt.Wiki
  - PublicTxt.Community
  - PublicTxt.Blog
  - PublicTxt.MetaWeb
- Apps
  - PublicTxt.Blazor: Web client for interactive editing and viewing.
  - PublicTxt.Avalonia: Cross-platform desktop interface.
  - PublicTxt.Tools: CLI utilities for batch operations, publishing, and data conversion.
