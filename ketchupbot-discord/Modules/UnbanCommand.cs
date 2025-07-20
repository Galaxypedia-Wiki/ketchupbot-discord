using Discord.Interactions;

namespace ketchupbot_discord.Modules;

public class PingCommand : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("unban", "For the \"please unban me from galaxy\" crowd")]
    public async Task PingAsync()
    {
        await RespondAsync("go to support server we dont have any control over anything but the galaxypedia\nhttps://support.galaxypedia.org");
    }
}