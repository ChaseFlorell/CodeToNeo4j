namespace CodeToNeo4j.Extensions;

public static class StringExtensions
{
	/// <summary>
	/// Truncates a string to the specified maximum length if it exceeds that length.
	/// </summary>
	/// <remarks><c>8000</c> is the default max length in neo4j</remarks>
	public static string? Truncate(this string? value, int maxLength = 8000) =>
		value is not null && value.Length > maxLength
			? value[..maxLength]
			: value;
}
