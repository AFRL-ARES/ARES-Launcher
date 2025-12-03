#!/bin/bash

# Define variables
APP_NAME="ARESLauncher.app"
DMG_VOLUME_NAME="ARES Launcher"
PUBLISH_OUTPUT_DIRECTORY=${1:-"publish"}
VERSION=${2:-"1.0"}
DMG_FILE=${3:-"ARESLauncher.dmg"}
INFO_PLIST="Info.plist"
ICON_FILE="BlackARESLogo_Smol.icns"

# Update Info.plist with the new version
/usr/libexec/PlistBuddy -c "Set :CFBundleVersion $VERSION" "$INFO_PLIST"

# Remove old .app bundle if it exists
if [ -d "$APP_NAME" ]; then
    rm -rf "$APP_NAME"
fi

# Create the .app bundle structure
mkdir -p "$APP_NAME/Contents/MacOS"
mkdir -p "$APP_NAME/Contents/Resources"

# Copy the Info.plist file and the icon
cp "$INFO_PLIST" "$APP_NAME/Contents/Info.plist"
cp "$ICON_FILE" "$APP_NAME/Contents/Resources/logo.icns"

# Copy the published output to the MacOS directory
cp -a "$PUBLISH_OUTPUT_DIRECTORY/." "$APP_NAME/Contents/MacOS"

echo "Packaged $APP_NAME successfully."

# Sign the app
codesign --deep --force --verbose --sign - "$APP_NAME"
echo "Packaged and signed $APP_NAME successfully."

# Create DMG
STAGING_DIR="dmg_staging"
rm -rf "$STAGING_DIR"
mkdir -p "$STAGING_DIR"
cp -R "$APP_NAME" "$STAGING_DIR/"
ln -s /Applications "$STAGING_DIR/Applications"

hdiutil create -volname "$DMG_VOLUME_NAME" -srcfolder "$STAGING_DIR" -ov -format UDZO "$DMG_FILE"

rm -rf "$STAGING_DIR"

echo "DMG created successfully."