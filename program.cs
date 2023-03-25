using System;
using System.IO;
using System.Threading.Tasks;
using OpenAI_API;
using Discord;
using Discord.WebSocket;
using OpenAI_API.Completions;
using Discord.Interactions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using DNet_V3_Tutorial;
using System.Text.RegularExpressions;

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

        await Task.Delay(-1);
    }

    private async Task MessageReceived(SocketMessage message)
    {
        string input = message.ToString();

        // Remove symbols
        string filteredInput = new string(input.Where(c => Char.IsLetterOrDigit(c) || Char.IsWhiteSpace(c)).ToArray());

        Dictionary<string, string> blacklist = new Dictionary<string, string>()
        {
            { "", "" }
        };

        foreach (string key in blacklist.Keys)
        {
            if (filteredInput.ToLower().Contains(key.ToLower()))
            {
                Console.WriteLine(blacklist[key]);
                await message.Channel.SendMessageAsync("Filtered");
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

        if (message.Author.IsBot) return;

        // Check if user has interacted with the bot before
        var user = message.Author;
        var memory = await GetUserMemory(user.Id);

        // Use OpenAI API to generate a response based on user memory and message content
        var prompt = message.Content;

        // Store user memory in file
        await SaveUserMemory(user.Id, prompt);

        CompletionRequest completionRequest = new CompletionRequest();
        completionRequest.Prompt = "Prompt: "+ input + " Context that might be relevant but don't rely on it all the time: " + memory;
        completionRequest.Model = OpenAI_API.Models.Model.DavinciText;

        var completions = openai.Completions.CreateCompletionAsync(memory + " " + completionRequest.Prompt, completionRequest.Model, max_tokens: 100);
        var response = completions.Result.Completions[0].Text.TrimEnd();

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
            var parts = line.Split(":");
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

        lines.Add($"{userId}:{memory}");

        await File.WriteAllLinesAsync(_memoryFilePath, lines);
    }

    private Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }
}
