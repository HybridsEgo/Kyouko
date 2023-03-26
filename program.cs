using System;
using System.IO;
using System.Threading.Tasks;
using OpenAI_API;
using Discord;
using Discord.WebSocket;
using OpenAI_API.Completions;
using Discord.Interactions;


public class Program
{
    private DiscordSocketClient _client;
    private string _memoryFilePath;

    public static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

    public async Task MainAsync()
    {
        // Set up memory file path
        _memoryFilePath = "user_memory.txt";

        // Set up Discord bot client
        _client = new DiscordSocketClient();
        _client.Log += Log;

        await _client.LoginAsync(TokenType.Bot, "");
        await _client.StartAsync();

        _client.MessageReceived += MessageReceived;
     
     Console.WriteLine("Twenty years from now you will be more disappointed by the things that you didn't do than by the ones you did do, So throw off the bowlines, Sail away from the safe harbor, Catch the trade winds in your sails. Explore, dream discover, — Unknown");
        
        await Task.Delay(-1);
    }

    private async Task MessageReceived(SocketMessage message)
    {
        string Filename = "trainingdata.json";
        if (!File.Exists(Filename))
        {
            Console.WriteLine("File not found.");
            File.Create(Filename);
        }

        string trainingData = Filename;

        string input = message.ToString();

        // Remove symbols
        string filteredInput = new string(input.Where(c => Char.IsLetterOrDigit(c) || Char.IsWhiteSpace(c)).ToArray());

        Dictionary<string, string> blacklist = new Dictionary<string, string>()
        {
            {"", "filtered"},

        };

        foreach (string key in blacklist.Keys)
        {
            if (filteredInput.ToLower().Contains(key.ToLower()))
            {
                Console.WriteLine(blacklist[key]);
                await message.Channel.SendMessageAsync("filtered");
                return;
            }
        }

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

        var openai = new OpenAIAPI("");

        //if (message.Author.Id != 332582777897746444) return;
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
        completionRequest.Prompt = "Prompt: " + prompt + " Context that might be relevant but don't rely on it all the time: " + memory;
        completionRequest.Model = OpenAI_API.Models.Model.DavinciText;
        completionRequest.Temperature = 0.5;

        var completions = openai.Completions.CreateCompletionAsync(memory + " " + completionRequest.Prompt, completionRequest.Model, max_tokens: 1000);
        var response = completions.Result.Completions[0].Text.TrimEnd();
        Console.WriteLine(response);

        foreach (string key in blacklist.Keys)
        {
            Console.WriteLine(response);

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
            lines.Remove(existingLine);
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
