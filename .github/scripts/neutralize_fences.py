#!/usr/bin/env python3
"""SEC-18: break triple-backtick runs in captured tool output before it is embedded
inside a ```-fenced block of a PR comment.

Reads stdin, writes stdout with every ``` replaced by three backticks separated by a
zero-width space (U+200B) — invisible to a human reader, but no longer a fence
terminator to the markdown parser, so adversarial captured output (a crafted registry
entry, a scanner finding) can't escape the fence and inject live markdown.

Mirrors src/maf-autopilot/Data/RegistryService.NeutralizeFences and the C#
LlmFencing conventions. Shared by every workflow that fences captured output into a
comment (maf-ai-fill-verify, mcp-scanner).
"""
import sys

_ZWSP = chr(0x200B)
# Binary UTF-8 IO so the zero-width space round-trips regardless of the platform's
# default stdio encoding (Windows cp1252 can't encode U+200B; Ubuntu CI is UTF-8).
_data = sys.stdin.buffer.read().decode("utf-8", errors="replace")
sys.stdout.buffer.write(_data.replace("```", f"`{_ZWSP}`{_ZWSP}`").encode("utf-8"))
