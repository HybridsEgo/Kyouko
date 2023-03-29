using System;
using System.IO;
using System.Threading.Tasks;
using OpenAI_API;
using Discord;
using Discord.WebSocket;
using OpenAI_API.Completions;
using Discord.Interactions;
using Newtonsoft.Json.Linq;

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
            File.Create(apiKeys);
        }

        JObject json = JObject.Parse(File.ReadAllText(apiKeys));
        JToken discordToken = json["discordToken"];

        string Filename = "trainingdata.json";
        if (!File.Exists(Filename))
        {
            Console.WriteLine("File not found.");
            File.Create(Filename);
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
        string input = message.ToString();
        Console.WriteLine("(" + message.Author.Username + "#" + message.Author.DiscriminatorValue + ") " + message.Content.ToString());

        // Remove symbols
        string filteredInput = new string(input.Where(c => Char.IsLetterOrDigit(c) || Char.IsWhiteSpace(c)).ToArray());

        Dictionary<string, string> blacklist = new Dictionary<string, string>()
        {
            {"insert slur here", "filtered"}
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
        JObject json = JObject.Parse(File.ReadAllText(apiKeys));
        JToken openAIToken = json.SelectToken("openAIToken");

        var openai = new OpenAIAPI(openAIToken.ToString());

        //if (message.Author.Id != 332582777897746444 && message.Author.Id != 971242993774391396) return;
        if (message.Author.IsBot) return;

        // Check if user has interacted with the bot before
        var user = message.Author;

        var memory = await GetUserMemory(user.Id);

        // Use OpenAI API to generate a response based on user memory and message content
        var prompt = message.Content;

        // Store user memory in file
        await SaveUserMemory(user.Id, prompt);

        await message.Channel.TriggerTypingAsync();

        CompletionRequest completionRequest = new CompletionRequest();
        completionRequest.Prompt = prompt;
        completionRequest.Model = new OpenAI_API.Models.Model("text-davinci-003");
        completionRequest.Temperature = 1;

        var completions = openai.Completions.CreateCompletionAsync(completionRequest.Prompt, completionRequest.Model, max_tokens: 1000, completionRequest.Temperature);
        var response = completions.Result.Completions[0].Text.Trim();

        Console.WriteLine("(Responing to : " + message.Author.Username + "#" + message.Author.DiscriminatorValue + ") " + response.Trim());

        Console.WriteLine(memory);

        foreach (string key in blacklist.Keys)
        {
            if (response.Contains(key.ToLower()))
            {
                Console.WriteLine(blacklist[key]);

                await message.Channel.SendMessageAsync("Filtered");
                return;
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
        foreach (var line in lines)
        {
            var parts = line.Split(" : ");
            if (parts.Length == 2 && ulong.TryParse(parts[0], out ulong id) && id == userId)
            {
                return parts[1];
            }
        }

        return "";
    }

    private async Task SaveUserMemory(ulong userId, string memory)
    {
        var lines = new List<string>();
        if (File.Exists(_memoryFilePath))
        {
            lines = (await File.ReadAllLinesAsync(_memoryFilePath)).ToList();
        }

        var existingLine = lines.FirstOrDefault(x => x.StartsWith($"{userId}:"));
        if (existingLine != null)
        {
            //lines.Remove(existingLine);
        }

        lines.Add($"{userId} : {memory}");

        await File.WriteAllLinesAsync(_memoryFilePath, lines);
    }

    private Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }
}
