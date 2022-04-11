using SQLite.CodeFirst.Statement;

namespace SQLite.CodeFirst.Builder {
	internal class DropIndexStatementBuilder : IStatementBuilder<DropIndexStatement> {
		private readonly string indexName;
		public DropIndexStatementBuilder(string indexNames) => this.indexName = indexNames;

		public DropIndexStatement BuildStatement() => new DropIndexStatement() { Name = indexName };
	}
}
