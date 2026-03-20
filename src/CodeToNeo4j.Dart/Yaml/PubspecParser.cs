namespace CodeToNeo4j.Dart.Yaml;

public static class PubspecParser
{
	public static PubspecInfo Parse(string content)
	{
		var name = string.Empty;
		List<PubspecDependency> dependencies = [];
		List<PubspecDependency> devDependencies = [];

		string? currentSection = null;

		foreach (var line in content.Split('\n'))
		{
			var trimmed = line.TrimEnd('\r');

			// Top-level key (no indentation)
			if (!string.IsNullOrEmpty(trimmed) && !char.IsWhiteSpace(trimmed[0]))
			{
				if (trimmed.StartsWith("name:", StringComparison.Ordinal))
				{
					name = trimmed["name:".Length..].Trim();
					currentSection = null;
				}
				else if (trimmed.StartsWith("dependencies:", StringComparison.Ordinal))
				{
					currentSection = "dependencies";
				}
				else if (trimmed.StartsWith("dev_dependencies:", StringComparison.Ordinal))
				{
					currentSection = "dev_dependencies";
				}
				else
				{
					currentSection = null;
				}

				continue;
			}

			// Indented entry under a section
			if (currentSection is null)
			{
				continue;
			}

			if (string.IsNullOrWhiteSpace(trimmed))
			{
				continue;
			}

			// Only process direct children (single indent level, typically 2 spaces)
			var stripped = trimmed.TrimStart();
			var indent = trimmed.Length - stripped.Length;
			if (indent == 0)
			{
				continue;
			}

			var colonIndex = stripped.IndexOf(':');
			if (colonIndex <= 0)
			{
				continue;
			}

			var depName = stripped[..colonIndex].Trim();
			var depValue = stripped[(colonIndex + 1)..].Trim();

			// Skip sub-keys (e.g. "path:", "git:" under a dependency)
			// These will have deeper indentation, but we only care about top-level deps
			if (string.IsNullOrEmpty(depName))
			{
				continue;
			}

			// Simple version constraint (e.g. "^1.0.0") or empty
			var version = string.IsNullOrEmpty(depValue) ? null : depValue;
			var isDev = currentSection == "dev_dependencies";

			var list = isDev ? devDependencies : dependencies;
			list.Add(new(depName, version, isDev));
		}

		return new(name, dependencies, devDependencies);
	}
}

public record PubspecInfo(string Name, List<PubspecDependency> Dependencies, List<PubspecDependency> DevDependencies);

public record PubspecDependency(string Name, string? Version, bool IsDev);
