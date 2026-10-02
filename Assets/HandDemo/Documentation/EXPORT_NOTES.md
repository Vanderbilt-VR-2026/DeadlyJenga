HISTORICAL SOURCE-PROJECT RECORD. Superseded by IMPORT_GUIDE.md and PORTABILITY_AUDIT.md for this destination.

# Export procedure

The package is produced by Unity's `AssetDatabase.ExportPackage`, not by manually constructing an archive.

Unity 6000.3's `IncludeDependencies` flag was tested and its archive inspected. It included 98 `Packages/` source/shader/icon files from installed UPM packages. That archive was replaced, because package-owned files must remain external and versioned by Package Manager.

The final exporter includes dependencies explicitly: it collects every feature asset and Unity's recursive `AssetDatabase.GetDependencies` result, retains all `Assets/` project dependencies, verifies they live under `Assets/HandDemo`, then exports that complete list through Unity. `Packages/` content and engine built-ins are deliberately excluded. References keep their original GUIDs and resolve against the installed packages in the destination.

To export again, exit Play Mode and choose **Tools > Flying Hand > Export Portable Package**, then select an output path outside `Assets`. The exporter refuses accidental dependencies outside the feature root rather than silently bundling unrelated project content. It never installs packages or changes project-level XR/render/input settings.

See `IMPORT_GUIDE.md` and `PackageDependencies.json` for the external requirements. Archive inspection confirms all exported asset paths are under `Assets/HandDemo`, with no `Packages/`, `ProjectSettings/`, unrelated scene/sample assets, or repository content.
