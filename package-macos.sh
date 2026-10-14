#!/bin/bash
set -euo pipefail

# Define variables
APP_NAME="ARESLauncher.app"
DMG_VOLUME_NAME="ARES Launcher"

PUBLISH_X64=${1:-"publish-x64"}
PUBLISH_ARM64=${2:-"publish-arm64"}
VERSION=${3:-"1.0"}
DMG_FILE=${4:-"ARESLauncher.dmg"}

INFO_PLIST="Info.plist"
ICON_FILE="BlackARESLogo_Smol.icns"

# Update Info.plist with the new version
/usr/libexec/PlistBuddy -c "Set :CFBundleVersion $VERSION" "$INFO_PLIST" 2>/dev/null || \
/usr/libexec/PlistBuddy -c "Add :CFBundleVersion string $VERSION" "$INFO_PLIST"

# Remove old .app bundle if it exists
rm -rf "$APP_NAME"

# Create the .app bundle structure
mkdir -p "$APP_NAME/Contents/MacOS"
mkdir -p "$APP_NAME/Contents/Resources"

# Copy Info.plist and icon
cp "$INFO_PLIST" "$APP_NAME/Contents/Info.plist"
cp "$ICON_FILE" "$APP_NAME/Contents/Resources/logo.icns"

echo "Stitching Universal 2 binary with lipo..."

# 1. Copy base runtime and managed assemblies from x64 build
cp -a "$PUBLISH_X64/." "$APP_NAME/Contents/MacOS/"

# 2. Iterate through arm64 files and fuse Mach-O binaries with lipo
find "$PUBLISH_ARM64" -type f | while read -r arm64_file; do
    rel_path="${arm64_file#$PUBLISH_ARM64/}"
    x64_file="$PUBLISH_X64/$rel_path"
    target_file="$APP_NAME/Contents/MacOS/$rel_path"

    if [ -f "$x64_file" ]; then
echo "Stitching Universal 2 binary with lipo..."

# Copy the x64 publish as the base. Managed assemblies and other
# architecture-independent files are identical between publishes.
cp -a "$PUBLISH_X64/." "$APP_NAME/Contents/MacOS/"

has_arch() {
    local archs="$1"
    local wanted="$2"
    [[ " $archs " == *" $wanted "* ]]
}

# Merge thin Mach-O binaries and preserve binaries that are already universal.
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

    # Non-Mach-O files were already copied from the x64 publish.
    if ! file "$arm64_file" | grep -q "Mach-O"; then
        continue
    fi

    if ! file "$x64_file" | grep -q "Mach-O"; then
        echo "ERROR: Architecture mismatch for $rel_path" >&2
        echo "  arm64 file is Mach-O, but x64 file is not." >&2
        exit 1
    fi

    x64_archs="$(lipo -archs "$x64_file")"
    arm64_archs="$(lipo -archs "$arm64_file")"

    echo "  -> $rel_path"
    echo "     x64 input:   $x64_archs"
    echo "     arm64 input: $arm64_archs"

    if has_arch "$x64_archs" x86_64 &&
       has_arch "$x64_archs" arm64; then
        # The base copy is already Universal 2.
        echo "     Already Universal 2; keeping x64 publish copy."

    elif has_arch "$arm64_archs" x86_64 &&
         has_arch "$arm64_archs" arm64; then
        # The arm64 publish contains a Universal 2 version.
        echo "     Already Universal 2; copying arm64 publish version."
        cp -a "$arm64_file" "$target_file"

    elif has_arch "$x64_archs" x86_64 &&
         has_arch "$arm64_archs" arm64; then
        # These are complementary thin binaries.
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
    else
        # Copy files that exist only in arm64
        mkdir -p "$(dirname "$target_file")"
        cp -a "$arm64_file" "$target_file"
    fi
done

echo "Packaged $APP_NAME successfully."

echo "Validating packaged Mach-O binaries..."

while IFS= read -r -d '' binary; do
    if file "$binary" | grep -q "Mach-O"; then
        archs="$(lipo -archs "$binary")"
        echo "  -> ${binary#"$APP_NAME/"}: $archs"

        if ! has_arch "$archs" x86_64 ||
           ! has_arch "$archs" arm64; then
            echo "ERROR: $binary is not Universal 2: $archs" >&2
            exit 1
        fi
    fi
done < <(find "$APP_NAME/Contents/MacOS" -type f -print0)

# Sign the app (ad-hoc signing)
codesign --deep --force --verbose --sign - "$APP_NAME"
echo "Signed $APP_NAME successfully."

# Create DMG
STAGING_DIR="dmg_staging"
rm -rf "$STAGING_DIR"
mkdir -p "$STAGING_DIR"
cp -R "$APP_NAME" "$STAGING_DIR/"
ln -s /Applications "$STAGING_DIR/Applications"

hdiutil create -volname "$DMG_VOLUME_NAME" -srcfolder "$STAGING_DIR" -ov -format UDZO "$DMG_FILE"
rm -rf "$STAGING_DIR"

echo "Universal 2 DMG ($DMG_FILE) created successfully."