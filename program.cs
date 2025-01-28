using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Collections.Generic;
using OllamaSharp;
using OllamaSharp.Models;
using System.Net.Http;
using System.Linq;

public partial class Program
{
    private DiscordSocketClient? _client;
    public string apiKeys = "keys.json";
    public JObject json;
    private static readonly string whitelistFilePath = "channelWhitelist.json";
    private static List<ulong> whitelistedChannelIds = new List<ulong>();
    private string _memoryFilePath = "memory";
    private const int MemoryMaxLines = 100;
    private const long MaxMemorySize = 200 * 1024 * 1024;

    // Updated custom prompt to discourage internal monologue
    private const string CustomPrompt = @"You are a helpful AI assistant in a Discord server. Follow these rules:
1. Respond concisely but naturally
2. use all revelant info given to you to make your responses more natural
3. Always maintain a friendly and professional tone
4. Acknowledge users by their username when relevant
5. Never show internal thinking process - only provide final answers";

    public static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

    public async Task MainAsync()
    {
        LoadWhitelistedChannels();

        if (!File.Exists(apiKeys))
        {
            Console.WriteLine("File not found.");
            File.WriteAllText(apiKeys, "{}");
        }
        json = JObject.Parse(File.ReadAllText(apiKeys));

        if (!Directory.Exists("temp"))
        {
            Directory.CreateDirectory("temp");
        }

        if (!Directory.Exists(_memoryFilePath))
        {
            Directory.CreateDirectory(_memoryFilePath);
        }

        _client = new DiscordSocketClient();
        _client.Log += message => { Console.WriteLine(message.ToString()); return Task.CompletedTask; };

        await _client.LoginAsync(TokenType.Bot, ""); // Replace with your bot token
        await _client.StartAsync();

        _client.MessageReceived += MessageReceived;

        await _client.SetStatusAsync(UserStatus.DoNotDisturb);
        await _client.SetGameAsync("🙌");

        Console.ReadKey();
        await _client.StopAsync();
    }

    private static void LoadWhitelistedChannels()
    {
        if (!File.Exists(whitelistFilePath))
        {
            File.WriteAllText(whitelistFilePath, "[]");
        }

        var json = File.ReadAllText(whitelistFilePath);

        // Handle empty file or invalid JSON
        if (string.IsNullOrWhiteSpace(json))
        {
            whitelistedChannelIds = new List<ulong>();
            return;
        }

        try
        {
            var array = JArray.Parse(json);
            whitelistedChannelIds = array.Select(id => (ulong)id).ToList();
        }
        catch (Newtonsoft.Json.JsonException)
        {
            // Create new empty list if JSON is corrupted
            whitelistedChannelIds = new List<ulong>();
            File.WriteAllText(whitelistFilePath, "[]");
        }
    }

    private static void SaveWhitelistedChannels()
    {
        var array = new JArray(whitelistedChannelIds.Select(id => id.ToString()));
        File.WriteAllText(whitelistFilePath, array.ToString());
    }

    private static IEnumerable<string> SplitMessage(string message, int maxLength = 2000)
    {
        if (string.IsNullOrEmpty(message))
            return new[] { "No content generated." };

        var messages = new List<string>();
        while (message.Length > 0)
        {
            var length = Math.Min(maxLength, message.Length);
            messages.Add(message.Substring(0, length));
            message = message.Substring(length);
        }
        return messages;
    }

    private async Task MessageReceived(SocketMessage discordMessage)
    {
        if (!(discordMessage is SocketUserMessage msg) || msg.Author.IsBot) return;

        _ = Task.Run(async () =>
        {
            try
            {
                var allowedChannels = new HashSet<ulong> { 1090479634715516948, 1089715502663880765, 1091941641729888376 };
                allowedChannels.UnionWith(whitelistedChannelIds);

                if (!allowedChannels.Contains(msg.Channel.Id))
                {
                    Console.WriteLine($"Ignoring message in non-whitelisted channel: {msg.Channel.Id}");
                    return;
                }

                var attachments = msg.Attachments.ToArray();
                if (attachments.Length > 0)
                {
                    foreach (var attachment in attachments)
                    {
                        if (attachment.Width.HasValue && attachment.Height.HasValue)
                        {
                            string imageUrl = attachment.Url;
                            string tempImagePath = Path.Combine("temp", attachment.Filename);

                            await DownloadImageAsync(imageUrl, tempImagePath);
                            byte[] imageBytes = File.ReadAllBytes(tempImagePath);

                            var uri = new Uri("http://localhost:11434");
                            var ollama = new OllamaApiClient(uri);
                            ollama.SelectedModel = ""; //replace with ur on  model 

                            Console.WriteLine($"User: {msg.Author.Username} sent an image.");

                            var memory = await GetUserMemory(msg.Author.Id);
                            var input = $"{CustomPrompt}\n{memory}\nUser: {msg.Author.Username} ({msg.Author.Id}): {msg.Content.Trim()}";

                            StringBuilder messageHolder = new StringBuilder();
                            await msg.Channel.TriggerTypingAsync();

                            var request = new GenerateRequest
                            {
                                Prompt = input,
                            };

                            Console.Write("Generating response... ");
                            await foreach (var stream in ollama.GenerateAsync(request, CancellationToken.None))
                            {
                                messageHolder.Append(stream.Response);
                                Console.Write(stream.Response); // Show thinking process in terminal
                            }
                            Console.WriteLine();

                            // Remove <think> blocks from Discord response
                            var replyMessage = messageHolder.ToString();
                            var cleanReply = Regex.Replace(replyMessage, @"<think>.*?</think>", "", RegexOptions.Singleline);

                            // Log full response to terminal
                            Console.WriteLine($"Full AI output:\n{replyMessage}");

                            if (string.IsNullOrWhiteSpace(cleanReply)) cleanReply = "No content generated.";

                            var chunks = SplitMessage(cleanReply).ToList();
                            for (int i = 0; i < chunks.Count; i++)
                            {
                                if (i == 0)
                                    await msg.ReplyAsync(chunks[i]);
                                else
                                    await msg.Channel.SendMessageAsync(chunks[i]);
                                await Task.Delay(200);
                            }

                            File.Delete(tempImagePath);

                            string userLine = $"User: {msg.Author.Username} ({msg.Author.Id}): {msg.Content.Trim()}";
                            string assistantLine = $"Assistant: {cleanReply}";
                            await UpdateUserMemory(msg.Author.Id, $"{userLine}\n{assistantLine}");
                        }
                    }
                }
                else
                {
                    var uri = new Uri("http://localhost:11434");
                    var ollama = new OllamaApiClient(uri);
                    ollama.SelectedModel = "";  //replace with the model of ur choice

                    Console.WriteLine($"User: {msg.Author.Username} said: {msg.Content.Trim()}");

                    var memory = await GetUserMemory(msg.Author.Id);
                    var input = $"{CustomPrompt}\n{memory}\nUser: {msg.Author.Username} ({msg.Author.Id}): {msg.Content.Trim()}";

                    StringBuilder messageHolder = new StringBuilder();
                    await msg.Channel.TriggerTypingAsync();

                    var textRequest = new GenerateRequest { Prompt = input };

                    Console.Write("Generating response... ");
                    await foreach (var stream in ollama.GenerateAsync(textRequest, CancellationToken.None))
                    {
                        messageHolder.Append(stream.Response);
                        Console.Write(stream.Response); // Show thinking process in terminal
                    }
                    Console.WriteLine();

                    // Remove <think> blocks from Discord response
                    var replyMessage = messageHolder.ToString();
                    var cleanReply = Regex.Replace(replyMessage, @"<think>.*?</think>", "", RegexOptions.Singleline);

                    // Log full response to terminal
                    Console.WriteLine($"Full AI output:\n{replyMessage}");

                    if (string.IsNullOrWhiteSpace(cleanReply)) cleanReply = "No content generated.";

                    var chunks = SplitMessage(cleanReply).ToList();
                    for (int i = 0; i < chunks.Count; i++)
                    {
                        if (i == 0)
                            await msg.ReplyAsync(chunks[i]);
                        else
                            await msg.Channel.SendMessageAsync(chunks[i]);
                        await Task.Delay(200);
                    }

                    string userLine = $"User: {msg.Author.Username} ({msg.Author.Id}): {msg.Content.Trim()}";
                    string assistantLine = $"Assistant: {cleanReply}";
                    await UpdateUserMemory(msg.Author.Id, $"{userLine}\n{assistantLine}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex}");
            }
        });
    }

    private async Task<string> GetUserMemory(ulong userId)
    {
        var fileName = Path.Combine(_memoryFilePath, $"user_memory_{userId}.txt");
        if (!File.Exists(fileName)) return "";
        var lines = await File.ReadAllLinesAsync(fileName);
        return string.Join("\n", lines);
    }

    private async Task UpdateUserMemory(ulong userId, string newContent)
    {
        var fileName = Path.Combine(_memoryFilePath, $"user_memory_{userId}.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(fileName));

        List<string> lines = new List<string>();
        if (File.Exists(fileName))
        {
            lines.AddRange(await File.ReadAllLinesAsync(fileName));
        }

        lines.AddRange(newContent.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));

        if (lines.Count > MemoryMaxLines)
        {
            lines = lines.Skip(lines.Count - MemoryMaxLines).ToList();
        }

        long currentSize = 0;
        foreach (var line in lines)
        {
            currentSize += Encoding.UTF8.GetByteCount(line) + 1;
        }

        while (currentSize > MaxMemorySize && lines.Count > 0)
        {
            var removedLine = lines[0];
            currentSize -= Encoding.UTF8.GetByteCount(removedLine) + 1;
            lines.RemoveAt(0);
        }

        await File.WriteAllLinesAsync(fileName, lines);
    }

    private async Task DownloadImageAsync(string url, string outputPath)
    {
        try
        {
            using (var client = new HttpClient())
            {
                var imageBytes = await client.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(outputPath, imageBytes);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error downloading image: {ex.Message}");
        }
    }
}
