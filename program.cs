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
    private string PersonalityFilePath = "personality.txt";
    public string apiKeys = "keys.json";
    public JObject json = JObject.Parse(File.ReadAllText("keys.json"));
    private string GetRandomPersonality()
    {
        var personalities = File.ReadAllLines("personality.txt");
        var random = new Random();
        var index = random.Next(0, personalities.Length);
        return personalities[index];
    }
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
        _memoryFilePath = "Insert folder path";

        // Set up Discord bot client
        _client = new DiscordSocketClient();
        _client.Log += message => { Log(message); return Task.CompletedTask; };

        JToken discordToken = json["discordToken"];

        await _client.LoginAsync(TokenType.Bot, discordToken.ToString());
        await _client.StartAsync();

        _client.MessageReceived += MessageReceived;
        _client.ButtonExecuted += ButtonHandler;





        await _client.SetStatusAsync(UserStatus.DoNotDisturb);
        await _client.SetGameAsync("I glow so brighly");

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

            if (!(message is SocketUserMessage msg)) return;
            if (msg.Author.IsBot) return;

            var context = new CommandContext(_client, msg);


            ulong colorize = 674068746167386149;
            int r = 0; int g = 0; int b = 0;

            var random = new Random();
            r = random.Next(0, 255); g = random.Next(0, 255); b = random.Next(0, 255);
            EmbedBuilder builder = new EmbedBuilder();

         
            if (message.Channel.Id != 1091941641729888376 && message.Channel.Id != 1090479634715516948 && message.Channel.Id != 1089715502663880765 && message.Channel.Id != 587117758911873035) return;

            //if (message.Author.Id == 332582777897746444 && message.Author

            string input = message.ToString();
            Console.WriteLine("(" + message.Author.Username + "#" + message.Author.DiscriminatorValue + " : in - " + message.Channel.Name + ") " + message.Content.ToString().Trim());

            // Remove symbols
            string filteredInput = new string(input.Where(c => Char.IsLetterOrDigit(c) || Char.IsWhiteSpace(c)).ToArray());

            // Blacklist filter
            Dictionary<string, string> blacklist = new Dictionary<string, string>() { { "slur here", "filtered" } };

            foreach (string word in blacklist.Keys)
            {
                string lowercaseWord = word.ToLower();
                string lowercaseInput = filteredInput.ToLower();
                filteredInput = lowercaseInput.Replace(lowercaseWord, blacklist[word]);
            }

            if (filteredInput.Contains("::clear"))
            {
                if (File.Exists(@"Insert folder path"))
                {
                    // Delete the file for the current user
                    File.Delete(Path.Combine(_memoryFilePath, $"{message.Author.Id}.txt"));
                }

                // Delete all other memory files
                var directory = new DirectoryInfo("insert folder path");
    foreach (var file in directory.GetFiles("*.txt"))
    {
        if (file.Name.StartsWith("user_memory") && file.Name != _memoryFilePath)
        {
            file.Delete();
        }
    }

    Console.WriteLine("Memory files deleted.");
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
            var prompt = $"{GetRandomPersonality()} {memory} {message.Content}. (Act like you're Kyouko from Touhou Project .  (Don't read this aloud and dont talk about yourself in thirdperson or your name, use the context provided  to create a good reply))";


            // Store user memory in file
            await SaveUserMemory(user.Id, prompt);

            var emojiList = new List<string>
            {
                "👌","👋", "🙌", "👀", "🙃", "🤔", "🤨"
            };

            // Select a random emoji
            int index = random.Next(emojiList.Count);

            // Result
            var selectedEmoji = new Emoji(emojiList[index]);

            // Add reaction which was the random result
            await message.AddReactionAsync(selectedEmoji);
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
            var buttonbuilder = new ComponentBuilder().WithButton("Github", null, ButtonStyle.Link, null, "https://github.com/HybridsEgo/Cosmic-Drip").WithButton("Lore", null, ButtonStyle.Link, null, "https://en.touhouwiki.net/wiki/Touhou_Wiki").WithButton("Debug", "debug", ButtonStyle.Danger);

            Random a = new Random();

            //r = a.Next(0, 255); g = a.Next(0, 255); b = a.Next(0, 255);
            //await context.Guild.GetRole(colorize).ModifyAsync(x => x.Color = new Color(r, g, b));

            builder.WithColor(r, g, b);
            builder.WithDescription(response);
            builder.WithFooter("Replying to: " + message.Author.Username + "#" + message.Author.Discriminator);

            //await context.Message.ReplyAsync(response, false, builder.Build(), null, null, buttonbuilder.Build());

            await context.Message.ReplyAsync(response, false, null, null, null, buttonbuilder.Build());
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return;
        }

    }

    private string GetUserMemoryFileName(ulong userId)
    {
        return $"{userId}.txt";
    }

    private async Task<string> GetUserMemory(ulong userId)
    {
        var fileName = Path.Combine(_memoryFilePath, $"user_memory_{userId}.txt");
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

        var fileName = Path.Combine(_memoryFilePath, $"user_memory_{userId}.txt");
        using (StreamWriter writer = File.CreateText(fileName))
        {
            await writer.WriteAsync(memory);
        }
    }

    public async Task ButtonHandler(SocketMessageComponent component)
    {
        switch (component.Data.CustomId)
        {
            case "debug":
                // Check if the user is authorized to delete files
                if (component.User.Id == 332582777897746444 || component.User.Id == 971242993774391396)
                {
                    // Get the directory that contains the memory files
                    string memoryDirectory = "Insert folder path";

                    // Loop through all files in the directory
                    foreach (string filePath in Directory.GetFiles(memoryDirectory, "*.txt"))
                    {
                        // If the file is found, delete it
                        File.Delete(filePath);
                        Console.WriteLine("File deleted: " + filePath);
                    }

                    Console.WriteLine("Files have been deleted.");
                }
                else
                {
                    await component.Channel.TriggerTypingAsync();
                    await component.Channel.SendMessageAsync(component.User.Mention + " You don't have access to use that!");
                }

                break;
        }
    }

    public async Task SlashCommandHandler(SocketSlashCommand command)
    {
        // Let's add a switch statement for the command name so we can handle multiple commands in one event.
        switch (command.Data.Name)
        {
            case "github":
                await command.RespondAsync($"https://github.com/HybridsEgo/Cosmic-Drip");
                break;
        }
    }
}
