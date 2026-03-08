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

## Building

### MSIX Package (default)
Run `.\build.ps1` to build the MSIX package.

### MSI Installer (alternative)
Run `.\build-msi.ps1` to build an MSI installer using WiX Toolset.

Requires WiX Toolset installed from https://wixtoolset.org/releases/

### Zip Archive (alternative)
Run `.\build-zip.bat` to build a zip archive of the application.
