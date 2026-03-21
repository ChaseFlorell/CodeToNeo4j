namespace CodeToNeo4j.Dart.Yaml;

public interface IPubspecParser
{
	PubspecInfo Parse(string content);
}

public class PubspecParser : IPubspecParser
{
	public PubspecInfo Parse(string content)
	{
		var name = string.Empty;
		string? sdkConstraint = null;
		List<PubspecDependency> dependencies = [];
		List<PubspecDependency> devDependencies = [];

		string? currentSection = null;
		var inEnvironment = false;

		foreach (var line in content.Split('\n'))
		{
			var trimmed = line.TrimEnd('\r');

			// Top-level key (no indentation)
			if (!string.IsNullOrEmpty(trimmed) && !char.IsWhiteSpace(trimmed[0]))
			{
				inEnvironment = false;
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
				else if (trimmed.StartsWith("environment:", StringComparison.Ordinal))
				{
					currentSection = null;
					inEnvironment = true;
				}
				else
				{
					currentSection = null;
				}

				continue;
			}

			// Indented entries
			if (string.IsNullOrWhiteSpace(trimmed))
			{
				continue;
			}

			var stripped = trimmed.TrimStart();
			var indent = trimmed.Length - stripped.Length;
			if (indent == 0)
			{
				continue;
			}

			// Parse environment.sdk
			if (inEnvironment && stripped.StartsWith("sdk:", StringComparison.Ordinal))
			{
				var raw = stripped["sdk:".Length..].Trim().Trim('"', '\'');
				if (!string.IsNullOrEmpty(raw))
				{
					sdkConstraint = raw;
				}

				continue;
			}

			if (currentSection is null)
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

			if (string.IsNullOrEmpty(depName))
			{
				continue;
			}

			var version = string.IsNullOrEmpty(depValue) ? null : depValue;
			var isDev = currentSection == "dev_dependencies";

			var list = isDev ? devDependencies : dependencies;
			list.Add(new(depName, version, isDev));
		}

		return new(name, dependencies, devDependencies, sdkConstraint);
	}
}

public record PubspecInfo(string Name, List<PubspecDependency> Dependencies, List<PubspecDependency> DevDependencies, string? SdkConstraint = null);

public record PubspecDependency(string Name, string? Version, bool IsDev);
