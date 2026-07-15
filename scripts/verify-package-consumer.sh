#!/usr/bin/env bash

set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
temporary="$(mktemp -d "${TMPDIR:-/tmp}/sharpvision-package-consumer.XXXXXX")"
trap 'rm -rf "$temporary"' EXIT

version="0.0.0-packageproof"
feed="$temporary/feed"
consumer="$temporary/consumer"
packages="$temporary/packages"
artifacts="$temporary/artifacts"

mkdir -p "$feed" "$consumer/PackageSpecimens" "$packages" "$artifacts"
cp -R "$root/tests/SharpVision.PackageConsumer/." "$consumer/"
cp "$root/tests/SharpVision.Consumer.Tests/PackageSpecimens/"*.cs "$consumer/PackageSpecimens/"

dotnet pack "$root/src/SharpVision.Terminal/SharpVision.Terminal.csproj" \
  --configuration Release \
  --artifacts-path "$artifacts" \
  --output "$feed" \
  -p:PackageVersion="$version" \
  -p:ContinuousIntegrationBuild=true \
  --disable-build-servers

dotnet pack "$root/src/SharpVision/SharpVision.csproj" \
  --configuration Release \
  --artifacts-path "$artifacts" \
  --output "$feed" \
  -p:PackageVersion="$version" \
  -p:ContinuousIntegrationBuild=true \
  --disable-build-servers

NUGET_PACKAGES="$packages" dotnet restore "$consumer/SharpVision.PackageConsumer.csproj" \
  --configfile "$consumer/NuGet.config" \
  --no-cache \
  --force-evaluate

node "$root/scripts/validate-package-assets.mjs" \
  "$consumer/obj/project.assets.json" \
  "$version"

for package in SharpVision SharpVision.Terminal; do
  archive="$feed/$package.$version.nupkg"
  test -f "$archive"
  unzip -Z1 "$archive" | grep -Fx "lib/net10.0/$package.dll" >/dev/null
  unzip -Z1 "$archive" | grep -Fx "lib/net10.0/$package.xml" >/dev/null
  unzip -Z1 "$archive" | grep -Fx "README.md" >/dev/null
done

NUGET_PACKAGES="$packages" dotnet build "$consumer/SharpVision.PackageConsumer.csproj" \
  --configuration Release \
  --no-restore \
  --warnaserror

NUGET_PACKAGES="$packages" dotnet run \
  --project "$consumer/SharpVision.PackageConsumer.csproj" \
  --configuration Release \
  --no-build \
  --no-restore
