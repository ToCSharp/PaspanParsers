# PaspanParsers

## Build and test

- Build: `dotnet build PaspanParsers.slnx`
- Run tests: `dotnet run --project src/PaspanParsers.Tests`

Do not use `dotnet test`: the test project uses Microsoft.Testing.Platform, and the VSTest-based `dotnet test` is rejected by the .NET 10 SDK.
