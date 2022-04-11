using System.Collections.ObjectModel;

namespace SQLite.CodeFirst.Statement {
	public abstract class ACollectionStatement : Collection<IStatement>, IStatement {
		public abstract string CreateStatement();
	}
}