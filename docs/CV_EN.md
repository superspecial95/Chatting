# Mini Discord — CV-ready description

Use only the bullets that match what you can explain in an interview. Do not describe this learning project as production-ready or publicly deployed.

**Project:** Mini Discord | ASP.NET Core, SignalR, EF Core, SQL Server, WPF, xUnit

- Built a desktop chat application with an ASP.NET Core API, a SignalR hub, and a WPF client for direct messaging, friend requests, message history, and file sharing.
- Implemented JWT authentication and server-side authorization for friend-only messaging and attachment downloads; stored new uploads outside the public web root.
- Added automated unit and HTTP/SignalR integration tests covering registration, login, friendship, real-time delivery, message history, and file access.

**Interview examples to prepare:** Explain why a JWT alone does not grant access to another user's messages; how SignalR connects users and persists messages; why `wwwroot/uploads` was replaced for new files; how EF Core migrations changed an existing SQL Server database without losing its rows; and what the InMemory API tests do *not* prove about SQL Server constraints or deployment.

Before sharing the repository publicly, use test accounts and remove real personal data from screenshots. Never commit SQL backups, runtime uploads, User Secrets, or a database export containing chat history.
