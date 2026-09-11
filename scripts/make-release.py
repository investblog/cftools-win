"""Package the GitHub / SourceForge release assets from the self-contained Release build.

    python scripts/make-release.py            # package what is in bin/ (run the MSBuild step first)
    python scripts/make-release.py --build    # run the MSBuild step too

Produces in temp/release/<version>/:
  CloudflareTools-v<version>-x64-setup.exe   InnoSetup installer (installer/setup.iss)
  CloudflareTools-v<version>-x64.zip         no-installer ZIP, one top-level folder
  SHA256SUMS                                 basenames, `sha256sum -c SHA256SUMS` next to the files

WHY SELF-CONTAINED. The default Release build is framework-dependent: no coreclr.dll, no
PackageDependency in the manifest, so a PC without the .NET 8 Desktop Runtime gets an
"install .NET" dialog instead of the app. Every shelf outside the Store (GitHub, SourceForge)
hands the file to strangers, so the build here is `-p:SelfContained=true` and this script
refuses to package a folder that lacks the runtime. The Store MSIX is built the same way
(see CLAUDE.md), for the same reason.

The MSIX is NOT a release asset: unsigned outside the Store, it cannot be installed by a
downloader, and SourceForge would happily make it the default download.
"""

from __future__ import annotations

import hashlib
import re
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MSBUILD = Path(r"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe")
ISCC = Path(r"W:\Program Files\Inno Setup 6\ISCC.exe")
BUILD_DIR = ROOT / "src" / "CFTools" / "bin" / "x64" / "Release" / "net8.0-windows10.0.19041.0" / "win-x64"
EXCLUDE_SUFFIXES = (".pdb", ".appxrecipe")
EXCLUDE_NAMES = {"AppxManifest.xml"}


def version() -> str:
    text = (ROOT / "installer" / "setup.iss").read_text(encoding="utf-8")
    m = re.search(r'#define MyAppVersion "([^"]+)"', text)
    if not m:
        sys.exit("setup.iss: MyAppVersion not found")
    return m.group(1)


def build() -> None:
    cmd = [
        str(MSBUILD), str(ROOT / "src" / "CFTools" / "CFTools.csproj"),
        "-p:Platform=x64", "-p:Configuration=Release", "-p:RuntimeIdentifier=win-x64",
        "-p:SelfContained=true", "-nologo", "-v:m",
    ]
    print("+", " ".join(cmd[1:]))
    subprocess.run(cmd, check=True)


def check_self_contained() -> None:
    for name in ("coreclr.dll", "hostfxr.dll", "CFTools.exe", "resources.pri"):
        if not (BUILD_DIR / name).exists():
            sys.exit(f"FAIL: {BUILD_DIR / name} missing — build with -p:SelfContained=true first")


def package_files():
    for path in sorted(BUILD_DIR.rglob("*")):
        if not path.is_file():
            continue
        rel = path.relative_to(BUILD_DIR)
        if rel.suffix.lower() in EXCLUDE_SUFFIXES or rel.name in EXCLUDE_NAMES:
            continue
        yield path, rel


def make_zip(out: Path, ver: str) -> Path:
    top = f"CloudflareTools-v{ver}-x64"
    target = out / f"{top}.zip"
    with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as zf:
        for path, rel in package_files():
            zf.write(path, f"{top}/{rel.as_posix()}")
        for extra in ("LICENSE", "README.md"):
            zf.write(ROOT / extra, f"{top}/{extra}")
    return target


def make_installer(out: Path, ver: str) -> Path:
    if not ISCC.exists():
        sys.exit(f"FAIL: {ISCC} not found")
    subprocess.run([str(ISCC), "/Q", f"/DBuildDirOverride={BUILD_DIR}", f"/O{out}", str(ROOT / "installer" / "setup.iss")], check=True)
    target = out / f"CloudflareTools-v{ver}-x64-setup.exe"
    if not target.exists():
        sys.exit(f"FAIL: installer not produced at {target}")
    return target


def sha256sums(out: Path, files: list[Path]) -> Path:
    lines = []
    for f in files:
        digest = hashlib.sha256(f.read_bytes()).hexdigest()
        lines.append(f"{digest}  {f.name}")
    target = out / "SHA256SUMS"
    target.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    return target


def main() -> int:
    if "--build" in sys.argv:
        build()
    check_self_contained()
    ver = version()
    out = ROOT / "temp" / "release" / ver
    if out.exists():
        shutil.rmtree(out)
    out.mkdir(parents=True)

    installer = make_installer(out, ver)
    archive = make_zip(out, ver)
    sums = sha256sums(out, [installer, archive])

    for f in (installer, archive, sums):
        print(f"{f.stat().st_size / 1_048_576:8.1f} MB  {f.relative_to(ROOT)}")
    print(sums.read_text(encoding="utf-8"), end="")
    return 0


if __name__ == "__main__":
    sys.exit(main())
