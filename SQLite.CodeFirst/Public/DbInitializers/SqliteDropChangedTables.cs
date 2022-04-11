using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using SQLite.CodeFirst.Utility;
using System.Diagnostics.CodeAnalysis;
using SQLite.CodeFirst.Statement;

namespace SQLite.CodeFirst {
	/// <summary>
	/// An implementation of <see cref="IDatabaseInitializer{TContext}"/> that will always recreate and optionally re-seed the 
	/// database the first time that a context is used in the app domain or if the model has changed. 
	/// To seed the database, create a derived class and override the Seed method.
	/// <remarks>
	/// To detect model changes a new table (implementation of <see cref="IHistory"/>) is added to the database.
	/// There is one record in this table which holds the hash of the SQL-statement which was generated from the model
	/// executed to create the database. When initializing the database the initializer checks if the hash of the SQL-statement for the
	/// model is still the same as the hash in the database.  If you use this initializer on a existing database, this initializer 
	/// will interpret this as model change because of the new <see cref="IHistory"/> table.
	/// Notice that a database can be used by more than one context. Therefore the name of the context is saved as a part of the history record.
	/// </remarks>
	/// </summary>
	/// <typeparam name="TContext">The type of the context.</typeparam>
	public class SqliteDropChangedTables<TContext> : SqliteInitializerBase<TContext>
		where TContext : DbContext {
		private readonly Type historyEntityType;
		private enum HistoryType {
			Table, Index
		}
		private struct Name : IEquatable<Name> {
			public readonly string name;
			public readonly HistoryType type;
			public Name(string name, HistoryType type) {
				this.name = name;
				this.type = type;
			}
			public bool IsTable() => type == HistoryType.Table;
			public static Name FromIHistorySmart(IHistorySmart entry) => entry.IsTable ? Table(entry.Name) : Index(entry.Name);
			public static Name IsTable(bool isTable, string name) => isTable ? Table(name) : Index(name);
			public static Name Table(string name) => new Name(name, HistoryType.Table);
			public static Name Index(string name) => new Name(name, HistoryType.Index);
			public override bool Equals(object obj) => obj is Name name && Equals(name);
			public bool Equals(Name other) => name == other.name && type == other.type;

			public override int GetHashCode() {
				var hashCode = 1725085987;
				hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(name);
				hashCode = hashCode * -1521134295 + type.GetHashCode();
				return hashCode;
			}

			public static bool operator ==(Name left, Name right) => left.Equals(right);
			public static bool operator !=(Name left, Name right) => !(left == right);
		}
		private struct NameAndHash {
			public Name Name;
			public string Hash;
			public NameAndHash(Name name, string hash) {
				Name = name;
				Hash = hash;
			}
		}
		private struct NameAndSQL {
			public Name Name;
			public string SQL;
			public NameAndSQL(Name name, string sql) {
				Name = name;
				SQL = sql;
			}
			public static NameAndSQL Table(string name, string sql) => new NameAndSQL(new Name(name, HistoryType.Table), sql);
			public static NameAndSQL Index(string name, string sql) => new NameAndSQL(new Name(name, HistoryType.Index), sql);
		}
		/// <summary>
		/// Initializes a new instance of the <see cref="SqliteDropChangedTables{TContext}"/> class.
		/// </summary>
		/// <param name="modelBuilder">The model builder.</param>
		public SqliteDropChangedTables(DbModelBuilder modelBuilder)
			: base(modelBuilder) {
			historyEntityType = typeof(HistorySmart);
			ConfigureHistoryEntity();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SqliteDropChangedTables{TContext}"/> class.
		/// </summary>
		/// <param name="modelBuilder">The model builder.</param>
		/// <param name="historyEntityType">Type of the history entity (must implement <see cref="IHistory"/> and provide an parameterless constructor).</param>
		public SqliteDropChangedTables(DbModelBuilder modelBuilder, Type historyEntityType)
			: base(modelBuilder) {
			this.historyEntityType = historyEntityType;
			ConfigureHistoryEntity();
		}


		protected void ConfigureHistoryEntity() {
			HistoryEntityTypeValidator.EnsureValidType(historyEntityType);
			ModelBuilder.RegisterEntityType(historyEntityType);
		}

		/// <summary>
		/// Initialize the database for the given context.
		/// Generates the SQLite-DDL from the model and executes it against the database.
		/// After that the <see cref="Seed" /> method is executed.
		/// All actions are be executed in transactions.
		/// </summary>
		/// <param name="context">The context.</param>
		public override void InitializeDatabase(TContext context) {
			var databaseFilePath = GetDatabasePathFromContext(context);

			var dbExists = InMemoryAwareFile.Exists(databaseFilePath);
			if (dbExists) {
				if (IsSameModel(context)) {
					return;
				}
				else {//perform upgrade

					UpgradeDatabase(context);
					//FileAttributes? attributes = InMemoryAwareFile.GetFileAttributes(databaseFilePath);
					//CloseDatabase(context);
					//DeleteDatabase(context, databaseFilePath);
					//base.InitializeDatabase(context);
					//InMemoryAwareFile.SetFileAttributes(databaseFilePath, attributes);
					SaveHistory(context);
				}
			}
			else {
				base.InitializeDatabase(context);
				SaveHistory(context);
			}
		}

		/// <summary>
		/// Called to drop/remove Database file from disk.
		/// </summary>
		/// <param name="context">The context.</param>
		/// <param name="databaseFilePath">Filename of Database to be removed.</param>
		protected virtual void DeleteDatabase(TContext context, string databaseFilePath) {
			InMemoryAwareFile.Delete(databaseFilePath);
		}

		[SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.GC.Collect", Justification = "Required.")]
		private static void CloseDatabase(TContext context) {
			context.Database.Connection.Close();
			GC.Collect();
			GC.WaitForPendingFinalizers();
		}
		/// <summary>
		/// deletes missing tables and replaces them with new ones
		/// </summary>
		/// <param name="context"></param>
		private void UpgradeDatabase(TContext context) {
			var tablesToRemove = new List<string>();
			var indexesToRemove = new List<string>();
			var tablesToCreate = new List<string>();
			var currentModel = GetHashFromModelDict(context.Database.Connection);
			var oldModel = BuildHistoryDictionary(GetHistoryRecords(context));
			//compare history entries to actual model
			foreach (var entry in oldModel) {
				string hash;
				if (!currentModel.TryGetValue(entry.Key, out hash)) {  //the table has been deleted
					(entry.Key.IsTable() ? tablesToRemove : indexesToRemove).Add(entry.Key.name);
				}
				else if (entry.Value.Hash != hash) {//the table has been modified
					if (entry.Key.IsTable()) {
						tablesToRemove.Add(entry.Key.name);
						tablesToCreate.Add(entry.Key.name);
					}
					else
						indexesToRemove.Add(entry.Key.name);
				}
			}
			//add tables that are not in the database
			foreach (var table in currentModel.Keys) {
				if (table.IsTable() && !oldModel.ContainsKey(table)) //the table is new and an actual table not an index (indexes will be automatically generated)
					tablesToCreate.Add(table.name);
			}
			//let's start upgrading!
			var model = ModelBuilder.Build(context.Database.Connection);
			var sqliteDatabaseUpgrader = new SqliteDatabaseUpgrader(DefaultCollation);
			sqliteDatabaseUpgrader.Upgrade(context.Database, model, tablesToRemove, indexesToRemove, tablesToCreate);

		}
		//private void SaveHistory(TContext context) {
		//	var hash = GetHashFromModel(context.Database.Connection);
		//	var history = GetHistoryRecord(context);
		//	EntityState entityState;
		//	if (history == null) {
		//		history = (IHistory)Activator.CreateInstance(historyEntityType);
		//		entityState = EntityState.Added;
		//	}
		//	else {
		//		entityState = EntityState.Modified;
		//	}

		//	history.Context = context.GetType().FullName;
		//	history.Hash = hash;
		//	history.CreateDate = DateTime.UtcNow;

		//	context.Set(historyEntityType).Attach(history);
		//	context.Entry(history).State = entityState;
		//	context.SaveChanges();
		//}
		private void SaveHistory(TContext context) {
			var currentModelHashes = GetHashFromModelDict(context.Database.Connection);
			var oldModel = BuildHistoryDictionary(GetHistoryRecords(context));
			var historyEntries = new Dictionary<IHistorySmart, EntityState>();
			var historySet = context.Set(historyEntityType);
			var contextName = context.GetType().FullName;
			if (oldModel.Count == 0) {//make new history
				foreach (var table in currentModelHashes) {
					var entry = (IHistorySmart)Activator.CreateInstance(historyEntityType);
					entry.Name = table.Key.name;
					entry.IsTable = table.Key.IsTable();
					entry.Hash = table.Value;
					historyEntries.Add(entry, EntityState.Added);
				}
			}
			else {//update old history
				var toRemove = new HashSet<Name>();
				var toEdit = new HashSet<Name>();
				//compare history entries to actual model
				foreach (var entry in oldModel.Values) {
					string hash;
					var name = Name.FromIHistorySmart(entry);
					if (!currentModelHashes.TryGetValue(name, out hash)) //the table has been deleted
						toRemove.Add(name);
					else if (entry.Hash != hash) //the table has been modified
						toEdit.Add(name);
				}
				{//remove entries
					var start = historySet.AsNoTracking();
					foreach (var entry in Enumerable.Cast<IHistorySmart>(start).ToList()) {//make sure this is independent of the real list so the iterator doesn't break
						if (entry.Context == contextName) {
							var entryname = Name.FromIHistorySmart(entry);
							if (toRemove.Contains(entryname))
								historySet.Remove(entry);
							if (toEdit.Contains(entryname)) {
								var real = historySet.Find(entry.Id) as IHistorySmart;
								real.Hash = currentModelHashes[entryname];//update hash
								context.Entry(real).State = EntityState.Modified;
							}
						}
					}
				}
				{//add tables that are not in the database
					foreach (var table in currentModelHashes) {
						if (!oldModel.ContainsKey(table.Key)) {
							var entry = (IHistorySmart)Activator.CreateInstance(historyEntityType);
							entry.Name = table.Key.name;
							entry.IsTable = table.Key.IsTable();
							entry.Hash = table.Value;
							historyEntries.Add(entry, EntityState.Added);
						}
					}
				}
			}
			foreach (var pairs in historyEntries) {
				var entry = pairs.Key;
				entry.Context = contextName;
				entry.CreateDate = DateTime.UtcNow;

				historySet.Attach(entry);
				context.Entry(entry).State = pairs.Value;
			}
			context.SaveChanges();
		}

		[SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
		private bool IsSameModel(TContext context) {

			var hashes = GetHashFromModelTablewise(context.Database.Connection);

			try {
				var history = BuildHistoryDictionary(GetHistoryRecords(context));
				foreach (var entry in hashes) {
					if (!history.ContainsKey(entry.Name) || history[entry.Name].Hash != entry.Hash) {
						return false;//the table doesn't exist or the hash is different!
					}
				}
				return true;
			}
			catch (Exception) {
				// This happens if the history table does not exist.
				// So it covers also the case with a null byte file (see SqliteCreateDatabaseIfNotExists).
				return false;
			}
		}
		/// <summary>
		/// Filters out all tables that do not belong to this and builds a nice dictionary of hashes for tables
		/// </summary>
		private Dictionary<Name, IHistorySmart> BuildHistoryDictionary(IEnumerable<IHistorySmart> historySmarts) {
			var ret = new Dictionary<Name, IHistorySmart>();
			foreach (var historySmart in historySmarts) {
				ret.Add(Name.FromIHistorySmart(historySmart), historySmart);
			}
			return ret;
		}
		//private IHistory GetHistoryRecord(TContext context) {
		//	// Yes, it seams to be complicated but it has to be done this way
		//	// in order to be supported by .NET 4.0.
		//	DbQuery dbQuery = context.Set(historyEntityType).AsNoTracking();
		//	var records = Enumerable.Cast<IHistory>(dbQuery);
		//	return records.SingleOrDefault();
		//}
		private IEnumerable<IHistorySmart> GetHistoryRecords(TContext context) {
			// Yes, it seams to be complicated but it has to be done this way
			// in order to be supported by .NET 4.0.
			var contextName = context.GetType().FullName;
			var dbQuery = context.Set(historyEntityType).AsNoTracking();
			var uniquenessGuarantee = new HashSet<Name>();
			var queryResult = Enumerable.Cast<IHistorySmart>(dbQuery);
			if (queryResult != null) {
				try {
					queryResult.First();//check if the query works
				}
				catch {
					yield break;
				}
				foreach (var entry in queryResult) {
					if (entry.Context == contextName) {
						var name = Name.FromIHistorySmart(entry);
						if (!uniquenessGuarantee.Contains(name)) {
							yield return entry;
							uniquenessGuarantee.Add(name);
						}
					}
				}
			}
		}
		//private string GetHashFromModel(DbConnection connection) {
		//	var sql = GetSqlFromModel(connection);
		//	var hash = HashCreator.CreateHash(sql);
		//	return hash;
		//}
		private IEnumerable<NameAndHash> GetHashFromModelTablewise(DbConnection connection) {
			foreach (var table in GetSqlFromModelTablewise(connection)) {
				yield return new NameAndHash(table.Name, HashCreator.CreateHash(table.SQL));
			}
		}
		private Dictionary<Name, string> GetHashFromModelDict(DbConnection connection) {
			var dict = new Dictionary<Name, string>();
			foreach (var table in GetSqlFromModelTablewise(connection)) {
				dict.Add(table.Name, HashCreator.CreateHash(table.SQL));
			}
			return dict;
		}

		private string GetSqlFromModel(DbConnection connection) {
			var model = ModelBuilder.Build(connection);
			var sqliteSqlGenerator = new SqliteSqlGenerator(DefaultCollation);
			return sqliteSqlGenerator.Generate(model.StoreModel);
		}
		private IEnumerable<NameAndSQL> GetSqlFromModelTablewise(DbConnection connection) {
			var model = ModelBuilder.Build(connection);
			var sqliteSqlGenerator = new SqliteSqlGenerator(DefaultCollation);
			foreach (var gen in sqliteSqlGenerator.GenerateIndividually(model.StoreModel)) {
				if (gen is CreateTableStatement) {
					yield return NameAndSQL.Table((gen as CreateTableStatement).TableName, gen.CreateStatement());
				}
				else if (gen is CreateIndexStatement) {
					yield return NameAndSQL.Index((gen as CreateIndexStatement).Name, gen.CreateStatement());
				}
			}
		}
	}
}
