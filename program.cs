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

public class Program
{
    private DiscordSocketClient? _client;
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

        _client = new DiscordSocketClient();
        _client.Log += message => { Log(message); return Task.CompletedTask; };

        JToken discordToken = json["discordToken"];

        await _client.LoginAsync(TokenType.Bot, discordToken.ToString());
        await _client.StartAsync();

        _client.MessageReceived += MessageReceived;
        _client.ButtonExecuted += ButtonHandler;

        await _client.SetStatusAsync(UserStatus.DoNotDisturb);
        await _client.SetGameAsync("I glow so brightly");

        Console.ReadKey();

        await _client.StopAsync();
    }

    private Task Log(LogMessage message)
    {
        Console.WriteLine(message.ToString());
        return Task.CompletedTask;
    }

    private async Task MessageReceived(SocketMessage discordMessage)
    {
        try
        {
            var uri = new Uri("http://localhost:11434");
            var ollama = new OllamaApiClient(uri);
            ollama.SelectedModel = "Kyouko";

            if (!(discordMessage is SocketUserMessage msg)) return;
            if (msg.Author.IsBot) return;

            var context = new CommandContext(_client, msg);

            ulong colorize = 674068746167386149;
            int r = 0, g = 0, b = 0;

            var random = new Random();
            r = random.Next(0, 255);
            g = random.Next(0, 255);
            b = random.Next(0, 255);
            EmbedBuilder builder = new EmbedBuilder();

            if (discordMessage.Channel.Id != 1090479634715516948 && discordMessage.Channel.Id != 1089715502663880765 && discordMessage.Channel.Id != 1091941641729888376) return;

            Console.WriteLine("(" + discordMessage.Author.Username + " : in - " + discordMessage.Channel.Name + ") " + discordMessage.Content.Trim());

            var emojiList = new List<string> { "👌", "👋", "🙌", "👀", "🙃", "🤔", "🤨" };
            int index = random.Next(emojiList.Count);
            var selectedEmoji = new Emoji(emojiList[index]);
            await discordMessage.AddReactionAsync(selectedEmoji);
            
            ConversationContext llama3Context = null;
            StringBuilder messageHolder = new StringBuilder();

            await discordMessage.Channel.TriggerTypingAsync();

            await foreach (var stream in ollama.StreamCompletion(discordMessage.Content.Trim().ToString(), llama3Context))
            {
                messageHolder.Append(stream.Response);
                //context = stream.Response
                Console.Write(stream.Response);
            }

            Console.WriteLine(messageHolder.ToString());
            await context.Message.ReplyAsync(messageHolder.ToString());

            var buttonbuilder = new ComponentBuilder().WithButton("Github", null, ButtonStyle.Link, null, "https://www.google.com");

            builder.WithColor(r, g, b)
                   .WithDescription(messageHolder.ToString())
                   .WithFooter($"Replying to: {discordMessage.Author.Username}");

            //await context.Message.ReplyAsync(embed: builder.Build(), components: buttonbuilder.Build());
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
            case "debug":

                break;
        }
    }

    public async Task SlashCommandHandler(SocketSlashCommand command)
    {
        switch (command.Data.Name)
        {
            case "ping":
                await command.RespondAsync($" ");
                break;
        }
    }
}
