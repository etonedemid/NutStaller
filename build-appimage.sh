#!/bin/sh
# Builds NutStaller-x86_64.AppImage. Needs the .NET SDK, and network the first
# time (appimagetool is fetched into NutStaller/bin/, it fetches its runtime).
#
# The app is published self-contained and not single-file, so nothing is
# unpacked at startup and the AppImage needs no .NET or libicu on the host.
# The bundled runtime and Skia want glibc 2.17+; the type2 AppImage runtime
# is static, so FUSE 3 is the only other host requirement (or run it with
# --appimage-extract-and-run).
set -eu
cd "$(dirname "$0")"

BIN=NutStaller/bin
APPDIR=$BIN/NutStaller.AppDir
TOOL=$BIN/appimagetool-x86_64.AppImage

rm -rf "$APPDIR"
dotnet publish NutStaller/NutStaller.csproj -c Release -r linux-x64 --self-contained \
    -p:PublishSingleFile=false -p:DebugType=none -o "$APPDIR/usr/bin"
cp packaging/nutstaller.desktop packaging/nutstaller.png "$APPDIR/"
ln -s usr/bin/NutStaller "$APPDIR/AppRun"

if [ ! -x "$TOOL" ]; then
    curl -fL -o "$TOOL" \
        https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage
    chmod +x "$TOOL"
fi
ARCH=x86_64 "$TOOL" --no-appstream "$APPDIR" "$BIN/NutStaller-x86_64.AppImage"
echo "built $BIN/NutStaller-x86_64.AppImage"
