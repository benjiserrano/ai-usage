#!/usr/bin/env bash
# Publica el proyecto y lo empaqueta en AIUsage.app.
#
# Hay que ejecutarlo EN macOS: sips, iconutil y codesign solo existen ahí.
# Desde Windows se puede compilar el binario (dotnet publish -r osx-arm64),
# pero no montar el bundle.
#
#   ./packaging/build-macos.sh [osx-arm64|osx-x64]

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RID="${1:-osx-arm64}"
OUT="$ROOT/portable/mac"
APP="$OUT/AIUsage.app"

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

dotnet publish "$ROOT/AIUsage.csproj" -c Release -r "$RID" --self-contained -o "$OUT/bin"

cp "$OUT/bin/AIUsage" "$APP/Contents/MacOS/AIUsage"
chmod +x "$APP/Contents/MacOS/AIUsage"
cp "$ROOT/packaging/Info.plist" "$APP/Contents/Info.plist"

# .icns a partir del PNG que ya usa la app en Windows.
ICONSET="$(mktemp -d)/app-icon.iconset"
mkdir -p "$ICONSET"
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" "$ROOT/Assets/app-icon.png" \
    --out "$ICONSET/icon_${size}x${size}.png" > /dev/null
  sips -z "$((size * 2))" "$((size * 2))" "$ROOT/Assets/app-icon.png" \
    --out "$ICONSET/icon_${size}x${size}@2x.png" > /dev/null
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/app-icon.icns"

# Firma ad-hoc. No satisface a Gatekeeper, pero sin ninguna firma macOS trata
# el bundle como no identificado y algunas comprobaciones fallan.
codesign --force --sign - "$APP"

echo "Listo: $APP"
echo
echo "Sin notarizar, Gatekeeper lo bloquea la primera vez. Para abrirlo:"
echo "  xattr -dr com.apple.quarantine \"$APP\""
