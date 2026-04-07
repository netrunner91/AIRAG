using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0010

// --- CONFIGURATION ---
const string ConnectionString = "Host=localhost;Port=7777;Database=postgres;Username=postgres;Password=password";
const string OllamaUrl = "http://localhost:11434/v1/";

// --- MAIN ENTRY POINT ---
var dbManager = new DatabaseManager(ConnectionString);
await dbManager.InitializeSchemaAsync();

var builder = Kernel.CreateBuilder();
var httpClient = new HttpClient { BaseAddress = new Uri(OllamaUrl), Timeout = TimeSpan.FromMinutes(5) };

builder.AddOpenAIChatCompletion("llama3.1", "no-key", httpClient: httpClient);
builder.AddOpenAITextEmbeddingGeneration("all-minilm", "no-key", httpClient: httpClient);
builder.Plugins.AddFromType<SalaryCalculatorPlugin>();

var kernel = builder.Build();

var knowledgeBase = new KnowledgeBaseService(dbManager.DataSource, kernel.GetRequiredService<ITextEmbeddingGenerationService>());
await knowledgeBase.SeedInitialDataAsync();

var assistant = new ChatAssistant(kernel, knowledgeBase);
await assistant.RunAsync();
