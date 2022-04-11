using System.Collections.Generic;
using System.Data.Entity.Core.Metadata.Edm;
using System.Linq;
using SQLite.CodeFirst.Builder.NameCreators;
using SQLite.CodeFirst.Statement;
using SQLite.CodeFirst.Utility;

namespace SQLite.CodeFirst.Builder {
	internal class FinalizeDatabaseUpgradeStatementBuilder : CreateDatabaseStatementBuilder {
		private readonly HashSet<string> tables;

		public FinalizeDatabaseUpgradeStatementBuilder(EdmModel edmModel, Collation defaultCollation, IEnumerable<string> tables) : base(edmModel, defaultCollation) {
			this.tables = new HashSet<string>(tables);
		}
		public new CreateDatabaseStatement BuildStatement() {
			var createTableStatements = GetCreateTableStatements();
			var createIndexStatements = GetCreateIndexStatements();
			var createStatements = createTableStatements.Concat<IStatement>(createIndexStatements);
			var createDatabaseStatement = new CreateDatabaseStatement(createStatements);
			return createDatabaseStatement;
		}
		protected new IEnumerable<CreateTableStatement> GetCreateTableStatements() {
			var associationTypeContainer = new AssociationTypeContainer(edmModel.AssociationTypes, edmModel.Container);

			foreach (var entitySet in edmModel.Container.EntitySets) {
				if (tables.Contains(NameCreator.EscapeName(entitySet.Table))) {
					var tableStatementBuilder = new CreateTableStatementBuilder(entitySet, associationTypeContainer, defaultCollation);
					yield return tableStatementBuilder.BuildStatement();
				}
			}
		}

		protected new IEnumerable<CreateIndexStatementCollection> GetCreateIndexStatements() {
			foreach (var entitySet in edmModel.Container.EntitySets) {
				if (tables.Contains(NameCreator.EscapeName(entitySet.Table))) {
					var indexStatementBuilder = new CreateIndexStatementBuilder(entitySet);
					yield return indexStatementBuilder.BuildStatement();
				}
			}
		}
	}
}