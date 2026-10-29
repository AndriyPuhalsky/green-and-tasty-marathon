#!/usr/bin/env bash
# Збирає HTML-звіт Allure з TestResults/allure-results у TestResults/allure-report.
# Потрібна Java 8+. Якщо Allure CLI не встановлено, береться з npm через npx.
set -euo pipefail
cd "$(dirname "$0")/.."

results=TestResults/allure-results
report=TestResults/allure-report

if command -v allure >/dev/null 2>&1; then
  allure generate "$results" --clean --single-file -o "$report"
else
  npx --yes allure-commandline@2.46.1 generate "$results" --clean --single-file -o "$report"
fi
echo "Report: $report/index.html"
