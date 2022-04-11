using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Diagnostics;

namespace SQLite.CodeFirst {
	/// <summary>
	/// Creates a SQLite-Database based on a Entity Framework <see cref="Database"/> and <see cref="DbModel"/>.
	/// This creator can be used standalone or within an initializer.
	/// <remark>
	/// The generated DDL-SQL will be executed together as one statement.
	/// If there is a open transaction on the Database, the statement will be executed within this transaction.
	/// Otherwise it will be executed within a own transaction. In anyway the atomicity is guaranteed.
	/// </remark>
	/// </summary>
	public class SqliteDatabaseUpgrader {
		public Collation DefaultCollation { get; }
		public SqliteDatabaseUpgrader(Collation defaultCollation = null) {
			DefaultCollation = defaultCollation;
		}

		/// <summary>
		/// Creates the SQLite-Database.
		/// </summary>
		public void Upgrade(Database db, DbModel model, IEnumerable<string> removeTables, IEnumerable<string> removeIndexes, IEnumerable<string> addTables) {
			if (db == null)
				throw new ArgumentNullException("db");
			if (model == null)
				throw new ArgumentNullException("model");
			//Step one: Remove old tables and indexes!
			var sqliteSqlGenerator = new SqliteSqlGenerator(DefaultCollation);
			string sql = sqliteSqlGenerator.GeneratePrepareUpgrade(removeTables, removeIndexes);
			Debug.Write(sql);
			if (!string.IsNullOrWhiteSpace(sql))
				db.ExecuteSqlCommand(TransactionalBehavior.EnsureTransaction, sql);
			//Step two: Add new tables and indexes
			sql = sqliteSqlGenerator.GenerateFinalizeUpgrade(model.StoreModel, addTables);
			Debug.Write(sql);
			db.ExecuteSqlCommand(TransactionalBehavior.EnsureTransaction, sql);
			//done! (hopefully this should upgrade everything smoothly)(ish)
		}
	}
}
