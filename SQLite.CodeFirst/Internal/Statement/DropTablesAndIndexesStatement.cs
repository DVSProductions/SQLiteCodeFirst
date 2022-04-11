using System;
using System.Collections.Generic;
using System.Linq;
namespace SQLite.CodeFirst.Statement {
	internal class DropTablesAndIndexesStatement : ACollectionStatement {
		private const string StatementSeperator = "\r\n";

		public DropTablesAndIndexesStatement() { }

		public DropTablesAndIndexesStatement(IEnumerable<IStatement> statements) {
			foreach (var statement in statements) {
				Add(statement);
			}
		}

		public override string CreateStatement() => String.Join(StatementSeperator, this.Select(c => c.CreateStatement()));
	}
}
