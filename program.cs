using System;
using System.IO;
using System.Threading.Tasks;
using OpenAI_API;
using Discord;
using Discord.WebSocket;
using OpenAI_API.Completions;
using Discord.Interactions;
using Newtonsoft.Json.Linq;
using Discord.Commands;

public class Program
{
    private DiscordSocketClient? _client;
    private string? _memoryFilePath;
    public string apiKeys = "keys.json";
    public JObject json = JObject.Parse(File.ReadAllText("keys.json"));

    public static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

    public async Task MainAsync()
    {
        if (!File.Exists(apiKeys))
        {
            Console.WriteLine("File not found.");
            File.Create(apiKeys);
        }

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
        _client.Log += message => { Log(message); return Task.CompletedTask; };

        JToken discordToken = json["discordToken"];

        await _client.LoginAsync(TokenType.Bot, discordToken.ToString());
        await _client.StartAsync();

        _client.MessageReceived += MessageReceived;

        Console.ReadKey();

        await _client.StopAsync();
    }
    private Task Log(LogMessage message)
    {
        Console.WriteLine(message.ToString());
        return Task.CompletedTask;
    }
    private async Task MessageReceived(SocketMessage message)
    {
        try
        {
            var msg = message as SocketUserMessage;
            var context = new CommandContext(_client, msg);

            ulong colorize = 674068746167386149;
            int r = 0; int g = 0; int b = 0;

            var random = new Random();
            r = random.Next(0, 255); g = random.Next(0, 255); b = random.Next(0, 255);
            EmbedBuilder builder = new EmbedBuilder();

            if (message.Author.IsBot) return;
            if (message.Channel.Id != 1091941641729888376) return;
            //if (message.Author.Id == 332582777897746444 && message.Author.Id == 815425069656440883) return;

            string input = message.ToString();
            Console.WriteLine("(" + message.Author.Username + "#" + message.Author.DiscriminatorValue + " : in - " + message.Channel.Name + ") " + message.Content.ToString().Trim());

            // Remove symbols
            string filteredInput = new string(input.Where(c => Char.IsLetterOrDigit(c) || Char.IsWhiteSpace(c)).ToArray());

            //Blacklist filter
            Dictionary<string, string> blacklist = new Dictionary<string, string>() { { "bad words here", "filtered" } };

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

            if (input.Contains("@"))
            {
                await message.Channel.SendMessageAsync("Filtered");
                return;
            }

            JToken openAIToken = json.SelectToken("openAIToken");
            var openai = new OpenAIAPI(openAIToken.ToString());

            // Check if user has interacted with the bot before
            var user = message.Author;

            var memory = await GetUserMemory(user.Id);

            // Use OpenAI API to generate a response based on user memory and message content
            var prompt = $"{memory} {message.Content}. Act like you're Kyouko from Touhou Project! be gooofy!";

            // Store user memory in file
            await SaveUserMemory(user.Id, prompt);

            var YourEmoji = new Emoji("👌");

            await message.AddReactionAsync(YourEmoji);
            await message.Channel.TriggerTypingAsync();

            CompletionRequest completionRequest = new CompletionRequest();
            completionRequest.Prompt = prompt;
            completionRequest.Model = new OpenAI_API.Models.Model("text-davinci-003");
            completionRequest.Temperature = 0.9;

            var completions = openai.Completions.CreateCompletionAsync(completionRequest.Prompt, completionRequest.Model, max_tokens: 100, completionRequest.Temperature);
            var response = completions.Result.Completions[0].Text.Trim();

            Console.WriteLine("(Responing to : " + message.Author.Username + "#" + message.Author.DiscriminatorValue + ") " + response.Trim());

            Console.WriteLine(memory);

            foreach (string key in blacklist.Keys)
            {
                if (response.Contains("@"))
                {
                    //await message.Channel.SendMessageAsync("Filtered");
                    return;
                }

                if (response.Contains(key.ToLower()))
                {
                    Console.WriteLine(blacklist[key]);

                    await message.Channel.SendMessageAsync("Filtered");
                    return;
                }
            }

            Random a = new Random();
            r = a.Next(0, 255); g = a.Next(0, 255); b = a.Next(0, 255);
            await context.Guild.GetRole(colorize).ModifyAsync(x => x.Color = new Color(r, g, b));

            builder.WithColor(r, g, b);
            builder.WithDescription(response);
            builder.WithFooter("Replying to: " + message.Author.Username + "#" + message.Author.Discriminator);

            await message.Channel.SendMessageAsync("", false, builder.Build());
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return;
        }

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

        var existingLineIndex = lines.FindIndex(x => x.StartsWith($"{userId}:"));
        if (existingLineIndex != -1)
        {
            var existingLineParts = lines[existingLineIndex].Split(" : ");
            var existingMemory = existingLineParts[1];
            memory = $"{existingMemory} {memory}";
            lines[existingLineIndex] = $"{userId} : {memory}";
        }
        else
        {
            lines.Add($"{userId} : {memory}");
        }

        await File.WriteAllLinesAsync(_memoryFilePath, lines);
    }
}
