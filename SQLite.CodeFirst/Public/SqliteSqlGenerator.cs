using System.Collections.Generic;
using System.Data.Entity.Core.Metadata.Edm;
using SQLite.CodeFirst.Builder;
using SQLite.CodeFirst.Statement;

namespace SQLite.CodeFirst {
	/// <summary>
	/// Generates the SQL statement to create a database, based on a <see cref="EdmModel"/>.
	/// </summary>
	public class SqliteSqlGenerator : ISqlGenerator {
		public SqliteSqlGenerator(Collation defaultCollation = null) {
			DefaultCollation = defaultCollation;
		}

		public Collation DefaultCollation { get; }

		/// <summary>
		/// Generates the SQL statement, based on the <see cref="EdmModel"/>.
		/// </summary>
		public string Generate(EdmModel storeModel) {
			var statementBuilder = new CreateDatabaseStatementBuilder(storeModel, DefaultCollation);
			var statement = statementBuilder.BuildStatement();
			return statement.CreateStatement();
		}
		public ACollectionStatement GenerateIndividually(EdmModel storeModel) {
			var statementBuilder = new CreateDatabaseStatementBuilder(storeModel, DefaultCollation);
			return statementBuilder.BuildStatement();
		}
		public string GeneratePrepareUpgrade(IEnumerable<string> tableNames, IEnumerable<string> indexes) {
			var statementBuilder = new PrepareDatabaseUpdateStatementBuilder(tableNames, indexes);
			return statementBuilder.BuildStatement().CreateStatement();
		}
		public string GenerateFinalizeUpgrade(EdmModel storeModel, IEnumerable<string> tableNames) {
			var statementBuilder = new FinalizeDatabaseUpgradeStatementBuilder(storeModel, DefaultCollation, tableNames);
			return statementBuilder.BuildStatement().CreateStatement();
		}
	}
}
