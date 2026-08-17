"""Static security contracts for release artifact publication workflows."""

from pathlib import Path


WORKFLOWS = Path(__file__).resolve().parents[2] / "workflows"


def test_release_verifies_exact_packages_before_attestation_and_publish() -> None:
    text = (WORKFLOWS / "release.yml").read_text(encoding="utf-8")
    verify = text.index("verify_release_packages.py")
    attest = text.index("actions/attest-build-provenance@")
    publish = text.index("dotnet nuget push")
    assert verify < attest < publish
    assert '--dist src/dist --version "$VERSION"' in text
    assert "subject-path: 'src/dist/*nupkg'" in text
    assert "files: src/dist/*nupkg" in text


def test_analyzer_symbol_package_is_created_without_publishing_duplicate_dll_layout() -> None:
    text = (WORKFLOWS / "release.yml").read_text(encoding="utf-8")
    analyzer = text.split("- name: Pack analyzer package", 1)[1].split("- name: Verify release", 1)[0]
    assert "-p:IncludeBuildOutput=true" in analyzer
    assert "--no-build" in analyzer
    assert 'cp "$SYMBOL_PACKAGE" src/dist/' in analyzer
    assert 'cp "$SYMBOL_DIR"/*.nupkg' not in analyzer


def test_docker_tag_ancestry_guard_precedes_login_and_push() -> None:
    text = (WORKFLOWS / "docker-publish.yml").read_text(encoding="utf-8")
    guard = text.index("Guard — tag commit must be on main")
    login = text.index("Log in to GHCR")
    push = text.index("Build and push")
    assert guard < login < push
    assert "is_tag=$IS_TAG" in text
    assert "if: steps.version.outputs.is_tag == 'true'" in text[guard:login]
    assert 'git merge-base --is-ancestor "$GITHUB_SHA" origin/main' in text[guard:login]
    assert "fetch-depth: 0" in text[:guard]
