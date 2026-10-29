"""Короткий підсумок прогону з TRX-файлу у форматі Markdown (для $GITHUB_STEP_SUMMARY)."""
import glob
import sys
import xml.etree.ElementTree as ET

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def main(pattern: str) -> None:
    files = sorted(glob.glob(pattern, recursive=True))
    if not files:
        print("### AQA tests\n\nNo TRX file found, the test run did not finish.")
        return

    root = ET.parse(files[-1]).getroot()
    counters = root.find("t:ResultSummary/t:Counters", NS)
    total = int(counters.get("total", 0))
    passed = int(counters.get("passed", 0))
    failed = int(counters.get("failed", 0))
    skipped = total - passed - failed

    print("### AQA tests\n")
    print("| Total | Passed | Failed | Skipped |")
    print("|---|---|---|---|")
    print(f"| {total} | {passed} | {failed} | {skipped} |\n")

    failures = []
    for result in root.iterfind("t:Results/t:UnitTestResult", NS):
        if result.get("outcome") != "Failed":
            continue
        message = result.findtext("t:Output/t:ErrorInfo/t:Message", default="", namespaces=NS)
        first_line = next((line.strip() for line in message.splitlines() if line.strip()), "")
        failures.append((result.get("testName", "?"), first_line.replace("|", "\\|")[:200]))

    if failures:
        print("<details open><summary>Failed scenarios</summary>\n")
        print("| Scenario | Reason |")
        print("|---|---|")
        for name, reason in failures:
            print(f"| {name} | {reason} |")
        print("\n</details>")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "TestResults/**/*.trx")
