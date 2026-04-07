using System.Globalization;
using Microsoft.SemanticKernel.Embeddings;
using Npgsql;

#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0010

public class KnowledgeBaseService(NpgsqlDataSource dataSource, ITextEmbeddingGenerationService embeddingService)
{
    public async Task SeedInitialDataAsync()
    {
        await using var conn = await dataSource.OpenConnectionAsync();

        long count = 0;
        await using (var cmd = new NpgsqlCommand("SELECT count(*) FROM salary_data", conn))
            count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);

        if (count == 0)
        {
            Console.WriteLine("Seeding knowledge base...");
            string[] facts = [
                "Average .NET developer salary in Poland is 777 PLN.",
                "AWS Expert earns 780 PLN per hour.",
                "IT salary median in 2024 is 700 PLN."
            ];

            foreach (var fact in facts)
            {
                var vector = await embeddingService.GenerateEmbeddingAsync(fact);
                var vectorString = FormatVector(vector);

                await using var cmd = new NpgsqlCommand(
                    "INSERT INTO salary_data (content, embedding) VALUES (@c, CAST(@e as vector))", conn);
                cmd.Parameters.AddWithValue("c", fact);
                cmd.Parameters.AddWithValue("e", vectorString);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    public async Task<(string Context, double Distance)> GetRelevantContextAsync(string query)
    {
        var queryVector = await embeddingService.GenerateEmbeddingAsync(query);
        var vectorString = FormatVector(queryVector);

        await using var conn = await dataSource.OpenConnectionAsync();
        // Use <=> for cosine distance
        await using var cmd = new NpgsqlCommand(
            "SELECT content, (embedding <=> CAST(@v as vector)) as distance FROM salary_data ORDER BY distance LIMIT 1", conn);
        cmd.Parameters.AddWithValue("v", vectorString);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return (reader.GetString(0), reader.GetDouble(1));
        }

        return ("No context found", 1.0);
    }

    private static string FormatVector(ReadOnlyMemory<float> vector)
    {
        return "[" + string.Join(",", vector.ToArray().Select(f => f.ToString(CultureInfo.InvariantCulture))) + "]";
    }
}
