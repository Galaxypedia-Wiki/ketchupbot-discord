﻿using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

namespace ketchupbot_discord;

public class Program
{
    private static DiscordSocketClient _client = null!;
    private static InteractionService _interactionService = null!;

    private static readonly IConfigurationRoot Configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", true, true)
        .AddEnvironmentVariables()
        .AddUserSecrets<Program>()
        .Build();

    public static async Task Main()
    {
#if !DEBUG
        SentrySdk.Init(options =>
        {
            options.Dsn =
 Configuration["SENTRY_DSN"] ?? "https://fe5889aff53840ff6e748fd2de1cf963@o4507833886834688.ingest.us.sentry.io/4507992345214976";
            options.AutoSessionTracking = true;
            options.TracesSampleRate = 1.0;
            options.ProfilesSampleRate = 1.0;
        });
#endif

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = (GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent) &
                             ~(GatewayIntents.GuildScheduledEvents | GatewayIntents.GuildInvites)
        });
        _interactionService = new InteractionService(_client.Rest);

        #region Event Handlers

        _client.Log += Log;
        _client.Ready += Ready;
        _client.MessageReceived += AutoPublishAnnouncements;
        _client.MessageReceived += GalaxyGptHandler;
        _client.InteractionCreated += async interaction =>
            await _interactionService.ExecuteCommandAsync(new SocketInteractionContext(_client, interaction), null);

        #endregion

        await _client.LoginAsync(TokenType.Bot, Configuration["DISCORD_TOKEN"]);
        await _client.StartAsync();

        await _interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), null);

        await Task.Delay(-1);
    }

    private static Task Log(LogMessage msg)
    {
        Console.WriteLine(msg.ToString());
        return Task.CompletedTask;
    }

    private static async Task Ready()
    {
        await _client.SetActivityAsync(new Game("the upstream API", ActivityType.Watching));

#if DEBUG
        await _interactionService.RegisterCommandsToGuildAsync(ulong.Parse(Configuration["TEST_GUILD_ID"] ??
                                                                           throw new InvalidOperationException()));
#else
        await _interactionService.RegisterCommandsGloballyAsync();
#endif
    }

    private static async Task AutoPublishAnnouncements(SocketMessage messageParam)
    {
        // Check if the message is a user message and not from a bot
        if (messageParam is not SocketUserMessage message || message.Author.IsBot) return;

        // Check if the message is in the announcements channel
        if (message.Channel.GetChannelType() != ChannelType.News) return;

        List<ulong> allowedChannels =
        [
            956568339851931728,
            956568339851931728
        ];

        if (allowedChannels.Contains(message.Channel.Id)) await message.CrosspostAsync();
    }

    // Run the GalaxyGPT handler in a separate thread to prevent blocking the main thread
    private static Task GalaxyGptHandler(SocketMessage messageParam)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await GalaxyGpt.HandleMessage(messageParam, _client,
                    Configuration["ALLOWED_CHANNELS"]?.Split(",").Select(item => ulong.Parse(item.Trim())).ToArray());
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        });
        return Task.CompletedTask;
    }
}