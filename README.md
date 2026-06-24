# CSharpApiExtractorGUI

`CSharpApiExtractorGUI` is a graphical extension for [CSharpApiExtractor](https://github.com/AlexVertax/CSharpApiExtractor), built for exporting API Reference from C# projects.

It makes this workflow easier when you want to manage and export multiple projects without handling everything through code.

It is intended for preparing API Reference files for [One File Docs](https://onefiledocs.com/). The app lets you keep several project configurations, choose source folders, exclude paths you do not want to scan, set the output JSON file, optionally save missing items, and run the export from a desktop UI. After export, you can upload the generated JSON file to the API Reference section in [One File Docs](https://onefiledocs.com/).

Use `CSharpApiExtractorGUI` if you want a simple way to export API Reference for multiple projects without programming. If you want to integrate API Reference generation into your `CI/CD`, use [CSharpApiExtractor](https://github.com/AlexVertax/CSharpApiExtractor) directly.

## What One File Docs Is

[One File Docs](https://onefiledocs.com/) is a service for creating interactive multi-page documentation that is bundled into a single HTML file.

The final file contains pages, images, styles, scripts, and navigation inside it. The result is not a static document but a complete documentation experience that stays multi-page, opens in a browser, and behaves like a small documentation site while still existing as a single file.

This approach works well when documentation needs to be opened locally, attached to a release, shared with a client, passed around inside a team, or used without separate hosting and without an extra bundle of files.

## Stack

- .NET 9
- WPF
- Roslyn

## Notes

- Project settings are stored in `%LOCALAPPDATA%\CSharpApiExtractorGUI`.
- JSON handling in this project is built on [SilkJson](https://github.com/AlexVertax/SilkJson).
- The main output is a JSON file that can be used in documentation workflows for [One File Docs](https://onefiledocs.com/).
