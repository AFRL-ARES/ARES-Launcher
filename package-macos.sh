```bash
#!/bin/bash
set -euo pipefail

APP_NAME="ARESLauncher.app"
DMG_VOLUME_NAME="ARES Launcher"

PUBLISH_X64="${1:-publish-x64}"
PUBLISH_ARM64="${2:-publish-arm64}"
VERSION="${3:-1.0}"
DMG_FILE="${4:-ARESLauncher.dmg}"

INFO_PLIST="Info.plist"
ICON_FILE="BlackARESLogo_Smol.icns"

has_arch() {
    local archs="$1"
    local wanted="$2"
    [[ " $archs " == *" $wanted "* ]]
}

# Validate required inputs.
for required_dir in "$PUBLISH_X64" "$PUBLISH_ARM64"; do
    if [[ ! -d "$required_dir" ]]; then
        echo "ERROR: Publish directory not found: $required_dir" >&2
        exit 1
    fi
done

for required_file in "$INFO_PLIST" "$ICON_FILE"; do
    if [[ ! -f "$required_file" ]]; then
        echo "ERROR: Required file not found: $required_file" >&2
        exit 1
    fi
done

# Remove previous output.
rm -rf "$APP_NAME"

# Create the application bundle.
mkdir -p "$APP_NAME/Contents/MacOS"
mkdir -p "$APP_NAME/Contents/Resources"

cp "$INFO_PLIST" "$APP_NAME/Contents/Info.plist"
cp "$ICON_FILE" "$APP_NAME/Contents/Resources/logo.icns"

# Update the version in the copied Info.plist.
/usr/libexec/PlistBuddy \
    -c "Set :CFBundleVersion $VERSION" \
    "$APP_NAME/Contents/Info.plist" 2>/dev/null || \
/usr/libexec/PlistBuddy \
    -c "Add :CFBundleVersion string $VERSION" \
    "$APP_NAME/Contents/Info.plist"

echo "Stitching Universal 2 application..."

# Use the x64 output as the base for managed and architecture-independent files.
cp -a "$PUBLISH_X64/." "$APP_NAME/Contents/MacOS/"

# Merge complementary thin Mach-O binaries. Keep files that are already
# Universal 2 without passing duplicate slices to lipo.
while IFS= read -r -d '' arm64_file; do
    rel_path="${arm64_file#"$PUBLISH_ARM64"/}"
    x64_file="$PUBLISH_X64/$rel_path"
    target_file="$APP_NAME/Contents/MacOS/$rel_path"

    mkdir -p "$(dirname "$target_file")"

    if [[ ! -f "$x64_file" ]]; then
        echo "  -> Copying arm64-only file: $rel_path"
        cp -a "$arm64_file" "$target_file"
        continue
    fi

    # Non-Mach-O files are already present from the x64 base copy.
    if ! file "$arm64_file" | grep -q "Mach-O"; then
        continue
    fi

    if ! file "$x64_file" | grep -q "Mach-O"; then
        echo "ERROR: Architecture mismatch for $rel_path" >&2
        echo "The arm64 file is Mach-O, but the x64 file is not." >&2
        exit 1
    fi

    x64_archs="$(lipo -archs "$x64_file")"
    arm64_archs="$(lipo -archs "$arm64_file")"

    echo "  -> $rel_path"
    echo "     x64 input:   $x64_archs"
    echo "     arm64 input: $arm64_archs"

    if has_arch "$x64_archs" x86_64 &&
       has_arch "$x64_archs" arm64; then
        echo "     Already Universal 2; keeping base copy."

    elif has_arch "$arm64_archs" x86_64 &&
         has_arch "$arm64_archs" arm64; then
        echo "     Already Universal 2; using arm64 publish copy."
        cp -a "$arm64_file" "$target_file"

    elif has_arch "$x64_archs" x86_64 &&
         has_arch "$arm64_archs" arm64; then
        echo "     Combining x86_64 and arm64 slices."
        lipo -create \
            "$x64_file" \
            "$arm64_file" \
            -output "$target_file"

    else
        echo "ERROR: Cannot produce Universal 2 binary for $rel_path" >&2
        echo "  x64 input architectures:   $x64_archs" >&2
        echo "  arm64 input architectures: $arm64_archs" >&2
        exit 1
    fi
done < <(find "$PUBLISH_ARM64" -type f -print0)

# Ensure the launcher remains executable after lipo.
chmod +x "$APP_NAME/Contents/MacOS/ARESLauncher"

echo "Validating packaged Mach-O binaries..."

while IFS= read -r -d '' binary; do
    if ! file "$binary" | grep -q "Mach-O"; then
        continue
    fi

    archs="$(lipo -archs "$binary")"
    relative_path="${binary#"$APP_NAME"/}"

    echo "  -> $relative_path: $archs"

    if ! has_arch "$archs" x86_64 ||
       ! has_arch "$archs" arm64; then
        echo "ERROR: $binary is not Universal 2: $archs" >&2
        exit 1
    fi
done < <(find "$APP_NAME/Contents/MacOS" -type f -print0)

echo "Packaged $APP_NAME successfully."

# Apply an ad-hoc signature after all binaries have been modified.
codesign --deep \
    --force \
    --verbose \
    --sign - \
    "$APP_NAME"

codesign --verify \
    --deep \
    --strict \
    --verbose=2 \
    "$APP_NAME"

echo "Signed and verified $APP_NAME successfully."

# Create the DMG.
STAGING_DIR="dmg_staging"

rm -rf "$STAGING_DIR"
mkdir -p "$STAGING_DIR"

cp -R "$APP_NAME" "$STAGING_DIR/"
ln -s /Applications "$STAGING_DIR/Applications"

hdiutil create \
    -volname "$DMG_VOLUME_NAME" \
    -srcfolder "$STAGING_DIR" \
    -ov \
    -format UDZO \
    "$DMG_FILE"

rm -rf "$STAGING_DIR"

echo "Universal 2 DMG ($DMG_FILE) created successfully."
```