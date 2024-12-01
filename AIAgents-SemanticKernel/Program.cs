//using Microsoft.SemanticKernel;
//using Microsoft.SemanticKernel.Agents;
//using Microsoft.SemanticKernel.ChatCompletion;
//using System.Text;
//using System.Threading.Tasks;

//#pragma warning disable SKEXP0001
//#pragma warning disable SKEXP0110
//#pragma warning disable SKEXP0050
//#pragma warning disable SKEXP0070

//const string azureOpenAiEndpoint = "https://reza-openai-agents.openai.azure.com/";
//const string azureOpenAiApiKey = ""; // Todo - Key vault
//const string modelDeploymentName = "gpt-4o-mini";

//// Initialize Kernel
//var builder = Kernel.CreateBuilder();
//builder.AddAzureOpenAIChatCompletion(modelDeploymentName, azureOpenAiEndpoint, azureOpenAiApiKey);
//Kernel kernel = builder.Build();

//// Define Agents
//var smith = new ChatCompletionAgent
//{
//    Kernel = kernel,
//    Name = "Smith",
//    Instructions = "You're Agent Smith, the secretary. You greet the user, decide which agent (Neo, CodeMaster, MovieGuru, or GeneralBot) will handle the question, and refer the question. Respond briefly and politely."
//};

//var neo = new ChatCompletionAgent
//{
//    Kernel = kernel,
//    Name = "Neo",
//    Instructions = "You're Agent Neo, specializing in relationships and life questions. Answer thoroughly and provide meaningful advice. Answer less than 3 lines."
//};

//var codeMaster = new ChatCompletionAgent
//{
//    Kernel = kernel,
//    Name = "CodeMaster",
//    Instructions = "You're Agent CodeMaster, specializing in computer programming. Provide detailed and accurate technical responses. Answer less than 3 lines."
//};

//var movieGuru = new ChatCompletionAgent
//{
//    Kernel = kernel,
//    Name = "MovieGuru",
//    Instructions = "You're Agent MovieGuru, an expert in movies and TV shows. Share insights, recommendations, and trivia. Answer less than 3 lines."
//};

//var generalBot = new ChatCompletionAgent
//{
//    Kernel = kernel,
//    Name = "Generalist",
//    Instructions = "You're Generalist, handling all general questions that do not fit into other categories. Answer clearly and concisely. Answer less than 3 lines."
//};

//var history = new ChatHistory();
//history.AddUserMessage("The user is called Reza");

//Console.OutputEncoding = Encoding.UTF8;

//// Main Loop
//while (true)
//{
//    Console.Write("> ");
//    var question = Console.ReadLine() ?? "";
//    if (string.IsNullOrWhiteSpace(question)) continue;

//    history.AddUserMessage(question);

//    // Agent Smith greets and decides the agent
//    Console.WriteLine("Smith: Hello! My name is Agent Smith.");
//    ChatCompletionAgent selectedAgent = DetermineAgent(question, neo, codeMaster, movieGuru, generalBot);
//    Console.WriteLine($"Smith: I'll refer your question to {selectedAgent.Name}.");

//    // Wait 3 seconds before switching to the selected agent
//    await Task.Delay(3000);

//    // Selected agent answers the question
//    Console.Write($"{selectedAgent.Name}: ");
//    await foreach (var response in selectedAgent.InvokeStreamingAsync(history))
//    {
//        Console.Write(response.Content);
//    }

//    Console.WriteLine();
//    Console.WriteLine("*********************");
//}

//// Function to determine the appropriate agent
//static ChatCompletionAgent DetermineAgent(string question, ChatCompletionAgent neo, ChatCompletionAgent codeMaster, ChatCompletionAgent movieGuru, ChatCompletionAgent generalBot)
//{
//    if (question.Contains("relationship", StringComparison.OrdinalIgnoreCase) ||
//        question.Contains("love", StringComparison.OrdinalIgnoreCase) ||
//        question.Contains("friend", StringComparison.OrdinalIgnoreCase))
//    {
//        return neo;
//    }
//    else if (question.Contains("code", StringComparison.OrdinalIgnoreCase) ||
//             question.Contains("programming", StringComparison.OrdinalIgnoreCase) ||
//             question.Contains("error", StringComparison.OrdinalIgnoreCase))
//    {
//        return codeMaster;
//    }
//    else if (question.Contains("movie", StringComparison.OrdinalIgnoreCase) ||
//             question.Contains("series", StringComparison.OrdinalIgnoreCase) ||
//             question.Contains("film", StringComparison.OrdinalIgnoreCase))
//    {
//        return movieGuru;
//    }
//    else
//    {
//        return generalBot;
//    }
//}
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using HelloPlugin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Plugins.Core;
using AIAgents_SemanticKernel;
//using Shared;

#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0110
#pragma warning disable SKEXP0050
#pragma warning disable SKEXP0070

public class RateLimitHandler
{
    // Existing RateLimitHandler implementation remains the same
    private DateTime? _nextAllowedRequest;
    private readonly object _lockObject = new object();

    public async Task<T> ExecuteWithRateLimitHandling<T>(Func<Task<T>> operation)
    {
        while (true)
        {
            lock (_lockObject)
            {
                if (_nextAllowedRequest == null || DateTime.Now >= _nextAllowedRequest)
                {
                    _nextAllowedRequest = null;
                }
            }

            if (_nextAllowedRequest == null)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex)
                {
                    var match = Regex.Match(ex.Message, @"retry after (\d+) seconds");
                    if (match.Success)
                    {
                        int waitSeconds = int.Parse(match.Groups[1].Value);
                        lock (_lockObject)
                        {
                            _nextAllowedRequest = DateTime.Now.AddSeconds(waitSeconds);
                        }
                        Console.WriteLine($"Rate limited. Waiting {waitSeconds} seconds.");
                        await Task.Delay(TimeSpan.FromSeconds(waitSeconds));
                    }
                    else
                    {
                        throw; // Re-throw if it's not a rate limit error
                    }
                }
            }
            else
            {
                var waitTime = _nextAllowedRequest.Value - DateTime.Now;
                if (waitTime > TimeSpan.Zero)
                {
                    Console.WriteLine($"Waiting {waitTime.TotalSeconds} seconds before next request.");
                    await Task.Delay(waitTime);
                }
            }
        }
    }
}

class Program
{
    const string azureOpenAiEndpoint = "https://reza-openai-agents.openai.azure.com/";
    const string azureOpenAiApiKey = ""; // Todo - Key vault
    const string modelDeploymentName = "gpt-4o-mini";

    static async Task Main()
    {
        // Initialize Kernel
        var builder = Kernel.CreateBuilder();
        builder.AddAzureOpenAIChatCompletion(modelDeploymentName, azureOpenAiEndpoint, azureOpenAiApiKey);
        builder.Services.AddSingleton<IAutoFunctionInvocationFilter, AutoInvocationFilter>();
        Kernel kernel = builder.Build();

        // Import Plugins
        kernel.ImportPluginFromType<TimePlugin>();
        var myFirstPlugin = new FileManager();
        kernel.ImportPluginFromObject(myFirstPlugin);

        // Initialize Rate Limit Handler
        var rateLimitHandler = new RateLimitHandler();

        // Define Agents
        var smith = new ChatCompletionAgent
        {
            Kernel = kernel,
            Name = "Smith",
            Instructions = @"You are Agent Smith, a sophisticated router for a multi-agent system. 
            When a user asks a question, carefully analyze the query and determine the most appropriate agent to handle it.
            You have access to these agents:
            - Neo: Handles relationship and personal life questions
            - CodeMaster: Handles programming and technical questions
            - MovieGuru: Handles movie, TV, and entertainment queries
            - FileManager: Handles file and directory operations
            - GeneralBot: Handles general knowledge questions

            Respond with ONLY the name of the agent that should handle the question. 
            Do NOT provide any additional commentary or explanation.
            The possible responses are EXACTLY: Neo, CodeMaster, MovieGuru, FileManager, GeneralBot",
            Arguments = new KernelArguments(
                new AzureOpenAIPromptExecutionSettings
                {
                    //Temperature = 0.5,
                    //FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                    Temperature = 0.5, // Adjust as needed
                    TopP = 0.95, // Can help with response diversity
                    FrequencyPenalty = 0, // Adjust as needed
                    PresencePenalty = 0, // Adjust as needed
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                })
        };

        var neo = new ChatCompletionAgent
        {
            Kernel = kernel,
            Name = "Neo",
            Instructions = "You're Agent Neo, specializing in relationships and life questions. Answer thoroughly and provide meaningful advice. Answer less than 3 lines.",
            Arguments = new KernelArguments(
                new AzureOpenAIPromptExecutionSettings
                {
                    //Temperature = 0.5,
                    //FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                    Temperature = 0.5, // Adjust as needed
                    TopP = 0.95, // Can help with response diversity
                    FrequencyPenalty = 0, // Adjust as needed
                    PresencePenalty = 0, // Adjust as needed
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                })
        };

        var codeMaster = new ChatCompletionAgent
        {
            Kernel = kernel,
            Name = "CodeMaster",
            Instructions = "You're Agent CodeMaster, specializing in computer programming. Provide detailed and accurate technical responses. Answer less than 3 lines.",
            Arguments = new KernelArguments(
                new AzureOpenAIPromptExecutionSettings
                {
                    //Temperature = 0.5,
                    //FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                    Temperature = 0.5, // Adjust as needed
                    TopP = 0.95, // Can help with response diversity
                    FrequencyPenalty = 0, // Adjust as needed
                    PresencePenalty = 0, // Adjust as needed
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                })
        };

        var movieGuru = new ChatCompletionAgent
        {
            Kernel = kernel,
            Name = "MovieGuru",
            Instructions = "You're Agent MovieGuru, an expert in movies and TV shows. Share insights, recommendations, and trivia. Answer less than 3 lines.",
            Arguments = new KernelArguments(
                new AzureOpenAIPromptExecutionSettings
                {
                    //Temperature = 0.5,
                    //FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                    Temperature = 0.5, // Adjust as needed
                    TopP = 0.95, // Can help with response diversity
                    FrequencyPenalty = 0, // Adjust as needed
                    PresencePenalty = 0, // Adjust as needed
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                })
        };

        var fileManager = new ChatCompletionAgent
        {
            Kernel = kernel,
            Name = "FileManager",
            Instructions = $"You are File Manager that can create and list files and folders. " +
                           $"When you create files and folder you need to give the full path based" +
                           $" on this root folder: {myFirstPlugin.RootFolder}",
            Arguments = new KernelArguments(
                new AzureOpenAIPromptExecutionSettings
                {
                    //Temperature = 0.5,
                    //FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                    Temperature = 0.5, // Adjust as needed
                    TopP = 0.95, // Can help with response diversity
                    FrequencyPenalty = 0, // Adjust as needed
                    PresencePenalty = 0, // Adjust as needed
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                })
        };

        var generalBot = new ChatCompletionAgent
        {
            Kernel = kernel,
            Name = "Generalist",
            Instructions = "You're Generalist, handling all general questions that do not fit into other categories. Answer clearly and concisely. Answer less than 3 lines."
        };

        var history = new ChatHistory();
        history.AddUserMessage("The user is called Reza");

        Console.OutputEncoding = Encoding.UTF8;

        // Main Loop
        while (true)
        {
            Console.Write("> ");
            var question = Console.ReadLine() ?? "";
            if (string.IsNullOrWhiteSpace(question)) continue;

            history.AddUserMessage(question);

            // Agent Smith decides the agent
            Console.WriteLine("Smith: Hello! My name is Agent Smith.");
            var smithHistory = new ChatHistory();
            smithHistory.AddUserMessage(question);

            string selectedAgentName = await rateLimitHandler.ExecuteWithRateLimitHandling(async () =>
            {
                string agentName = "";
                await foreach (var response in smith.InvokeStreamingAsync(smithHistory))
                {
                    agentName += response.Content;
                }
                return agentName.Trim();
            });

            // Select the appropriate agent
            ChatCompletionAgent selectedAgent = selectedAgentName switch
            {
                "Neo" => neo,
                "CodeMaster" => codeMaster,
                "MovieGuru" => movieGuru,
                "FileManager" => fileManager,
                "GeneralBot" => generalBot,
                _ => generalBot // Default to GeneralBot if something unexpected happens
            };

            Console.WriteLine($"Smith: I'll refer your question to {selectedAgent.Name}.");

            // Selected agent answers the question
            Console.Write($"{selectedAgent.Name}: ");
            string agentResponse = await rateLimitHandler.ExecuteWithRateLimitHandling(async () =>
            {
                string response = "";
                await foreach (var chunk in selectedAgent.InvokeStreamingAsync(history))
                {
                    response += chunk.Content;
                    Console.Write(chunk.Content);
                }
                return response;
            });

            Console.WriteLine();
            Console.WriteLine("*********************");
        }
    }
}

