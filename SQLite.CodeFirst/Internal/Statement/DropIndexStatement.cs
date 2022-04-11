using System.Text;

namespace SQLite.CodeFirst.Statement {
	internal class DropIndexStatement : IStatement {
		private const string Template = "DROP INDEX {index-name};";
		public string Name { get; set; }
		public string CreateStatement() {
			var stringBuilder = new StringBuilder(Template);
			stringBuilder.Replace("{index-name}", Name);
			return stringBuilder.ToString();
		}
	}
}
