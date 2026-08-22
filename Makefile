.PHONY: bootstrap-packages build clean docs-samples format format-check help lint restore run test test-ci test-tty watch

.DEFAULT_GOAL := help

SOLUTION := SharpVision.slnx
SHOWCASE := examples/Showcase/SharpVision.Showcase.csproj
BOOTSTRAP_PACKAGES := artifacts/bootstrap-packages
RESTORE_PACKAGES := artifacts/restore-packages
# Package sources (nuget.org, the bootstrap-packages folder) live in NuGet.Config, not on any
# `dotnet restore --source` command line -- see that file's comment for why.

# Coverage instrumentation mode. Static instrumentation rewrites each assembly and prefixes every
# basic block with a store into a memory-mapped probe buffer under the temporary directory. On
# Linux the coverage host rewrites that buffer in place while the test host still has it mapped,
# and a probe that stores into the unbacked page kills the process with an AccessViolationException
# blamed on whatever managed method was running (microsoft/codecoverage#232). Dynamic
# instrumentation collects through the profiler and creates no such buffer, so the fault cannot
# occur. It is used only on Linux: the profiler ships for a limited set of runtime identifiers and
# macOS arm64 is not one of them, where it would silently report no coverage at all.
COVERAGE_SETTINGS := $(if $(filter Linux,$(shell uname -s)),tests/coverage.dynamic.config,tests/coverage.static.config)

help:
	@echo "SharpVision - Available Make Targets"
	@echo "===================================="
	@echo "  make restore       Restore .NET and Node.js dependencies"
	@echo "  make build         Build all projects in Release mode"
	@echo "  make test          Run all tests with timeout protection"
	@echo "  make test-ci       Run tests with CI reports"
	@echo "  make test-tty      Run controlling-terminal-gated Unix console host tests (Linux/macOS only)"
	@echo "  make run           Run the showcase in Release mode"
	@echo "  make watch         Run the showcase in watch mode"
	@echo "  make docs-samples  Compile every documentation C# sample"
	@echo "  make lint          Check C#, Markdown, and documentation links"
	@echo "  make format        Format C# and Markdown"
	@echo "  make format-check  Check formatting without changing files"
	@echo "  make clean         Clean .NET and test output"

bootstrap-packages:
	@echo "📦 Bootstrapping SharpVision packages..."
	@rm -rf $(BOOTSTRAP_PACKAGES) $(RESTORE_PACKAGES)
	@mkdir -p $(BOOTSTRAP_PACKAGES)
	@dotnet restore src/SharpVision.Terminal/SharpVision.Terminal.csproj
	@dotnet restore src/SharpVision/SharpVision.csproj
	@dotnet build src/SharpVision.Terminal/SharpVision.Terminal.csproj --configuration Release --no-restore --target Rebuild
	@dotnet build src/SharpVision/SharpVision.csproj --configuration Release --no-restore --target Rebuild
	@dotnet pack src/SharpVision.Terminal/SharpVision.Terminal.csproj --configuration Release --no-build --no-restore -p:IsPackable=true --output $(BOOTSTRAP_PACKAGES)
	@dotnet pack src/SharpVision/SharpVision.csproj --configuration Release --no-build --no-restore --output $(BOOTSTRAP_PACKAGES)
	@echo "✅ Bootstrap packages ready."

restore: bootstrap-packages
	@echo "📦 Restoring dependencies..."
	@dotnet restore $(SOLUTION) --packages $(RESTORE_PACKAGES)
	@npm ci
	@echo "✅ Dependencies restored."

build: restore
	@echo "🔨 Building SharpVision..."
	@dotnet build $(SOLUTION) --configuration $${CONFIGURATION:-Release} --no-restore
	@echo "✅ Build complete."

run:
	@dotnet run --project $(SHOWCASE) --configuration $${CONFIGURATION:-Release}

test: build
	@echo "🧪 Running tests..."
	@dotnet test --solution $(SOLUTION) --configuration Release --no-build --minimum-expected-tests 4386 --timeout 300s
	@npm run test:docs
	@echo "✅ Tests complete."

test-ci: build
	@dotnet test --project tests/SharpVision.Terminal.Tests --configuration $${CONFIGURATION:-Release} --no-build --minimum-expected-tests 1716 --timeout $${TEST_TIMEOUT:-900s} --coverage --coverage-settings $(COVERAGE_SETTINGS) --coverage-output-format cobertura --report-xunit-trx
	@dotnet test --project tests/SharpVision.Tests --configuration $${CONFIGURATION:-Release} --no-build --minimum-expected-tests 2670 --timeout $${TEST_TIMEOUT:-900s} --coverage --coverage-settings $(COVERAGE_SETTINGS) --coverage-output-format cobertura --report-xunit-trx --parallel none
	@dotnet test --project tests/SharpVision.Document.Tests --configuration $${CONFIGURATION:-Release} --no-build --minimum-expected-tests 12 --timeout $${TEST_TIMEOUT:-900s} --coverage --coverage-settings $(COVERAGE_SETTINGS) --coverage-output-format cobertura --report-xunit-trx
	@dotnet test --project tests/SharpVision.Compatibility.Tests --configuration $${CONFIGURATION:-Release} --no-build --minimum-expected-tests 4 --timeout 300s --report-xunit-trx
	@echo "⏱️  Re-running performance gates without instrumentation..."
	@dotnet test --project tests/SharpVision.Tests --configuration $${CONFIGURATION:-Release} --no-build --minimum-expected-tests 24 --timeout 300s --filter-query "/*/SharpVision.Tests.Performance/*/*"
	@node scripts/validate-control-coverage.mjs --results tests/SharpVision.Tests/bin/$${CONFIGURATION:-Release}/net10.0/TestResults --minimum 0.85
	@npm run test:docs

test-tty: build
	@echo "🧪 Running controlling-terminal-gated Unix console host tests..."
	@if [ "$$(uname -s)" = "Darwin" ]; then \
		script -q /dev/null dotnet test --project tests/SharpVision.Terminal.Tests --configuration $${CONFIGURATION:-Release} --no-build --minimum-expected-tests 2 --filter-query "/*/*/UnixConsoleHostTests/*"; \
	else \
		script -qec "dotnet test --project tests/SharpVision.Terminal.Tests --configuration $${CONFIGURATION:-Release} --no-build --minimum-expected-tests 2 --filter-query '/*/*/UnixConsoleHostTests/*'" /dev/null; \
	fi
	@echo "✅ Controlling-terminal Unix host tests complete."

docs-samples:
	@echo "📖 Compiling documentation C# samples..."
	@dotnet build src/SharpVision/SharpVision.csproj --configuration Release --no-restore
	@dotnet build src/SharpVision.Document/SharpVision.Document.csproj --configuration Release --no-restore
	@dotnet build src/SharpVision.FigletFonts/SharpVision.FigletFonts.csproj --configuration Release --no-restore
	@npm run lint:docs-samples
	@echo "✅ Documentation samples compile."

lint: restore docs-samples
	@echo "🔍 Checking source and documentation..."
	@dotnet format $(SOLUTION) --verify-no-changes --no-restore --verbosity diagnostic
	@npm run format:check
	@npm run check:unicode
	@npm run check:figlet
	@npm run lint:markdown
	@npm run lint:links
	@npm run lint:doc-content
	@npm run lint:control-doc-structure
	@npm run lint:control-images
	@npm run lint:test-names
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
