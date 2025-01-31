﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace SQLite.CodeFirst.Statement {
	internal class CreateDatabaseStatement : ACollectionStatement {
		private const string StatementSeperator = "\r\n";

		public CreateDatabaseStatement() { }

		public CreateDatabaseStatement(IEnumerable<IStatement> statements) {
			foreach (var statement in statements) {
				Add(statement);
			}
		}

		public override string CreateStatement() => String.Join(StatementSeperator, this.Select(c => c.CreateStatement()));
	}
}
