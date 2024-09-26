using System.Net.Http.Json;
using System.Text;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace ketchupbot_discord;

public static class GalaxyGpt
{
    private static readonly HttpClient HttpClient = new();

    private const int MaxResponseLength = 1900;

    public static async Task HandleMessage(SocketMessage messageParam, DiscordSocketClient client, ulong[]? allowedChannels = null)
    {
        if (messageParam is not SocketUserMessage message || message.Author.IsBot) return;

        if (allowedChannels != null && !allowedChannels.Contains(message.Channel.Id)) return;

        if (message.Content.Trim() == client.CurrentUser.Mention)
        {
            await message.ReplyAsync("""
                                     Hello! I'm KetchupBot. The official Galaxypedia Assistant & Automatic Updater!
                                     
                                     Updates ships every hour at XX:00
                                     Updates turrets page every hour at XX:30
                                     More information can be found here: <https://robloxgalaxy.wiki/wiki/User:Ketchupbot101>
                                     
                                     [Report](<https://discord.robloxgalaxy.wiki>) any unintended behaviour to Galaxypedia Head Staff immediately
                                     """);
            return;
        }

        int argPos = 0;
        if (!message.HasMentionPrefix(client.CurrentUser, ref argPos)) return;

        string messageContent = message.Content[argPos..].Trim();

        if (messageContent.Length > 750)
        {
            await message.ReplyAsync("Your question is too long! Please keep it under 750 characters.");
            return;
        }

        IDisposable? typingState = message.Channel.EnterTypingState();

        try
        {
            ApiResponse apiResponse = await GetApiResponse(message, messageContent);

            // ReSharper disable once RedundantAssignment
            bool verbose = false;
#if DEBUG
            verbose = true;
#endif

            if (messageContent.Contains("+v", StringComparison.OrdinalIgnoreCase))
            {
                verbose = true;
                messageContent = messageContent.Replace("+v", "", StringComparison.OrdinalIgnoreCase).Trim();
            }

            var answerMessage = new StringBuilder();

            if (messageContent.Contains("best", StringComparison.OrdinalIgnoreCase))
            {
                answerMessage.AppendLine().AppendLine("""**Warning:** These kinds of questions have a high likelihood of being answered incorrectly. Please be more specific and avoid ambiguous questions like "what is the best super capital?" """);
            }

            #region Response Answer

            if (apiResponse.Answer.Length > MaxResponseLength)
                answerMessage.AppendLine(apiResponse.Answer[..Math.Min(apiResponse.Answer.Length, MaxResponseLength)] + " (truncated)");
            else
                answerMessage.AppendLine(apiResponse.Answer);

            #endregion

            #region Verbose Information

            if (verbose)
            {
                if (int.TryParse(apiResponse.QuestionTokens, out int questionTokens))
                    answerMessage.AppendLine($"Question Tokens: {questionTokens}");
                if (int.TryParse(apiResponse.ResponseTokens, out int responseTokens))
                    answerMessage.AppendLine($"Response Tokens: {responseTokens}");

                // NOTE: These numbers are hardcoded and not necessarily representative of the actual costs, as the model can change
                if (questionTokens != 0 && responseTokens != 0)
                    answerMessage.AppendLine($"Cost: ${Math.Round(questionTokens * 0.00000015 + responseTokens * 0.0000006, 10)}");

                if (apiResponse.Duration != null)
                    answerMessage.AppendLine(
                        $"Response Time: {apiResponse.Duration}ms (not including API transport overhead)");
            }

            #endregion

            #region Context Attacher and Message Sender

            if (!string.IsNullOrWhiteSpace(apiResponse.Context) && verbose)
            {
                using var contextStream = new MemoryStream(Encoding.UTF8.GetBytes(apiResponse.Context));
                await message.Channel.SendFileAsync(contextStream, "context.txt", answerMessage.ToString(), messageReference: new MessageReference(message.Id), allowedMentions: AllowedMentions.None);
            } else {
                await message.ReplyAsync(answerMessage.ToString(), allowedMentions: AllowedMentions.None);
            }

            #endregion
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            await message.ReplyAsync($"Sorry! An error occurred while processing your request.\nError Code: 0x{e.HResult:X8}");
        }
        finally
        {
            typingState.Dispose();
        }
    }

    private static async Task<ApiResponse> GetApiResponse(SocketUserMessage message, string messageContent)
    {
        using HttpResponseMessage response =
            await HttpClient.PostAsJsonAsync(
                Environment.GetEnvironmentVariable("GPTAPIURL") ?? "http://localhost:3636/api/v1/ask", new
                {
                    prompt = messageContent,
                    username = message.Author.Username,
                    maxlength = 500,
                    maxcontextlength = 10
                });

        response.EnsureSuccessStatusCode();
        ApiResponse responseJson = await response.Content.ReadFromJsonAsync<ApiResponse>() ??
                                   throw new InvalidOperationException("Failed to deserialize response from API");

        return responseJson;
    }
}
