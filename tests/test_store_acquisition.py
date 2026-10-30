import pathlib
import subprocess
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[1]


class StoreAcquisitionTests(unittest.TestCase):
    def test_vendored_store_helper_has_pinned_mpl_provenance(self):
        vendor = ROOT / "third_party" / "microsoft-store-package-downloader"
        provenance = (vendor / "PROVENANCE.md").read_text(encoding="utf-8")
        self.assertIn("d1c508cc458223ec7ae684e685625c5b7cb62286", provenance)
        self.assertIn("Mozilla Public License 2.0", provenance)
        self.assertTrue((vendor / "LICENSE").is_file())
        self.assertTrue((vendor / "references" / "storelib" / "LICENSE").is_file())
        self.assertTrue((vendor / "references" / "storelib" / "FE3Handler.cs").is_file())
        self.assertTrue((vendor / "scripts" / "download-store-package.ps1").is_file())

    def test_production_acquisition_uses_only_local_vendored_helper(self):
        wrapper = (ROOT / "scripts" / "acquire_store_package.ps1").read_text(encoding="utf-8")
        self.assertIn("third_party\\microsoft-store-package-downloader", wrapper)
        self.assertNotIn("store.rg-adguard.net", wrapper)
        self.assertNotIn("git clone", wrapper.lower())
        self.assertNotIn("& powershell -NoProfile", wrapper)

    def test_workflow_no_longer_requires_self_hosted_runner(self):
        workflow = (ROOT / ".github" / "workflows" / "store-release-enrichment.yml").read_text(encoding="utf-8")
        self.assertIn("runs-on: windows-latest", workflow)
        self.assertNotIn("runs-on: [self-hosted", workflow)
        self.assertNotIn("tapstrum-store]", workflow)

    def test_acquisition_safely_handles_strict_mode_property_access(self):
        script_path = str(ROOT / "scripts" / "acquire_store_package.ps1").replace("\\", "/")
        ps_test = """
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptPath = '{SCRIPT_PATH}'
$tokens = $null
$errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$errors)
if ($null -ne $errors -and $errors.Count -gt 0) {
    throw "Syntax error in script"
}

$memberExprs = $ast.FindAll({ param($node)
    $node -is [System.Management.Automation.Language.MemberExpressionAst] -and
    $node.Expression -is [System.Management.Automation.Language.VariableExpressionAst] -and
    $node.Expression.VariablePath.UserPath -eq 'selected'
}, $true)

foreach ($m in $memberExprs) {
    if ($m.Member.Value -in @('Sha1', 'Sha256')) {
        throw "Direct property access $($m.Extent.Text) found on `$selected under StrictMode"
    }
}

$selected = [pscustomobject]@{
    FileName = 'test.msix'
    SourceUrl = 'https://delivery.mp.microsoft.com/test'
    Sha256 = 'AABBCC'
}

$expectedSha256 = if ($selected.PSObject.Properties['Sha256']) { [string]$selected.PSObject.Properties['Sha256'].Value } else { '' }
$expectedSha1 = if ($selected.PSObject.Properties['Sha1']) { [string]$selected.PSObject.Properties['Sha1'].Value } else { '' }

if ($expectedSha256 -ne 'AABBCC' -or $expectedSha1 -ne '') {
    throw 'Failed extracting properties under StrictMode'
}
""".replace("{SCRIPT_PATH}", script_path)
        proc = subprocess.run(
            ["powershell", "-NoProfile", "-NonInteractive", "-Command", ps_test],
            capture_output=True,
            text=True,
            check=False,
        )
        self.assertEqual(0, proc.returncode, f"PowerShell strict mode check failed: {proc.stderr}")

    def test_sonar_excludes_only_vendored_store_source(self):
        sonar = (ROOT / ".sonarcloud.properties").read_text(encoding="utf-8").strip()
        self.assertEqual(
            "sonar.exclusions=third_party/microsoft-store-package-downloader/**",
            sonar,
        )


if __name__ == "__main__":
    unittest.main()
