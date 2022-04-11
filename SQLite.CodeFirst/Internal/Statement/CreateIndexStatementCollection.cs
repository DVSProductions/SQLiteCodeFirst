using System;
using System.Collections.Generic;
using System.Linq;

namespace SQLite.CodeFirst.Statement {
	internal class CreateIndexStatementCollection : ACollectionStatement {
		private const string StatementSeperator = "\r\n";

		public CreateIndexStatementCollection(IEnumerable<CreateIndexStatement> createIndexStatements) {
			foreach (var createIndexStatement in createIndexStatements) {
				Add(createIndexStatement);
			}
		}

		public override string CreateStatement() => String.Join(StatementSeperator, this.Select(e => e.CreateStatement()));
	}
}
