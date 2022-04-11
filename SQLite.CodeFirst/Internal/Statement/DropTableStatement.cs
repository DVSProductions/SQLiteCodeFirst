using System.Text;

namespace SQLite.CodeFirst.Statement {
	internal class DropTableStatement : IStatement {
		private const string Template = "DROP TABLE {table-name};";

		public string TableName { get; set; }

		public string CreateStatement() {
			var sb = new StringBuilder(Template);
			sb.Replace("{table-name}", TableName);
			return sb.ToString();
		}
	}
}