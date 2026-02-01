MDReader — Basics Plan (C# / WPF + WebView2)

Scope for now: a minimal, reliable Markdown viewer with clickable links and three ways to open files (Explorer, command line, drag-and-drop). Editing is out of scope for the first pass.

Section 0 — Decisions (one-time)
- Choose WPF (recommended for speed) or WinUI 3 (newer APIs).
- Use WebView2 for rendering.
- Use Markdig for Markdown → HTML.

Section 1 — Skeleton App
- Create solution + WPF app.
- Add WebView2 NuGet.
- Window layout: main viewer + status bar (file path + load status).

Section 2 — Render Markdown
- Load a markdown file from disk.
- Convert to HTML via Markdig.
- Render HTML in WebView2.
- Intercept link clicks and open in default browser.

Section 3 — Open File Entry Points
- Command line: open file passed as argument.
- Windows Explorer: enable file association (later in packaging, but support the argument now).
- Drag-and-drop: drop .md onto the window to load.

Milestone Definition (Basics Complete)
- App opens .md from command line or drag-and-drop.
- Markdown renders in the viewer.
- Links open in the default browser.
- Status bar shows the current file path and load errors.

Checklist (Basics)
- [x] Decide WPF vs WinUI 3 (WPF)
- [x] Create solution + WPF app
- [x] Add WebView2 NuGet
- [x] Add Markdig NuGet
- [x] Window layout with viewer + status bar
- [x] Load markdown file → HTML
- [x] Render HTML in WebView2
- [x] Open external links in default browser
- [x] Parse command-line file argument
- [x] Implement drag-and-drop open
- [x] Status bar: show file path + errors

Section 4 — UX Basics

Recent files list.
Error messages for missing/invalid files.
Theme toggle (light/dark).
Zoom in/out.

Section 5 — Optional Viewer Enhancements

Live reload on file change.
Search in document.
Table of contents.

Section 6 — WYSIWYG (later)

Add editor pane (split view).
Embed rich editor in WebView2 (e.g., TipTap).
Save edits back to .md.
Conflict detection if file changes on disk.

Section 7 — Packaging

MSIX packaging.
File association in installer.
Optional auto-update.

Checklist (Remaining)

- [] Add recent files list
- []  Add basic error handling
- []  Add theme toggle
- []  Add zoom controls
- []  Optional: live reload on file change
- []  Optional: search in document
- []  Optional: table of contents
- []  Later: split view editor
- []  Later: WYSIWYG editor in WebView2
- []  Later: save + conflict handling
- []  Package with MSIX Add recent files list
- []  Add basic error handling
- []  Add theme toggle
- []  Add zoom controls
- []  Optional: live reload on file change
- []  Optional: search in document
- []  Optional: table of contents
- []  Later: split view editor
- []  Later: WYSIWYG editor in WebView2
- []  Later: save + conflict handling
- []  Package with MSIX