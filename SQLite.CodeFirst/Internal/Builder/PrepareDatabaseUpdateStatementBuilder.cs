using System.Collections.Generic;
using System.Data.Entity.Core.Metadata.Edm;
using System.Linq;
using SQLite.CodeFirst.Statement;
using SQLite.CodeFirst.Utility;

namespace SQLite.CodeFirst.Builder {
	internal class PrepareDatabaseUpdateStatementBuilder : IStatementBuilder<DropTablesAndIndexesStatement> {
		private readonly IEnumerable<string> tables;
		private readonly IEnumerable<string> indexes;
		public PrepareDatabaseUpdateStatementBuilder(IEnumerable<string> tables, IEnumerable<string> indexes) {
			this.tables = tables;
			this.indexes = indexes;
		}

		public DropTablesAndIndexesStatement BuildStatement() {
			var createTableStatements = GetDropTableStatements();
			var createIndexStatements = GetDropIndexStatements();
			var createStatements = createTableStatements.Concat<IStatement>(createIndexStatements);
			var createDatabaseStatement = new DropTablesAndIndexesStatement(createStatements);
			return createDatabaseStatement;
		}

		private IEnumerable<DropTableStatement> GetDropTableStatements() {
			foreach (var entitySet in tables) {
				var tableStatementBuilder = new DropTableStatementBuilder(entitySet);
				yield return tableStatementBuilder.BuildStatement();
			}
		}

		private IEnumerable<DropIndexStatement> GetDropIndexStatements() {
			foreach (var entitySet in indexes) {
				var indexStatementBuilder = new DropIndexStatementBuilder(entitySet);
				yield return indexStatementBuilder.BuildStatement();
			}
		}
	}
}