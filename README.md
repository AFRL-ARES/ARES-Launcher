# ARES Launcher

ARES Launcher is a utility designed to make installing, running, and managing self‑contained ARES instances simple and reliable.

## Getting Started

Visit the [Releases](https://github.com/AFRL-ARES/ARES-Launcher/releases) page and download the latest launcher zip for your operating system. Builds are available for **Windows**, **Linux**, and **macOS (arm64)**.

After downloading, extract the zip to any folder. Inside, you will find the launcher executable.

### Windows

Double‑click `ARESLauncher.exe` to start the launcher. Windows SmartScreen may display a warning-select **More info → Run anyway** to continue.

### macOS (arm64)

Double click the downloaded .dmg file to mount the volume. Then drag the ARESLauncher application into the Applications directory.

Upon starting the launcher you will be greeted with a security exception. You will just need to follow these simple steps to start it. [Running Unknown Apps](https://support.apple.com/guide/mac-help/open-a-mac-app-from-an-unknown-developer-mh40616/mac)

### Linux

The launcher currently isn't packaged as a native `.desktop` bundle. After extracting, you can run it from a terminal:

```bash
./ARESLauncher
```

You may need to set executable permissions:

```bash
chmod +x ARESLauncher
```

### Offline Use
To support users requiring a completely offline installation solution, we provide an additional Offline Launcher for all three major platforms. This version offers the same functionality as the standard launcher but comes pre-packaged with the latest version of ARES. It can be installed without an internet connection, making it ideal for air-gapped environments or locations with restricted network access. These can be found alongside the regular launcher release files.

## Using the Launcher

### Installation

On startup, the launcher displays a simple UI with two tabs and an **Install** button. Click **Install** and the launcher will:

* Download the correct ARES binaries for your system
* Create a local database
* Configure the instance automatically

During installation you may see a certificate prompt. Approve the certificate-it's required for ARES’s built‑in web servers and for secure communication between the ARES service and UI.

### Running ARES

After installation, click **Start**. The launcher will:

* Start both the ARES service and UI
* Open your browser to the ARES UI URL

It may take several seconds for the server to finish booting, so the page may appear unavailable briefly.

You can safely close the launcher window-it continues running in the background and can be accessed from the **system tray**. To fully exit, right‑click the tray icon and choose **Exit**. *Stopping the launcher does **not** stop ARES; stopping ARES must be done explicitly.*

#### Handling Conflicts

If you launch the app while an ARES instance is already running, you’ll be prompted to choose how to resolve the conflict:

* **Take Over** – assign the running processes to the launcher
* **Kill Processes** – terminate the existing instance
* **Ignore** – leave the existing instance running

The process names used for detection are configurable but generally should not be touched unless you know what you're doing.

## Configuration

The **Configuration** tab allows you to customize launcher behavior.

### Binary Locations

Set where ARES binaries should be stored. Most ARES releases bundle the UI and service together, so their binary paths should usually be the same. By default, binaries are placed in a subfolder of the launcher’s directory.

### Advanced Settings

Advanced options allow further customization:

* **Database Provider** – The default is SQLite, stored locally, but you can supply a custom SQLite path or switch providers using a valid connection string. Supported providers include **SQLite**, **PostgreSQL**, and **MSSQL**.
* **Service/UI Endpoints** – Adjust ports or addresses if they conflict with other services.

## Updates

When a new ARES release is published with a higher version number, the launcher will notify you on startup. Selecting **Update** will:

* Clear old binary directories
* Download and extract the new release bundle
* Apply any database migrations

**Always back up your database before updating ARES.**

The launcher does *not* automatically update itself. If you encounter a launcher‑specific issue, check its Releases page for a newer version.

## Custom ARES Repositories

By default, the launcher uses the official repository:

```
https://github.com/AFRL-ARES/ARES
```

You can add custom GitHub‑hosted repositories under **Available Repositories**, then select one as the **Current Repository**.

Your custom repository must:

* Provide proper release bundles
* Follow the expected structure so the launcher knows what to download

If switching repositories while an instance is already installed, update the binary path and database path to prevent conflicts.

If the custom repository is private, you can supply a GitHub token with **read** access to fetch releases.
