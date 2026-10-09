using AsmResolver.DotNet;
using AssetRipper.Import.Logging;
using AssetRipper.Import.Structure.Assembly.Managers;
using AssetRipper.Import.Structure.Platforms;
using AssetRipper.IO.Files;

namespace AssetRipper.Import.Platforms;

internal static class VirtualDllLoader
{
	private static readonly string[] DirectoryNames = ["Il2CppDummyDlls", "DummyDlls"];

	public static bool TryLoad(PlatformGameStructure gameStructure, Action<string> requestAssemblyCallback,
		[NotNullWhen(true)] out MonoManager? manager)
	{
		manager = null;
		if (string.IsNullOrEmpty(gameStructure.GameDataPath))
		{
			return false;
		}

		FileSystem fileSystem = gameStructure.FileSystem;
		foreach (string directoryName in DirectoryNames)
		{
			string directory = fileSystem.Path.Join(gameStructure.GameDataPath, directoryName);
			MonoManager? candidate = null;
			try
			{
				if (!fileSystem.Directory.Exists(directory))
				{
					continue;
				}

				Dictionary<string, (AssemblyDefinition Assembly, string Path)> assemblies = new(StringComparer.OrdinalIgnoreCase);
				foreach (string path in fileSystem.Directory.EnumerateFiles(directory).Order(StringComparer.Ordinal))
				{
					if (!string.Equals(fileSystem.Path.GetExtension(path), MonoManager.AssemblyExtension, StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					AssemblyDefinition assembly = AssemblyDefinition.FromBytes(fileSystem.File.ReadAllBytes(path), createRuntimeContext: false);
					string name = assembly.Name?.ToString() ?? "";
					if (string.IsNullOrEmpty(name) || assembly.ManifestModule is null)
					{
						throw new InvalidDataException($"'{path}' has no assembly name or manifest module.");
					}
					if (!assemblies.TryAdd(name, (assembly, path)))
					{
						throw new InvalidDataException($"Duplicate assembly '{name}' in '{directory}'.");
					}
				}

				if (assemblies.Count == 0)
				{
					throw new InvalidDataException("The folder contains no managed DLLs.");
				}

				candidate = new MonoManager(requestAssemblyCallback);
				candidate.Initialize(assemblies.Values.Select(value => value.Assembly).ToArray());

				// Publish the external paths only after the entire candidate has parsed and initialized.
				gameStructure.Assemblies.Clear();
				foreach ((string name, (_, string path)) in assemblies)
				{
					gameStructure.Assemblies.Add($"{name}{MonoManager.AssemblyExtension}", path);
				}
				manager = candidate;
				Logger.Info(LogCategory.Import, $"Using external DLLs from '{directory}' ({assemblies.Count} assemblies).");
				return true;
			}
			catch (Exception ex)
			{
				candidate?.Dispose();
				Logger.Warning(LogCategory.Import, $"Could not use external DLLs from '{directory}': {ex.Message}. Trying the next folder or the original scripting backend.");
			}
		}
		return false;
	}
}
