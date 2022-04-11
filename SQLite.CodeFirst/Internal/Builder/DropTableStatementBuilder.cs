using SQLite.CodeFirst.Builder.NameCreators;
using SQLite.CodeFirst.Statement;

namespace SQLite.CodeFirst.Builder {
	internal class DropTableStatementBuilder : IStatementBuilder<DropTableStatement> {
		private readonly string name;

		public DropTableStatementBuilder(string name) => this.name = name;

		public DropTableStatement BuildStatement() => new DropTableStatement {
			TableName = NameCreator.EscapeName(name)
		};
	}
}
