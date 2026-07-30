.PHONY: build clean format format-check help lint restore run test test-binding-coverage test-ci test-tty watch

.DEFAULT_GOAL := help

SOLUTION := SharpVision.slnx
SHOWCASE := examples/Showcase/SharpVision.Showcase.csproj

help:
	@echo "SharpVision - Available Make Targets"
	@echo "===================================="
	@echo "  make restore       Restore .NET and Node.js dependencies"
	@echo "  make build         Build all projects in Release mode"
	@echo "  make test          Run all tests with timeout protection"
	@echo "  make test-binding-coverage  Gate binding line and branch coverage"
	@echo "  make test-ci       Run tests with CI reports"
	@echo "  make test-tty      Run controlling-terminal-gated Unix console host tests (Linux/macOS only)"
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
	@dotnet build $(SOLUTION) --configuration $${CONFIGURATION:-Release} --no-restore
	@echo "✅ Build complete."

run:
	@dotnet run --project $(SHOWCASE)

test: build
	@echo "🧪 Running tests..."
	@dotnet test --solution $(SOLUTION) --configuration Release --no-build --minimum-expected-tests 3 --timeout 900s
	@npm run test:docs
	@echo "✅ Tests complete."

test-ci: build
	@dotnet test --project tests/SharpVision.Terminal.Tests --configuration $${CONFIGURATION:-Release} --no-build --minimum-expected-tests 3 --timeout 900s --coverage --coverage-output-format cobertura --report-xunit-trx
	@dotnet test --project tests/SharpVision.Tests --configuration $${CONFIGURATION:-Release} --no-build --minimum-expected-tests 3 --timeout 900s --coverage --coverage-settings tests/SharpVision.Tests/coverage.config --coverage-output-format cobertura --report-xunit-trx --parallel none
	@dotnet test --project tests/SharpVision.Compatibility.Tests --configuration $${CONFIGURATION:-Release} --no-build --minimum-expected-tests 1 --timeout 300s --report-xunit-trx
	@node scripts/validate-control-coverage.mjs --results tests/SharpVision.Tests/bin/$${CONFIGURATION:-Release}/net10.0/TestResults --minimum 0.85

test-tty: build
	@echo "🧪 Running controlling-terminal-gated Unix console host tests..."
	@if [ "$$(uname -s)" = "Darwin" ]; then \
		script -q /dev/null dotnet test --project tests/SharpVision.Terminal.Tests --configuration $${CONFIGURATION:-Release} --no-build --minimum-expected-tests 2 --filter-query "/*/*/UnixConsoleHostTests/*"; \
	else \
		script -qec "dotnet test --project tests/SharpVision.Terminal.Tests --configuration $${CONFIGURATION:-Release} --no-build --minimum-expected-tests 2 --filter-query '/*/*/UnixConsoleHostTests/*'" /dev/null; \
	fi
	@echo "✅ Controlling-terminal Unix host tests complete."

test-binding-coverage: build
	@echo "🧪 Running binding coverage gate..."
	@mkdir -p TestResults/binding
	@dotnet test --project tests/SharpVision.Tests --configuration Release --no-build --filter-namespace "SharpVision.Tests.DataBinding*" --coverage --coverage-output-format cobertura --coverage-output "$$(pwd)/TestResults/binding/coverage.cobertura.xml" --timeout 180s
	@node scripts/validate-binding-coverage.mjs TestResults/binding/coverage.cobertura.xml 95 90
	@echo "✅ Binding coverage complete."

lint: restore
	@echo "🔍 Checking source and documentation..."
	@dotnet format $(SOLUTION) --verify-no-changes --no-restore --verbosity diagnostic
	@npm run format:check
	@npm run lint:markdown
	@npm run lint:links
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
