"""Tests for the fail-closed release package verifier."""

from __future__ import annotations

import importlib.util
import zipfile
from pathlib import Path

import pytest


SCRIPT = Path(__file__).resolve().parents[1] / "verify_release_packages.py"
SPEC = importlib.util.spec_from_file_location("verify_release_packages", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)

VERSION = "1.15.0"


def _nuspec(package_id: str, version: str, *, content: bool) -> bytes:
    readme = "<readme>NUGET_README.md</readme><icon>MAFDoctorIcon.png</icon>" if content else ""
    package_type = (
        '<packageTypes><packageType name="DotnetTool" /></packageTypes>'
        if package_id == "maf-doctor"
        else ""
    )
    return (
        '<?xml version="1.0"?><package><metadata>'
        f"<id>{package_id}</id><version>{version}</version>{readme}{package_type}"
        "</metadata></package>"
    ).encode()


def _write_package(path: Path, package_id: str, members: dict[str, bytes], *, content: bool) -> None:
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr(f"{package_id}.nuspec", _nuspec(package_id, VERSION, content=content))
        for name, data in members.items():
            archive.writestr(name, data)


def _valid_set(dist: Path) -> None:
    tool_members = {"NUGET_README.md": b"readme", "MAFDoctorIcon.png": b"icon"}
    tool_symbols: dict[str, bytes] = {}
    settings = (
        b'<DotNetCliTool Version="1"><Commands><Command Name="maf-doctor" '
        b'EntryPoint="maf-doctor.dll" Runner="dotnet" /></Commands></DotNetCliTool>'
    )
    for tfm in MODULE.TOOL_TFMS:
        root = f"tools/{tfm}/any"
        tool_members[f"{root}/maf-doctor.dll"] = b"dll"
        tool_members[f"{root}/maf-doctor.deps.json"] = b"{}"
        tool_members[f"{root}/maf-doctor.runtimeconfig.json"] = b"{}"
        tool_members[f"{root}/DotnetToolSettings.xml"] = settings
        tool_symbols[f"{root}/maf-doctor.pdb"] = b"pdb"

    _write_package(dist / f"maf-doctor.{VERSION}.nupkg", "maf-doctor", tool_members, content=True)
    _write_package(
        dist / f"maf-doctor.Analyzers.{VERSION}.nupkg",
        "maf-doctor.Analyzers",
        {
            "NUGET_README.md": b"readme",
            "MAFDoctorIcon.png": b"icon",
            "analyzers/dotnet/cs/maf-doctor.Analyzers.dll": b"dll",
        },
        content=True,
    )
    _write_package(
        dist / f"maf-doctor.{VERSION}.snupkg", "maf-doctor", tool_symbols, content=False
    )
    _write_package(
        dist / f"maf-doctor.Analyzers.{VERSION}.snupkg",
        "maf-doctor.Analyzers",
        {"analyzers/dotnet/cs/netstandard2.0/maf-doctor.Analyzers.pdb": b"pdb"},
        content=False,
    )


def test_accepts_exact_release_payload(tmp_path: Path) -> None:
    _valid_set(tmp_path)
    MODULE.verify_release_packages(tmp_path, VERSION)


@pytest.mark.parametrize(
    "filename",
    [
        f"maf-doctor.{VERSION}.nupkg",
        f"maf-doctor.Analyzers.{VERSION}.nupkg",
        f"maf-doctor.{VERSION}.snupkg",
        f"maf-doctor.Analyzers.{VERSION}.snupkg",
    ],
)
def test_rejects_each_missing_artifact(tmp_path: Path, filename: str) -> None:
    _valid_set(tmp_path)
    (tmp_path / filename).unlink()
    with pytest.raises(MODULE.VerificationError, match="artifact set mismatch"):
        MODULE.verify_release_packages(tmp_path, VERSION)


def test_rejects_unexpected_package(tmp_path: Path) -> None:
    _valid_set(tmp_path)
    _write_package(tmp_path / "unexpected.1.0.0.nupkg", "unexpected", {}, content=False)
    with pytest.raises(MODULE.VerificationError, match="unexpected"):
        MODULE.verify_release_packages(tmp_path, VERSION)


def test_rejects_wrong_nuspec_identity(tmp_path: Path) -> None:
    _valid_set(tmp_path)
    target = tmp_path / f"maf-doctor.{VERSION}.nupkg"
    with zipfile.ZipFile(target, "w") as archive:
        archive.writestr("wrong.nuspec", _nuspec("wrong", VERSION, content=True))
    with pytest.raises(MODULE.VerificationError, match="package id"):
        MODULE.verify_release_packages(tmp_path, VERSION)


def test_rejects_missing_tool_tfm(tmp_path: Path) -> None:
    _valid_set(tmp_path)
    target = tmp_path / f"maf-doctor.{VERSION}.nupkg"
    with zipfile.ZipFile(target, "r") as source:
        members = {name: source.read(name) for name in source.namelist() if "net10.0" not in name}
    with zipfile.ZipFile(target, "w") as archive:
        for name, data in members.items():
            archive.writestr(name, data)
    with pytest.raises(MODULE.VerificationError, match="tool TFMs"):
        MODULE.verify_release_packages(tmp_path, VERSION)


def test_rejects_duplicate_analyzer_dll(tmp_path: Path) -> None:
    _valid_set(tmp_path)
    target = tmp_path / f"maf-doctor.Analyzers.{VERSION}.nupkg"
    with zipfile.ZipFile(target, "a") as archive:
        archive.writestr("analyzers/dotnet/cs/netstandard2.0/maf-doctor.Analyzers.dll", b"dll")
    with pytest.raises(MODULE.VerificationError, match="analyzer DLL payload"):
        MODULE.verify_release_packages(tmp_path, VERSION)


def test_rejects_wrong_tool_command(tmp_path: Path) -> None:
    _valid_set(tmp_path)
    target = tmp_path / f"maf-doctor.{VERSION}.nupkg"
    with zipfile.ZipFile(target, "r") as source:
        members = {name: source.read(name) for name in source.namelist()}
    settings = "tools/net8.0/any/DotnetToolSettings.xml"
    members[settings] = members[settings].replace(b'Name="maf-doctor"', b'Name="wrong"')
    with zipfile.ZipFile(target, "w") as archive:
        for name, data in members.items():
            archive.writestr(name, data)
    with pytest.raises(MODULE.VerificationError, match="command is"):
        MODULE.verify_release_packages(tmp_path, VERSION)
