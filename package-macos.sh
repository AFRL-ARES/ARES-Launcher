#!/bin/bash
set -e

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
        # Check if the file is a Mach-O binary (executable or dynamic library)
        if file "$arm64_file" | grep -q "Mach-O"; then
            echo "  -> Fusing Universal slice: $rel_path"
            lipo -create "$x64_file" "$arm64_file" -output "$target_file"
        fi
    else
        # Copy files that exist only in arm64
        mkdir -p "$(dirname "$target_file")"
        cp -a "$arm64_file" "$target_file"
    fi
done

echo "Packaged $APP_NAME successfully."

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