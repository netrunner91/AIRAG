using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;
using Npgsql;
using System.ComponentModel;
using System.Globalization;

#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0010

// --- CONFIG ---
string connectionString = "Host=localhost;Port=7777;Database=postgres;Username=postgres;Password=password";
string ollamaUrl = "http://localhost:11434/v1/";

// 1. PRZYGOTOWANIE BAZY (Standardowe API Npgsql)
await using var dataSource = NpgsqlDataSource.Create(connectionString);

await using (var conn = await dataSource.OpenConnectionAsync())
{
    // Tworzenie rozszerzenia
    await using (var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector;", conn))
    {
        await cmd.ExecuteNonQueryAsync();
    }

    // Tworzenie tabeli
    await using (var cmd = new NpgsqlCommand("CREATE TABLE IF NOT EXISTS salary_data (id serial PRIMARY KEY, content text, embedding vector(384));", conn))
    {
        await cmd.ExecuteNonQueryAsync();
    }
}

// 2. SETUP SEMANTIC KERNEL
var builder = Kernel.CreateBuilder();
var httpClient = new HttpClient { BaseAddress = new Uri(ollamaUrl), Timeout = TimeSpan.FromMinutes(5) };

builder.AddOpenAIChatCompletion("llama3.1", "no-key", httpClient: httpClient);
builder.AddOpenAITextEmbeddingGeneration("all-minilm", "no-key", httpClient: httpClient);

builder.Plugins.AddFromType<SalaryCalculatorPlugin>();

var kernel = builder.Build();
var embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();

// 3. SEEDING (Zasilanie bazy wiedzy)
string[] facts = [
    "Średnia pensja programisty .NET to 18 000 PLN.",
    "Ekspert AWS zarabia 1000 PLN za godzinę.",
    "Mediana zarobków w IT w 2024 to 15 000 PLN."
];

await using (var conn = await dataSource.OpenConnectionAsync())
{
    long count = 0;
    await using (var cmd = new NpgsqlCommand("SELECT count(*) FROM salary_data", conn))
    {
        count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
    }

    if (count == 0)
    {
        Console.WriteLine("Zasilanie bazy wiedzy...");
        foreach (var fact in facts)
        {
            var vectorMemory = await embeddingService.GenerateEmbeddingAsync(fact);
            string vectorString = "[" + string.Join(",", vectorMemory.ToArray().Select(f => f.ToString(CultureInfo.InvariantCulture))) + "]";

            await using (var cmd = new NpgsqlCommand("INSERT INTO salary_data (content, embedding) VALUES (@c, CAST(@e as vector))", conn))
            {
                cmd.Parameters.AddWithValue("c", fact);
                cmd.Parameters.AddWithValue("e", vectorString);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}

// 4. PĘTLA CZATU
var chatService = kernel.GetRequiredService<IChatCompletionService>();

// Bardzo ważne: Jasna instrukcja systemowa dla modelu Llama
var chatHistory = new ChatHistory("Jesteś pomocnym asystentem kadrowym. " +
                                  "Jeśli użytkownik prosi o obliczenia, MUSISZ użyć dostępnych narzędzi (funkcji). " +
                                  "Po otrzymaniu wyniku z funkcji, przedstaw go użytkownikowi w czytelny sposób.");

Console.WriteLine("\n--- AI Salary Assistant (Stable .NET 8) ---");
Console.WriteLine("Spróbuj: 'Oblicz moją pensję: 160h po 120zł'");

while (true)
{
    Console.Write("\nUżytkownik: ");
    string input = Console.ReadLine() ?? "";
    if (string.IsNullOrWhiteSpace(input)) break;

    // --- RAG: Szukanie kontekstu ---
    var queryVectorMemory = await embeddingService.GenerateEmbeddingAsync(input);
    string queryVectorString = "[" + string.Join(",", queryVectorMemory.ToArray().Select(f => f.ToString(CultureInfo.InvariantCulture))) + "]";
    string context = "";
    await using (var conn = await dataSource.OpenConnectionAsync())
    {
        await using (var cmd = new NpgsqlCommand("SELECT content FROM salary_data ORDER BY embedding <=> CAST(@v as vector) LIMIT 1", conn))
        {
            cmd.Parameters.AddWithValue("v", queryVectorString);
            context = (await cmd.ExecuteScalarAsync())?.ToString() ?? "";
        }
    }

    // Dodajemy pytanie użytkownika do historii
    chatHistory.AddUserMessage($"Kontekst: {context}\n\nPytanie: {input}");

    // --- KLUCZOWA ZMIANA: Konfiguracja automatycznego wywołania ---
    var settings = new OpenAIPromptExecutionSettings
    {
        // Wymuszamy na modelu użycie funkcji i automatyczne wykonanie kodu C#
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
    };

    try
    {
        // WAŻNE: Przekazujemy 'kernel' jako trzeci parametr! 
        // Bez tego chatService nie będzie miał dostępu do zarejestrowanych pluginów.
        var result = await chatService.GetChatMessageContentAsync(chatHistory, settings, kernel);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Asystent: {result}");
        Console.ResetColor();

        chatHistory.AddAssistantMessage(result.ToString());
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Błąd podczas wywoływania funkcji: {ex.Message}");
    }
}
// --- PLUGINS ---
public class SalaryCalculatorPlugin
{
    [KernelFunction, Description("Oblicza wynagrodzenie brutto na podstawie godzin i stawki.")]
    public string Oblicz(double godziny, double stawka) => (godziny * stawka).ToString("N2") + " PLN";
}