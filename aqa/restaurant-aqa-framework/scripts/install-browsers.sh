#!/usr/bin/env bash
# Встановлює Chromium тієї версії, яку очікує Microsoft.Playwright з проєкту.
# Запускати після dotnet build. Аргумент: конфігурація збірки (Debug за замовчуванням).
set -euo pipefail
cd "$(dirname "$0")/.."

out="bin/${1:-Debug}/net10.0"
if [[ ! -d "$out/.playwright" ]]; then
  echo "Build the project first: dotnet build" >&2
  exit 1
fi

if command -v pwsh >/dev/null 2>&1; then
  pwsh "$out/playwright.ps1" install chromium
  exit 0
fi

# без PowerShell викликаємо той самий драйвер напряму
case "$(uname -s)-$(uname -m)" in
  Darwin-arm64) platform=darwin-arm64 ;;
  Darwin-x86_64) platform=darwin-x64 ;;
  Linux-x86_64) platform=linux-x64 ;;
  Linux-aarch64) platform=linux-arm64 ;;
  *) echo "Unsupported platform, use: pwsh $out/playwright.ps1 install chromium" >&2; exit 1 ;;
esac
"$out/.playwright/node/$platform/node" "$out/.playwright/package/cli.js" install chromium
