# MDReader
Markdown reader and editor.

## Features
- Open and view Markdown files
- Edit Markdown with a live preview (WYSIWYG)
- Toggle dark mode and table of contents
- Optional hard line breaks
- Print the current document (`File → Print` or `Ctrl+P`)

## Installation
Download the latest release from Github
Unzip the downloaded file and run `MDReader.app.exe` to start the application.

## Notes

In the conversion between Markdown and HTML (used for the editor screen), it will reformat your Markdown content. This may result in changes to the formatting 
of your original Markdown file. If you want to preserve the original formatting, consider using a dedicated Markdown editor that supports this feature, and only 
use this application for viewing.

This application is built using .NET 8 and Windows Presentation Foundation (WPF). It is designed to run on Windows 10 and later versions. If you encounter any 
issues or have suggestions for improvements, please feel free to open an issue on the GitHub repository.

## Dependencies

### App/runtime
- .NET 10 (Windows target)
- Markdig (`0.44.0`) for Markdown parsing/rendering
- Microsoft.Web.WebView2 (`1.0.3719.77`) for embedded browser rendering
- Turndown (bundled at `MDReader.App/Assets/turndown.js`) for HTML-to-Markdown conversion in edit mode

### Packaging/build
- Microsoft.Windows.SDK.BuildTools.MSIX (`1.7.251221100`) in the app project
- Microsoft.Windows.SDK.BuildTools (`10.0.26100.1742`) in the WAP packaging project
- WiX Toolset (required only for MSI build via `build-msi.ps1`)

### Test project
- xUnit (`2.9.3`)
- xUnit runner for Visual Studio (`3.1.4`)
- Microsoft.NET.Test.Sdk (`17.14.1`)
- coverlet.collector (`6.0.4`)

## Building

### MSIX Package (default)
Run `.\build.ps1` to build the MSIX package.

### MSI Installer (alternative)
Run `.\build-msi.ps1` to build an MSI installer using WiX Toolset.

Requires WiX Toolset installed from https://wixtoolset.org/releases/

### Zip Archive (alternative)
Run `.\build-zip.bat` to build a zip archive of the application.

For an ARM64 release zip from repo root:
`build-zip.bat Release ARM64 true`

Optional fourth argument selects target OS (`win` or `linux`):
`build-zip.bat Release x64 true win`

Linux currently returns a clear error because this app targets WPF (`net10.0-windows`) and WebView2, which are Windows-only.

Results will be found in ZipOutput.

## Versioning

The repository version is stored in [Version.props](Version.props) as `MDReaderVersion`.

- Manual edit: update `MDReaderVersion` (format `Major.Minor.Patch.Revision`, e.g. `1.0.0.0`).
- Auto increment: run [bump-version.ps1](bump-version.ps1)
	- `./bump-version.ps1` (increments revision)
	- `./bump-version.ps1 -Increment Patch`
	- `./bump-version.ps1 -Increment Minor`
	- `./bump-version.ps1 -Increment Major`
	- `./bump-version.ps1 -Version 1.2.0.0`

This version is used by the app assembly metadata, MSIX package version, and MSI package version.