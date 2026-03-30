namespace CodeToNeo4j.Graph;

public static class GraphSchema
{
	public static class Labels
	{
		public const string Project = "src__Project";
		public const string File = "src__File";
		public const string Symbol = "src__Symbol";
		public const string Author = "src__Author";
		public const string Commit = "src__Commit";
		public const string Dependency = "src__Dependency";
		public const string Tag = "src__Tag";
		public const string Url = "src__Url";
	}

	public static class Relationships
	{
		public const string HasFile = "src__HAS_FILE";
		public const string Declares = "src__DECLARES";
		public const string DependsOn = "src__DEPENDS_ON";
		public const string Contains = "src__CONTAINS";
		public const string PartOfProject = "src__PART_OF_PROJECT";
		public const string Committed = "src__COMMITTED";
		public const string ModifiedFile = "src__MODIFIED_FILE";
		public const string Authored = "src__AUTHORED";
		public const string HasTag = "src__HAS_TAG";
		public const string HasUrl = "src__HAS_URL";
		public const string Invokes = "src__INVOKES";
		public const string HasProperty = "src__HAS_PROPERTY";
	}
}
