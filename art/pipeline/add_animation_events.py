"""Repair/add provisional animation markers through the running Unity Editor.

ModelImporter event time is normalized (0..1), unlike AnimationClip event seconds.
The authoritative marker table lives in ConGameAnimationEventRepair.cs.
This entry point deliberately never writes .meta YAML.
"""
import argparse
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    if args.dry_run:
        print("Would repair/add normalized importer events using ConGame.Editor.ConGameAnimationEventRepair.Repair() in the connected Unity Editor.")
        return 0
    result = subprocess.run([
        "unity", "command", "eval", "--caller", "plugin", "--skill", "unity-cli",
        "--project-path", str(root), "--code",
        "ConGame.Editor.ConGameAnimationEventRepair.Repair();", "--format", "json"
    ], check=False)
    return result.returncode


if __name__ == "__main__":
    raise SystemExit(main())
