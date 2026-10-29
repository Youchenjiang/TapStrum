import sys
import unittest
from pathlib import Path
from unittest.mock import MagicMock, patch

for _dir in ("tools", "scripts"):
    _p = str(Path(__file__).resolve().parents[1] / _dir)
    if _p not in sys.path:
        sys.path.insert(0, _p)
import branch_creation
from process_guard import executable
import pr_tool


class BranchCreationTests(unittest.TestCase):
    def test_rejects_invalid_branch_name(self):
        with self.assertRaises(ValueError) as ctx:
            branch_creation.create_branch("release/v1.4.0.0")
        self.assertIn("invalid branch name", str(ctx.exception))
        self.assertIn("invalid branch prefix 'release'", str(ctx.exception))

    def test_rejects_base_starting_with_dash(self):
        with self.assertRaises(ValueError) as ctx:
            branch_creation.create_branch("chore/clean-tests", base="--force")
        self.assertIn("invalid base ref", str(ctx.exception))

    def test_rejects_uncommitted_worktree(self):
        fake_status = MagicMock(stdout=" M some_file.cs\n")
        with patch("branch_creation.invoke", return_value=fake_status):
            with self.assertRaises(RuntimeError) as ctx:
                branch_creation.create_branch("chore/clean-tests")
            self.assertIn("working tree has uncommitted changes", str(ctx.exception))

    def test_creates_compliant_branch_successfully(self):
        fake_status = MagicMock(stdout="")
        fake_proc = MagicMock(returncode=0, stdout="", stderr="")
        with patch("branch_creation.invoke", return_value=fake_status), \
             patch("branch_creation.subprocess.run", return_value=fake_proc) as mock_run:
            res = branch_creation.create_branch("chore/release-preparation", base="main", runner=mock_run)
            self.assertEqual(res["branch"], "chore/release-preparation")
            self.assertEqual(res["status"], "created")
            mock_run.assert_called_once_with(
                [executable("git"), "checkout", "-b", "chore/release-preparation", "main"],
                text=True, capture_output=True, check=False
            )

    def test_pr_tool_branch_cli_integration(self):
        fake_status = MagicMock(stdout="")
        fake_proc = MagicMock(returncode=0, stdout="", stderr="")
        with patch("branch_creation.invoke", return_value=fake_status), \
             patch("branch_creation.subprocess.run", return_value=fake_proc) as mock_run:
            code = pr_tool.main(["branch", "chore/release-preparation", "--base", "main"])
            self.assertEqual(code, 0)
            mock_run.assert_called_once_with(
                [executable("git"), "checkout", "-b", "chore/release-preparation", "main"],
                text=True, capture_output=True, check=False
            )
