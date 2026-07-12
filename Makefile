.PHONY: build clean format format-check help lint restore run test test-ci watch

.DEFAULT_GOAL := help

SOLUTION := SharpVision.slnx
SHOWCASE := src/SharpVision.Showcase/SharpVision.Showcase.csproj

help:
	@echo "SharpVision - Available Make Targets"
	@echo "===================================="
	@echo "  make restore       Restore .NET and Node.js dependencies"
	@echo "  make build         Build all projects in Release mode"
	@echo "  make test          Run all tests with timeout protection"
	@echo "  make test-ci       Run tests with CI reports"
	@echo "  make run           Run the showcase"
	@echo "  make watch         Run the showcase in watch mode"
	@echo "  make lint          Check C#, Markdown, and documentation links"
	@echo "  make format        Format C# and Markdown"
	@echo "  make format-check  Check formatting without changing files"
	@echo "  make clean         Clean .NET and test output"

restore:
	@echo "📦 Restoring dependencies..."
	@dotnet restore $(SOLUTION)
	@npm ci
	@echo "✅ Dependencies restored."

build: restore
	@echo "🔨 Building SharpVision..."
	@dotnet build $(SOLUTION) --configuration Release --no-restore
	@echo "✅ Build complete."

run:
	@dotnet run --project $(SHOWCASE)

test: build
	@echo "🧪 Running tests..."
	@dotnet test --solution $(SOLUTION) --configuration Release --no-build --minimum-expected-tests 3 --timeout 900s
	@echo "✅ Tests complete."

test-ci:
	@dotnet test --solution $(SOLUTION) --configuration $${CONFIGURATION:-Release} --no-build --minimum-expected-tests 3 --timeout 900s --report-xunit-trx

lint: restore
	@echo "🔍 Checking source and documentation..."
	@dotnet format $(SOLUTION) --verify-no-changes --no-restore --verbosity diagnostic
	@npm run lint:csharp-types
	@npm run lint:extern
	@npm run format:check
	@npm run lint:markdown
	@npm run lint:links
	@npm run test:docs
	@echo "✅ All lint checks passed."

format: restore
	@echo "✨ Formatting source and documentation..."
	@dotnet format $(SOLUTION) --no-restore
	@npm run format
	@echo "✅ Formatting complete."

format-check: restore
	@dotnet format $(SOLUTION) --verify-no-changes --no-restore
	@npm run format:check

watch:
	@dotnet watch --project $(SHOWCASE) run

clean:
	@dotnet clean $(SOLUTION) --verbosity minimal
	@rm -rf TestResults
