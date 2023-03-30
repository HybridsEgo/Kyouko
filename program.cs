using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using OpenAI_API;
using OpenAI_API.Completions;

public class Program
{
    private DiscordSocketClient _client;
    private string _memoryFilePath;

    public static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

    public async Task MainAsync()
    {
        string apiKeys = "keys.json";
        if (!File.Exists(apiKeys))
        {
            Console.WriteLine("File not found.");
            File.Create(apiKeys).Close();
        }

        JObject json = JObject.Parse(await File.ReadAllTextAsync(apiKeys));
        JToken discordToken = json["discordToken"];

        string filename = "trainingdata.json";
        if (!File.Exists(filename))
        {
            Console.WriteLine("File not found.");
            File.Create(filename).Close();
        }

        // Set up memory file path
        _memoryFilePath = "user_memory.txt";

        // Set up Discord bot client
        _client = new DiscordSocketClient();
        _client.Log += Log;

        await _client.LoginAsync(TokenType.Bot, discordToken.ToString());
        await _client.StartAsync();

        _client.MessageReceived += MessageReceived;

        Console.ReadKey();

        await _client.StopAsync();
    }

    private async Task MessageReceived(SocketMessage message)
    {
        if (message.Author.IsBot) return;

        string input = message.Content;
        Console.WriteLine($"({message.Author.Username}#{message.Author.DiscriminatorValue}) {input}");

        // Remove symbols
        string filteredInput = new string(input.Where(c => Char.IsLetterOrDigit(c) || Char.IsWhiteSpace(c)).ToArray());

        Dictionary<string, HashSet<string>> blacklist = new Dictionary<string, HashSet<string>>()
        {
            ["insert channel id here"] = new HashSet<string>() { "insert slur here" }
        };

        if (input.Contains("::clear"))
        {
            string FilePath = "user_memory.txt";

            if (File.Exists(FilePath))
            {
                // If file found, delete it    
                File.Delete(FilePath);
                Console.WriteLine("File deleted.");
            }

            return;
        }

        string apiKeys = "keys.json";
        JObject json = JObject.Parse(await File.ReadAllTextAsync(apiKeys));
        JToken openAIToken = json.SelectToken("openAIToken");

        var openai = new OpenAIAPI(openAIToken.ToString());

        // Check if user has interacted with the bot before
        var user = message.Author;

        var memory = await GetUserMemory(user.Id);

        // Use OpenAI API to generate a response based on user memory and message content
        var prompt = memory + filteredInput;

        // Store user memory in file
        await SaveUserMemory(user.Id, prompt);

        await message.Channel.TriggerTypingAsync();

        CompletionRequest completionRequest = new CompletionRequest();
        completionRequest.Prompt = prompt;
        completionRequest.Model = new OpenAI_API.Models.Model("text-davinci-003");
        completionRequest.Temperature = 0.5;

        var completions = await openai.Completions.CreateCompletionAsync(completionRequest.Prompt, completionRequest.Model, max_tokens: 1000, completionRequest.Temperature);
        var response = completions.Completions[0].Text.Trim();

        Console.WriteLine($"(Responing to: {message.Author.Username}#{message.Author.DiscriminatorValue}) {response}");

        Console.WriteLine(memory);

        // Check for blacklisted words
        HashSet<string> channelBlacklist = null;
        if (blacklist.TryGetValue(message.Channel.Id.ToString(), out channelBlacklist))
        {
            foreach (string word in channelBlacklist)
            {
                if (response.Contains(word))
                {
                    Console.WriteLine("Filtered");

                    await message.Channel.SendMessageAsync("Filtered");
                    return;
                }
            }
        }

        await message.Channel.SendMessageAsync(response);
    }

    private async Task<string> GetUserMemory(ulong userId)
    {
        if (!File.Exists(_memoryFilePath))
        {
            return "";
        }

        var lines = await File.ReadAllLinesAsync(_memoryFilePath);
        List<string> userMemories = new List<string>();
        foreach (var line in lines)
        {
            var parts = line.Split(" : ");
            if (parts.Length == 2 && ulong.TryParse(parts[0], out ulong id) && id == userId)
            {
                userMemories.Add(parts[1]);
            }
        }

        // Join all memories into a single string and return it
        return string.Join(Environment.NewLine, userMemories);
    }

    private async Task SaveUserMemory(ulong userId, string memory)
    {
        const int maxLines = 100; // maximum number of lines in memory file
        var lines = new List<string>();

        if (File.Exists(_memoryFilePath))
        {
            lines = (await File.ReadAllLinesAsync(_memoryFilePath)).ToList();
        }

        var existingLine = lines.FirstOrDefault(x => x.StartsWith($"{userId}:"));
        if (existingLine != null)
        {
            lines.Remove(existingLine);
        }

        lines.Add($"{userId} : {memory}");

        // Remove the oldest lines if the maximum file size is reached
        if (lines.Count > maxLines)
        {
            lines.RemoveRange(0, lines.Count - maxLines);
        }

        await File.WriteAllLinesAsync(_memoryFilePath, lines);
    }

    private Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }
}
