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
        
        //Console.WriteLine(message.ToString());
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
            //Console.WriteLine("(" + discordMessage.Author.Username + " : in - " + discordMessage.Channel.Name + ") " + discordMessage.Content.Trim());

            var emojiList = new List<string> { "👌", "👋", "🙌", "👀", "🙃", "🤔", "🤨" };
            int index = random.Next(emojiList.Count);
            var selectedEmoji = new Emoji(emojiList[index]);
            await discordMessage.AddReactionAsync(selectedEmoji);

            ConversationContext llama3Context = null;
            StringBuilder messageHolder = new StringBuilder();
            
            await discordMessage.Channel.TriggerTypingAsync();

            Console.Write($"{DateTime.Now.ToString("HH:mm:ss")} : ");
            await foreach (var stream in ollama.StreamCompletion(discordMessage.Content.Trim().ToString(), llama3Context))
            {
                messageHolder.Append(stream.Response);
                
                // real time output
                Console.Write(stream.Response);
            }

            //Console.WriteLine(messageHolder.ToString());

            var buttonbuilder = new ComponentBuilder();
                //.WithButton("Whitelist Channel", "whitelist", ButtonStyle.Primary)
                //.WithButton("Remove Channel", "remove", ButtonStyle.Danger);

            builder.WithColor(r, g, b)
                   .WithDescription(messageHolder.ToString());

            await context.Message.ReplyAsync(messageHolder.ToString(), components: buttonbuilder.Build());
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

    /*
    private async Task HandleWhitelistButton(SocketMessageComponent component)
    {
        try
        {
            ulong channelId = component.Channel.Id;
            if (!whitelistedChannelIds.Contains(channelId))
            {
                whitelistedChannelIds.Add(channelId);
                SaveWhitelist();
                await component.RespondAsync($"Channel {channelId} has been whitelisted!");
            }
            else
            {
                await component.RespondAsync($"Channel {channelId} is already whitelisted.");
            }
        }
        catch
        {
            await component.RespondAsync("Unable to whitelist this channel!");
        }
    }

    private async Task HandleRemoveButton(SocketMessageComponent component)
    {
        try
        {
            ulong channelId = component.Channel.Id;
            if (whitelistedChannelIds.Contains(channelId))
            {
                whitelistedChannelIds.Remove(channelId);
                SaveWhitelist();
                await component.RespondAsync($"Channel {channelId} has been removed from the whitelist!");
            }
            else
            {
                await component.RespondAsync($"Channel {channelId} is not in the whitelist.");
            }
        }
        catch
        {
            await component.RespondAsync("Unable to remove this channel from the whitelist!");
        }
    }

    public async Task SlashCommandHandler(SocketSlashCommand command)
    {
        switch (command.Data.Name)
        {
            case "whitelist":
                await HandleWhitelistCommand(command);
                break;
            case "remove":
                await HandleRemoveCommand(command);
                break;
        }
    }

    private async Task HandleWhitelistCommand(SocketSlashCommand command)
    {
        try
        {
            ulong channelId = command.Channel.Id;
            if (!whitelistedChannelIds.Contains(channelId))
            {
                whitelistedChannelIds.Add(channelId);
                SaveWhitelist();
                await command.RespondAsync($"Channel {channelId} has been whitelisted!");
            }
            else
            {
                await command.RespondAsync($"Channel {channelId} is already whitelisted.");
            }
        }
        catch
        {
            await command.RespondAsync("Unable to whitelist this channel!");
        }
    }

    private async Task HandleRemoveCommand(SocketSlashCommand command)
    {
        try
        {
            ulong channelId = command.Channel.Id;
            if (whitelistedChannelIds.Contains(channelId))
            {
                whitelistedChannelIds.Remove(channelId);
                SaveWhitelist();
                await command.RespondAsync($"Channel {channelId} has been removed from the whitelist!");
            }
            else
            {
                await command.RespondAsync($"Channel {channelId} is not in the whitelist.");
            }
        }
        catch
        {
            await command.RespondAsync("Unable to remove this channel from the whitelist!");
        }
    }

    private void SaveWhitelist()
    {
        try
        {
            JArray jsonArray = new JArray(whitelistedChannelIds);
            File.WriteAllText(whitelistFilePath, jsonArray.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save whitelist: {ex.Message}");
        }
    }

    private void LoadWhitelist()
    {
        try
        {
            if (File.Exists(whitelistFilePath))
            {
                string json = File.ReadAllText(whitelistFilePath);
                JArray jsonArray = JArray.Parse(json);
                whitelistedChannelIds = jsonArray.ToObject<List<ulong>>() ?? new List<ulong>();
            }
            else
            {
                // Create the file if it does not exist
                SaveWhitelist();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load whitelist: {ex.Message}");
            whitelistedChannelIds = new List<ulong>();
        }
    }
    */
}
