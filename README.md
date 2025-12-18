# EmpireWebApp

A lightweight ASP.NET Core Razor Pages implementation of the "Empire" party game with a TV display and phone controllers. The app uses SignalR for realtime updates and stores all game state in-memory.

## Running locally

1. Install the .NET 8 SDK.
2. Restore and run the app:
   ```bash
   dotnet restore
   dotnet run
   ```
3. Navigate to `/create` to start a new game. Use the generated code on phones at `/join`, then open `/game/{code}/tv` on the TV or a browser window.

## Key features
- Realtime updates via SignalR groups per game code.
- In-memory, thread-safe game store (no database required).
- TV display with host controls, prompt cycling, and optional speech synthesis.
- Mobile-friendly phone controller with prompt submission, guessing, and confirmation flows.
