#!/usr/bin/env python3
"""Fail closed unless the release directory contains the intended NuGet payload."""

from __future__ import annotations

import argparse
import re
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path, PurePosixPath


TOOL_ID = "maf-doctor"
ANALYZER_ID = "maf-doctor.Analyzers"
TOOL_TFMS = ("net8.0", "net9.0", "net10.0")
README = "NUGET_README.md"
ICON = "MAFDoctorIcon.png"


class VerificationError(RuntimeError):
    """A release artifact violates the expected package contract."""


def _local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def _child_text(parent: ET.Element, name: str) -> str | None:
    for child in parent:
        if _local_name(child.tag) == name:
            return (child.text or "").strip()
    return None


class Package:
    def __init__(self, path: Path) -> None:
        self.path = path
        try:
            self.archive = zipfile.ZipFile(path)
        except (OSError, zipfile.BadZipFile) as exc:
            raise VerificationError(f"{path.name}: invalid ZIP archive: {exc}") from exc

        names = self.archive.namelist()
        folded = [name.casefold() for name in names]
        if len(folded) != len(set(folded)):
            raise VerificationError(f"{path.name}: duplicate ZIP member name")

        for name in names:
            member = PurePosixPath(name)
            if "\\" in name or member.is_absolute() or ".." in member.parts:
                raise VerificationError(f"{path.name}: unsafe ZIP member {name!r}")

        self.names = set(names)

    def close(self) -> None:
        self.archive.close()

    def read(self, name: str) -> bytes:
        if name not in self.names:
            raise VerificationError(f"{self.path.name}: missing {name}")
        data = self.archive.read(name)
        if not data:
            raise VerificationError(f"{self.path.name}: {name} is empty")
        return data

    def metadata(self) -> ET.Element:
        nuspecs = [name for name in self.names if name.endswith(".nuspec") and "/" not in name]
        if len(nuspecs) != 1:
            raise VerificationError(
                f"{self.path.name}: expected exactly one root .nuspec, found {len(nuspecs)}"
            )
        try:
            root = ET.fromstring(self.read(nuspecs[0]))
        except ET.ParseError as exc:
            raise VerificationError(f"{self.path.name}: invalid nuspec XML: {exc}") from exc
        metadata = next((item for item in root.iter() if _local_name(item.tag) == "metadata"), None)
        if metadata is None:
            raise VerificationError(f"{self.path.name}: nuspec has no metadata element")
        return metadata


def _verify_identity(package: Package, package_id: str, version: str) -> ET.Element:
    metadata = package.metadata()
    actual_id = _child_text(metadata, "id")
    actual_version = _child_text(metadata, "version")
    if actual_id != package_id:
        raise VerificationError(
            f"{package.path.name}: package id is {actual_id!r}, expected {package_id!r}"
        )
    if actual_version != version:
        raise VerificationError(
            f"{package.path.name}: package version is {actual_version!r}, expected {version!r}"
        )
    return metadata


def _verify_common_content(package: Package, metadata: ET.Element) -> None:
    if _child_text(metadata, "readme") != README:
        raise VerificationError(f"{package.path.name}: nuspec readme must be {README}")
    if _child_text(metadata, "icon") != ICON:
        raise VerificationError(f"{package.path.name}: nuspec icon must be {ICON}")
    package.read(README)
    package.read(ICON)


def _verify_tool_settings(package: Package, path: str) -> None:
    try:
        root = ET.fromstring(package.read(path))
    except ET.ParseError as exc:
        raise VerificationError(f"{package.path.name}: invalid {path}: {exc}") from exc

    commands = [item for item in root.iter() if _local_name(item.tag) == "Command"]
    if len(commands) != 1:
        raise VerificationError(f"{package.path.name}: {path} must declare exactly one command")
    command = commands[0]
    expected = {"Name": TOOL_ID, "EntryPoint": f"{TOOL_ID}.dll", "Runner": "dotnet"}
    actual = {key: command.attrib.get(key) for key in expected}
    if actual != expected:
        raise VerificationError(
            f"{package.path.name}: {path} command is {actual!r}, expected {expected!r}"
        )


def _verify_tool(package: Package, version: str) -> None:
    metadata = _verify_identity(package, TOOL_ID, version)
    _verify_common_content(package, metadata)

    package_types = [
        item.attrib.get("name")
        for item in metadata.iter()
        if _local_name(item.tag) == "packageType"
    ]
    if package_types != ["DotnetTool"]:
        raise VerificationError(
            f"{package.path.name}: expected one DotnetTool package type, found {package_types!r}"
        )

    tfms = {
        match.group(1)
        for name in package.names
        if (match := re.match(r"^tools/([^/]+)/any/", name))
    }
    if tfms != set(TOOL_TFMS):
        raise VerificationError(
            f"{package.path.name}: tool TFMs are {sorted(tfms)!r}, expected {list(TOOL_TFMS)!r}"
        )
    for tfm in TOOL_TFMS:
        root = f"tools/{tfm}/any"
        package.read(f"{root}/{TOOL_ID}.dll")
        package.read(f"{root}/{TOOL_ID}.deps.json")
        package.read(f"{root}/{TOOL_ID}.runtimeconfig.json")
        _verify_tool_settings(package, f"{root}/DotnetToolSettings.xml")


def _verify_analyzer(package: Package, version: str) -> None:
    metadata = _verify_identity(package, ANALYZER_ID, version)
    _verify_common_content(package, metadata)
    expected_dll = f"analyzers/dotnet/cs/{ANALYZER_ID}.dll"
    package.read(expected_dll)
    analyzer_dlls = {
        name
        for name in package.names
        if name.startswith("analyzers/dotnet/cs/") and name.lower().endswith(".dll")
    }
    if analyzer_dlls != {expected_dll}:
        raise VerificationError(
            f"{package.path.name}: analyzer DLL payload is {sorted(analyzer_dlls)!r}, "
            f"expected only {expected_dll!r}"
        )


def _verify_tool_symbols(package: Package, version: str) -> None:
    _verify_identity(package, TOOL_ID, version)
    expected = {f"tools/{tfm}/any/{TOOL_ID}.pdb" for tfm in TOOL_TFMS}
    actual = {
        name
        for name in package.names
        if name.startswith("tools/") and name.lower().endswith(".pdb")
    }
    if actual != expected:
        raise VerificationError(
            f"{package.path.name}: symbol payload is {sorted(actual)!r}, expected {sorted(expected)!r}"
        )
    for name in expected:
        package.read(name)


def _verify_analyzer_symbols(package: Package, version: str) -> None:
    _verify_identity(package, ANALYZER_ID, version)
    expected = f"analyzers/dotnet/cs/netstandard2.0/{ANALYZER_ID}.pdb"
    package.read(expected)
    actual = {
        name
        for name in package.names
        if name.startswith("analyzers/dotnet/cs/") and name.lower().endswith(".pdb")
    }
    if actual != {expected}:
        raise VerificationError(
            f"{package.path.name}: analyzer symbol payload is {sorted(actual)!r}, "
            f"expected only {expected!r}"
        )


def verify_release_packages(dist: Path, version: str) -> None:
    if not dist.is_dir():
        raise VerificationError(f"release directory does not exist: {dist}")

    expected = {
        f"{TOOL_ID}.{version}.nupkg": _verify_tool,
        f"{ANALYZER_ID}.{version}.nupkg": _verify_analyzer,
        f"{TOOL_ID}.{version}.snupkg": _verify_tool_symbols,
        f"{ANALYZER_ID}.{version}.snupkg": _verify_analyzer_symbols,
    }
    actual = {
        path.name
        for path in dist.iterdir()
        if path.is_file() and path.name.lower().endswith((".nupkg", ".snupkg"))
    }
    if actual != set(expected):
        missing = sorted(set(expected) - actual)
        extra = sorted(actual - set(expected))
        raise VerificationError(
            f"release artifact set mismatch; missing={missing!r}, unexpected={extra!r}"
        )

    for filename, verifier in expected.items():
        package = Package(dist / filename)
        try:
            verifier(package, version)
        finally:
            package.close()


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dist", type=Path, required=True, help="Directory containing packed artifacts")
    parser.add_argument("--version", required=True, help="Resolved release version")
    args = parser.parse_args(argv)

    try:
        verify_release_packages(args.dist, args.version)
    except VerificationError as exc:
        print(f"release package verification failed: {exc}", file=sys.stderr)
        return 1
    print(f"Verified release package set for {args.version}: two packages and two symbol packages")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
