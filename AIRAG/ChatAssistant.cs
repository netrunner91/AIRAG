using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

public class ChatAssistant(Kernel kernel, KnowledgeBaseService knowledgeBase)
{
    public async Task RunAsync()
    {
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory(
        "You are a professional HR assistant. " +
        "When you need to use a tool, output ONLY the tool call. " +
        "DO NOT explain what you are doing. DO NOT write JSON as text. " +
        "If you don't have enough data for a tool, ask the user for it.");

        Console.WriteLine("\n--- AI Salary Assistant Ready ---");

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("\nUser: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) break;

            // 1. RAG Search with Distance
            var (context, distance) = await knowledgeBase.GetRelevantContextAsync(input);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[RAG Debug] Nearest match distance: {distance:F4}");

            // 2. Threshold Logic
            var effectiveContext = distance <= 0.5 ? context : "No relevant company data found for this specific query.";

            if (distance <= 0.5)
                Console.WriteLine($"[RAG Debug] Context used: {effectiveContext}");
            else
                Console.WriteLine("[RAG Debug] Distance above threshold. Using general knowledge.");

            // 3. Chat Preparation
            chatHistory.AddUserMessage($"Knowledge Base Context: {effectiveContext}\nUser Question: {input}");

            var settings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            try
            {
                var response = await chatService.GetChatMessageContentAsync(chatHistory, settings, kernel);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Assistant: {response}");
                chatHistory.AddAssistantMessage(response.ToString());
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
