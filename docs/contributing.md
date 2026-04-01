# Contributing

Lorex is a young project and contributions are very welcome.

## Getting the code

```bash
git clone https://github.com/alirezanet/lorex
cd lorex
```

## Build and run

```bash
# Build
dotnet build

# Run the dev version directly
dotnet run --project src/Lorex -- <args>

# Build and install the dev binary to your PATH
dotnet run install.cs

# Run tests
dotnet test
```

## This repo dogfoods Lorex

Once cloned, run `lorex init` and your AI agent will automatically read the `lorex-contributing` skill — which documents the internal architecture, file layout, and contribution workflow.

## Roadmap ideas

- Shared prompts and other reusable AI assets alongside skills
- Support for sub-agents and structured agent building blocks
- Expanded AI provider support and native integrations
- Improved methods for extracting reusable skills from AI sessions

## Reporting issues

Open an issue at [github.com/alirezanet/lorex/issues](https://github.com/alirezanet/lorex/issues).

## License

MIT
