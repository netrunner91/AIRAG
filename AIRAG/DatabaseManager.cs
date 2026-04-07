using Npgsql;
// --- CORE CLASSES ---

public class DatabaseManager(string connectionString)
{
    public NpgsqlDataSource DataSource { get; } = NpgsqlDataSource.Create(connectionString);

    public async Task InitializeSchemaAsync()
    {
        await using var conn = await DataSource.OpenConnectionAsync();

        await using (var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector;", conn))
            await cmd.ExecuteNonQueryAsync();

        await using (var cmd = new NpgsqlCommand(
            "CREATE TABLE IF NOT EXISTS salary_data (id serial PRIMARY KEY, content text, embedding vector(384));", conn))
            await cmd.ExecuteNonQueryAsync();
    }
}
