"""Guarded branch creation enforcing branch naming policies."""

import subprocess
from branch_policy import validate_branch_name
from process_guard import executable, invoke


def create_branch(branch, *, base=None, runner=None):
    """Validate and safely create a new compliant git branch."""
    if runner is None:
        runner = subprocess.run

    problems = validate_branch_name(branch)
    if problems:
        formatted = "\n  - ".join(problems)
        raise ValueError(f"invalid branch name '{branch}':\n  - {formatted}")

    # Check worktree cleanliness
    status = invoke(["git", "status", "--porcelain"])
    if status.stdout.strip():
        raise RuntimeError("working tree has uncommitted changes; commit or stash before creating a branch")

    cmd = [executable("git"), "checkout", "-b", branch]
    if base:
        cmd.append(base)

    proc = runner(cmd, text=True, capture_output=True, check=False)
    if proc.returncode != 0:
        err = proc.stderr.strip() or proc.stdout.strip()
        raise RuntimeError(f"git checkout -b failed ({proc.returncode}): {err}")

    return {"branch": branch, "base": base or "HEAD", "status": "created"}
