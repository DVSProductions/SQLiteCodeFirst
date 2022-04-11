using System;

namespace SQLite.CodeFirst
{
    public interface IHistory
    {
        int Id { get; set; }
        string Hash { get; set; }
        string Context { get; set; }
        DateTime CreateDate { get; set; }
    }
    public interface IHistorySmart
    {
        int Id { get; set; }
        bool IsTable { get; set; }
        string Name { get; set; }
        string Hash { get; set; }
        string Context { get; set; }
        DateTime CreateDate { get; set; }
    }
}