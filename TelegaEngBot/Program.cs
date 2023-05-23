﻿using NLog;
using TelegaEngBot.AppConfigurations;
using TelegaEngBot.DataAccessLayer;
using TelegaEngBot.Handlers;
using TelegaEngBot.Identity;
using TelegaEngBot.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TelegaEngBot;

class Program
{
    private static AppDbContext _dbContext;
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    static async Task Main()
    {
        _dbContext = new AppDbContext();
        
        // Check database is not empty
        var check = new CheckDb(_dbContext);
        check.CheckDbEmpty();

        // Check if "user vocabulary" match "common vocabulary"
        check.MatchVocabulary();

        // TelegramBot init
        var botToken = AppConfig.BotToken;
        if (botToken != null)
        {
            var botClient = new TelegramBotClient(botToken);
            //var botClient = new TelegramBotClient("6051962495:AAGqNcy-Li67m4IE6E1XpU8MGlS6e8Q6f0s"); // testBot

            using var cts = new CancellationTokenSource();
            var receiverOptions = new ReceiverOptions() {AllowedUpdates = { }};
            botClient.StartReceiving(
                HandleUpdate,
                ErrorHandler.HandleError,
                receiverOptions,
                cancellationToken: cts.Token
            );
            
            //await botClient.SendTextMessageAsync(450056320, "Press /start to begin.", cancellationToken: cts.Token);

            var me = await botClient.GetMeAsync(cancellationToken: cts.Token);
            Logger.Info("Start listening for @" + me.Username + ".");
            Console.WriteLine("Start listening for @" + me.Username);
        }
        else
            Logger.Fatal("Bot token not found.");

        Console.WriteLine("Press \"Enter\" to exit...");
        Console.Read();
        Logger.Info("Stop listening bot.");
        Console.WriteLine("Stop listening bot.");
        await Task.Delay(1000);
    }

    private static async Task HandleUpdate(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        if (update.Type == UpdateType.Message
            && update.Message?.Text != null
            && update.Message.From != null)
            await HandleMessage(botClient, update.Message);
    }

    private static async Task HandleMessage(ITelegramBotClient botClient, Message message)
    {
        // Identity
        var identity = new IdentityServer(message.From.Id);
        if (!identity.CheckAuth())
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, 
                "*Access denied.* You should request access and then restart bot using the command //start", 
                ParseMode.Markdown);
            Logger.Warn("New user tried to connect. User Id: "
                        + message.From.Id + " Username: " + message.From.Username 
                        + " FirstName: " + message.From.FirstName + " LastName: " + message.From.LastName);
            return;
        }

        var user = _dbContext.UserList.FirstOrDefault(x => x.TelegramUserId == message.From.Id);
        if (user == null) // If null Create User in database
        {
            var userService = new UserService(_dbContext);
            user = userService.CreateUser(message.From.Id, message);
            message.Text = "/start";
        }
        
        //todo make check if last message is processed
        //check unhandled updates (messages)
        // var updates = await botClient.GetUpdatesAsync();
        // if (updates.Any(x => x.Message.Chat.Id == message.Chat.Id)) return;

        var messageHandler = new MessageHandler(botClient, message, _dbContext, user);
        switch (message.Text)
        {
            case "/start":
                await messageHandler.Start();
                break;
            case "Know":
                await messageHandler.Know();
                break;
            case "Don't know":
                await messageHandler.NotKnow();
                break;
            case "Pronunciation":
                await messageHandler.Pron();
                break;
            case "tts":
                await messageHandler.TextToSpeech();
                break;
            case "/smile":
                user.UserSettings.IsSmileOn = !user.UserSettings.IsSmileOn;
                await _dbContext.SaveChangesAsync();
                break;
            case "/sound":// "/pronunciation":
                user.UserSettings.IsPronunciationOn = !user.UserSettings.IsPronunciationOn;
                await _dbContext.SaveChangesAsync();
                await messageHandler.RedrawKeyboard(false);
                break;
            case "/hard":
                await messageHandler.Hard();
                break;
        }
    }
}
// https://t.me/my_aL1fe_bot
// https://t.me/PhrasesAndWords_bot

// Menu
/*
start - Restart
smile - Smile On/Off
pronunciation - Pronunciation On/Of
hard - Show 20 hard-to-remember words
*/