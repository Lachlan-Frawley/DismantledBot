using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Commands;
using System.Text;

namespace DismantledBot
{
    public class HelpModule : ModuleBase<SocketCommandContext>
    {
        [Command("modules")]
        [Summary("Prints out each command module")]
        public async Task ModuleCommand()
        {
            await Discord.UserExtensions.SendMessageAsync(Context.User, $"Modules:\n{string.Join(",\n", CommandHandler.Modules.Select(x => x.Name))}");
        }

        [Command("help")]
        [Summary("Prints out every command the user has permission to view")]
        public async Task HelpCommand([Summary("The module the user wishes to sample commands from")]string module = "")
        {
            StringBuilder reply = new StringBuilder();
            reply.Append("Help Commands: \n");
            foreach(CommandInfo info in CommandHandler.Modules.Where(x => string.IsNullOrEmpty(module) ? true : x.Name.Equals(module, System.StringComparison.InvariantCultureIgnoreCase)).SelectMany(x => x.Commands))
            {
                if (!(await info.CheckPreconditionsAsync(Context)).IsSuccess)
                    continue;
                reply.Append($"Name: {(!string.IsNullOrEmpty(info.Module.Group) ? info.Module.Group + " " : "")}{info.Name}\n");
                if (!string.IsNullOrEmpty(info.Summary))
                    reply.Append($"Summary: {info.Summary}\n");
                if (info.Parameters.Count != 0)
                    reply.Append("Parameters:\n");
                foreach (ParameterInfo commandArg in info.Parameters)
                {                    
                    reply.Append($"\t- {commandArg.Name} ({commandArg.Type.Name}");
                    if (commandArg.DefaultValue != null)
                        reply.Append($" = {(commandArg.DefaultValue.GetType() == typeof(string) ? (string.IsNullOrEmpty(commandArg.DefaultValue.ToString()) ? "null" : commandArg.DefaultValue) : commandArg.DefaultValue)}");
                    reply.Append(")");
                    if (commandArg.IsOptional)
                        reply.Append(" [optional] ");
                    if (!string.IsNullOrEmpty(commandArg.Summary))
                        reply.Append($" -> {commandArg.Summary}");
                    reply.Append("\n");
                }
                reply.Append("\n");
            }

            await Discord.UserExtensions.SendMessageAsync(Context.User, reply.ToString());
        }
    }
}
