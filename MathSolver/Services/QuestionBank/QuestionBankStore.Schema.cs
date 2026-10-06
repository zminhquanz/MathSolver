using SQLite;

namespace MathSolver.Services.QuestionBank;

public sealed partial class QuestionBankStore
{
    private static void RenameProblemColumns(SQLiteConnection db)
    {
        // Rename before CreateTable, which otherwise adds empty replacement
        // columns. SQLite preserves values, row keys, indexes and triggers.
        db.RunInTransaction(() =>
        {
            var names = db.GetTableInfo("BasicQuestionBank").Select(column => column.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Rename("Family", "ProblemType");
            Rename("UnknownRole", "ProblemVariant");

            void Rename(string oldName, string newName)
            {
                if (!names.Contains(oldName) || names.Contains(newName)) return;
                db.Execute($"ALTER TABLE BasicQuestionBank RENAME COLUMN \"{oldName}\" TO \"{newName}\"");
            }
        });
    }
}
