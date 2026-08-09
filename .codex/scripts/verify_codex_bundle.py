#!/usr/bin/env python3
from __future__ import annotations

import re
import sys
import tomllib
from pathlib import Path

REQUIRED_AGENT_KEYS = {"name", "description", "developer_instructions"}
ALLOWED_SANDBOX_MODES = {"read-only", "workspace-write", "danger-full-access"}
EXPECTED_REFS = [
    ".agents/skills/banking-payment-gateway/references/architecture-baseline.md",
    ".agents/skills/banking-payment-gateway/references/domain-decisions.md",
    ".agents/skills/banking-payment-gateway/references/coding-and-verification.md",
    ".agents/skills/banking-payment-gateway/references/source-map.md",
]


def find_repo_root() -> Path:
    script = Path(__file__).resolve()
    # <root>/.codex/scripts/verify_codex_bundle.py
    return script.parents[2]


def check(condition: bool, message: str, errors: list[str]) -> None:
    if condition:
        print(f"PASS: {message}")
    else:
        print(f"FAIL: {message}")
        errors.append(message)


def parse_toml(path: Path, errors: list[str]) -> dict:
    try:
        with path.open("rb") as fh:
            data = tomllib.load(fh)
        print(f"PASS: TOML parses: {path.relative_to(find_repo_root())}")
        return data
    except Exception as exc:  # noqa: BLE001 - verifier must report all syntax failures
        msg = f"TOML parse failed for {path}: {exc}"
        print(f"FAIL: {msg}")
        errors.append(msg)
        return {}


def parse_frontmatter(text: str) -> dict[str, str]:
    if not text.startswith("---\n"):
        return {}
    end = text.find("\n---\n", 4)
    if end < 0:
        return {}
    result: dict[str, str] = {}
    for line in text[4:end].splitlines():
        if ":" in line:
            key, value = line.split(":", 1)
            result[key.strip()] = value.strip()
    return result


def main() -> int:
    root = find_repo_root()
    errors: list[str] = []

    agents_md = root / "AGENTS.md"
    check(agents_md.is_file(), "root AGENTS.md exists", errors)
    if agents_md.is_file():
        size = agents_md.stat().st_size
        check(size < 32 * 1024, f"AGENTS.md is below 32 KiB ({size} bytes)", errors)

    pointer = root / ".codex" / "agent.md"
    check(pointer.is_file(), ".codex/agent.md compatibility pointer exists", errors)
    if pointer.is_file():
        check("../AGENTS.md" in pointer.read_text(encoding="utf-8"), "compatibility pointer references ../AGENTS.md", errors)

    config = root / ".codex" / "config.toml"
    check(config.is_file(), ".codex/config.toml exists", errors)
    if config.is_file():
        data = parse_toml(config, errors)
        check(isinstance(data.get("agents"), dict), "config.toml contains [agents] table", errors)

    agent_dir = root / ".codex" / "agents"
    agent_files = sorted(agent_dir.glob("*.toml"))
    check(len(agent_files) >= 5, f"at least five custom agent files exist ({len(agent_files)})", errors)
    seen_names: set[str] = set()
    for path in agent_files:
        data = parse_toml(path, errors)
        missing = REQUIRED_AGENT_KEYS - data.keys()
        check(not missing, f"{path.name} has required keys", errors)
        name = data.get("name")
        if isinstance(name, str):
            check(name not in seen_names, f"custom agent name is unique: {name}", errors)
            seen_names.add(name)
        mode = data.get("sandbox_mode")
        check(mode in ALLOWED_SANDBOX_MODES, f"{path.name} has recognized sandbox_mode", errors)

    skill = root / ".agents" / "skills" / "banking-payment-gateway" / "SKILL.md"
    check(skill.is_file(), "repository skill SKILL.md exists in .agents/skills", errors)
    if skill.is_file():
        text = skill.read_text(encoding="utf-8")
        metadata = parse_frontmatter(text)
        check(bool(metadata.get("name")), "SKILL.md frontmatter has name", errors)
        check(bool(metadata.get("description")), "SKILL.md frontmatter has description", errors)

    for relative in EXPECTED_REFS:
        check((root / relative).is_file(), f"reference exists: {relative}", errors)

    # A conservative scan for obvious committed credential assignments.
    secret_assignment = re.compile(
        r"(?im)^\s*(password|client_secret|api_key|private_key|jwt_secret)\s*[=:]\s*['\"]?(?!<|example|change-me|\$\{)[^#\n]{8,}"
    )
    scanned = [agents_md, config, pointer, skill, *agent_files]
    matches: list[str] = []
    for path in scanned:
        if path.is_file() and secret_assignment.search(path.read_text(encoding="utf-8")):
            matches.append(str(path.relative_to(root)))
    check(not matches, f"no obvious credential assignments in active Codex files{': ' + ', '.join(matches) if matches else ''}", errors)

    report = root / ".codex" / "verification-result.txt"
    status = "PASS" if not errors else "FAIL"
    report.write_text(
        f"Codex bundle structural verification: {status}\n"
        f"Repository root: {root}\n"
        f"Errors: {len(errors)}\n"
        + ("\n".join(f"- {e}" for e in errors) + "\n" if errors else ""),
        encoding="utf-8",
    )
    print(f"\nRESULT: {status}")
    print(f"Report: {report}")
    return 0 if not errors else 1


if __name__ == "__main__":
    sys.exit(main())
