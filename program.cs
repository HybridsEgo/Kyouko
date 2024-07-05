using System;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using Newtonsoft.Json.Linq;
using Discord.Commands;
using OllamaSharp;
using System.Text;
using System.Collections.Generic;

public class Program
{
    private DiscordSocketClient? _client;
    public string apiKeys = "keys.json";
    public JObject json = JObject.Parse(File.ReadAllText("keys.json"));
    private static readonly string whitelistFilePath = "channelWhitelist.json";
    private static List<ulong> whitelistedChannelIds = new List<ulong>();
    private string _memoryFilePath = "memory";

    public static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

    public async Task MainAsync()
    {
        //LoadWhitelist();

        if (!File.Exists(apiKeys))
        {
            Console.WriteLine("File not found.");
            File.Create(apiKeys);
        }

        _client = new DiscordSocketClient();
        _client.Log += message => { Log(message); return Task.CompletedTask; };

        JToken discordToken = json["discordToken"];

        await _client.LoginAsync(TokenType.Bot, discordToken.ToString());
        await _client.StartAsync();

        _client.MessageReceived += MessageReceived;
        _client.ButtonExecuted += ButtonHandler;

        await _client.SetStatusAsync(UserStatus.DoNotDisturb);
        await _client.SetGameAsync("🙌");

        Console.ReadKey();

        await _client.StopAsync();
    }

    private Task Log(LogMessage message)
    {
        if (message.ToString().Contains("Connecting") && message.ToString().Contains("Gateway"))
        {
            Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} : Discord Gateway - Connecting...");
        }

        if (message.ToString().Contains("Ready") && message.ToString().Contains("Gateway"))
        {
            Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} : Discord Gateway - Connected and ready!");
        }

        return Task.CompletedTask;
    }

    private async Task MessageReceived(SocketMessage discordMessage)
    {
        try
        {
            if (discordMessage.Channel.Id != 1090479634715516948 &&
                discordMessage.Channel.Id != 1089715502663880765 &&
                discordMessage.Channel.Id != 1091941641729888376) return;

            var uri = new Uri("http://localhost:11434");
            var ollama = new OllamaApiClient(uri);
            ollama.SelectedModel = "Kyouko";

            if (!(discordMessage is SocketUserMessage msg)) return;
            if (msg.Author.IsBot) return;

            var context = new CommandContext(_client, msg);

            ulong colorize = 674068746167386149;
            int r = 0, g = 0, b = 0;

            EmbedBuilder builder = new EmbedBuilder();
            var random = new Random();
            r = random.Next(0, 255);
            g = random.Next(0, 255);
            b = random.Next(0, 255);

            Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} : Discord Message - User: " + discordMessage.Author.Username + " in: " + discordMessage.Channel.Name + " said: " + discordMessage.Content.Trim());
            Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} : Kyouko - Generating response...");

            var emojiList = new List<string> { "👌", "👋", "🙌", "👀", "🙃", "🤔", "🤨" };
            int index = random.Next(emojiList.Count);
            var selectedEmoji = new Emoji(emojiList[index]);
            await discordMessage.AddReactionAsync(selectedEmoji);

            var memory = await GetUserMemory(msg.Author.Id);
            var input = $"{memory}\nUser: {discordMessage.Author.Username} ({msg.Author.Id}): {discordMessage.Content.Trim()}"; // Include memory in the input for the language model

            ConversationContext llama3Context = null;
            StringBuilder messageHolder = new StringBuilder();

            await discordMessage.Channel.TriggerTypingAsync();

            Console.Write($"{DateTime.Now.ToString("HH:mm:ss")} : Kyouko -");
            await foreach (var stream in ollama.StreamCompletion(discordMessage.Content.Trim().ToString(), llama3Context))
            {
                messageHolder.Append(stream.Response);

                // real time output
                Console.Write(stream.Response);
            }
            Console.WriteLine($" ");

            var newMemory = $"{memory}\nUser: {discordMessage.Author.Username} ({msg.Author.Id}): {discordMessage.Content.Trim()}";
            await SaveUserMemory(msg.Author.Id, newMemory); // Save the conversation history for the user

            var buttonbuilder = new ComponentBuilder();

            builder.WithColor(r, g, b)
                   .WithDescription(messageHolder.ToString());

            await context.Message.ReplyAsync(messageHolder.ToString());
            Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} : Kyouko - Finished!");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return;
        }
    }

    public async Task ButtonHandler(SocketMessageComponent component)
    {
        switch (component.Data.CustomId)
        {
            case "whitelist":
                //await HandleWhitelistButton(component);
                break;
            case "remove":
                //await HandleRemoveButton(component);
                break;
        }
    }

    private string GetUserMemoryFileName(ulong userId)
    {
        return Path.Combine(_memoryFilePath, $"user_memory_{userId}.txt");
    }

    private async Task<string> GetUserMemory(ulong userId)
    {
        var fileName = GetUserMemoryFileName(userId);
        if (!File.Exists(fileName))
        {
            return "";
        }

        using (StreamReader reader = File.OpenText(fileName))
        {
            return await reader.ReadToEndAsync();
        }
    }

    private async Task SaveUserMemory(ulong userId, string memory)
    {
        if (!Directory.Exists(_memoryFilePath))
        {
            Directory.CreateDirectory(_memoryFilePath);
        }

        var fileName = GetUserMemoryFileName(userId);
        using (StreamWriter writer = new StreamWriter(fileName, append: true))
        {
            await writer.WriteLineAsync(memory);
        }
    }


    private async Task ClearUserMemory(ulong userId)
    {
        var fileName = GetUserMemoryFileName(userId);
        try
        {
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
                Console.WriteLine($"Memory file {fileName} deleted successfully.");
            }
            else
            {
                Console.WriteLine($"Memory file {fileName} does not exist.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting memory file: {ex.Message}");
        }
        await Task.CompletedTask;
    }
}
