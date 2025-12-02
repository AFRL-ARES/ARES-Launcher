#!/bin/bash

# Define variables
APP_NAME="ARESLauncher.app"
ZIP_FILE="ARESLauncher.zip"
PUBLISH_OUTPUT_DIRECTORY="publish"
INFO_PLIST="Info.plist"
ICON_FILE="BlackARESLogo_Smol.icns"
# SIGNING_IDENTITY is your Developer ID Application: Your Name (TEAMID)
SIGNING_IDENTITY="Developer ID Application: Your Name (TEAMID)"

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
# codesign --deep --force --verbose --sign "$SIGNING_IDENTITY" "$APP_NAME"
# echo "Packaged and signed $APP_NAME successfully."


# Zip the .app bundle
zip -r "$ZIP_FILE" "$APP_NAME"