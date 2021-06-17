using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace DismantledBot
{
    [RequireContext(ContextType.DM)]
    public sealed class DMCommands : ModuleBase<SocketCommandContext>
    {
        [Command("whoami")]
        public async Task RunWhoAmI()
        {
            SocketGuild guild = CoreProgram.BoundGuild;
            SocketGuildUser user = guild.GetUser(Context.User.Id);

            HashSet<AdminRoleInfo> adminRoles = CoreProgram.database.GetRows(new AdminRoleInfo.Converter());
            var userRoles = user.Roles.Select(x => x.Id);
            var userAdminRoles = adminRoles.Where(x => userRoles.Contains(x.RoleID)).OrderByDescending(x => x.RoleOrder);
            string adminRoleResponse = "You have no administrative roles on this server";
            if(userAdminRoles.Count() != 0)
            {
                adminRoleResponse = $"You have the following administrative roles:\n{string.Join(",\n", userAdminRoles.Select(x => $"{x.RoleName} / {x.RoleID}"))}";
            }
            await ReplyAsync($"Who Am I?\nI Am bound to [{guild.Name}], ID = {guild.Id}\nYou are [{user.Nickname ?? user.Username}]\n{adminRoleResponse}");
        }

        #region Admin Role Commands
        [Command("add admin role")]
        public async Task AddAdminRoleByID(ulong id)
        {
            SocketGuild guild = CoreProgram.BoundGuild;
            SocketRole foundRole = guild.GetRole(id);
            if(foundRole == null)
            {
                await ReplyAsync("Could not find role with selected ID in bound server!");
                return;
            }

            AdminRoleInfo roleInfo = new AdminRoleInfo(foundRole.Id, foundRole.Name, foundRole.Position);
            CoreProgram.database.InsertSingle(roleInfo);
            await ReplyAsync("Added role to administrative role list!");
        }

        [Command("add admin role")]
        public async Task AddAdminRoleByName(string name)
        {
            SocketGuild guild = CoreProgram.BoundGuild;
            SocketRole foundRole = guild.Roles.ToList().Find(x => string.Equals(name, x.Name));
            if (foundRole == null)
            {
                await ReplyAsync("Could not find role with the given name in bound server!");
                return;
            }

            AdminRoleInfo roleInfo = new AdminRoleInfo(foundRole.Id, foundRole.Name, foundRole.Position);
            CoreProgram.database.InsertSingle(roleInfo);
            await ReplyAsync("Added role to administrative role list!");
        }

        [Command("remove admin role")]
        public async Task RemoveAdminRoleByID(ulong id)
        {
            SocketGuild guild = CoreProgram.BoundGuild;
            SocketRole foundRole = guild.GetRole(id);
            if (foundRole == null)
            {
                await ReplyAsync("Could not find role with selected ID in bound server!");
                return;
            }

            AdminRoleInfo roleInfo = new AdminRoleInfo(foundRole.Id, foundRole.Name, foundRole.Position);
            CoreProgram.database.DeleteSingle(roleInfo, "RoleID");
            await ReplyAsync("Successfully removed role from administrative list!");
        }

        [Command("remove admin role")]
        public async Task RemoveAdminRoleByName(string name)
        {
            SocketGuild guild = CoreProgram.BoundGuild;
            SocketRole foundRole = guild.Roles.ToList().Find(x => string.Equals(name, x.Name));
            if (foundRole == null)
            {
                await ReplyAsync("Could not find role with selected ID in bound server!");
                return;
            }

            AdminRoleInfo roleInfo = new AdminRoleInfo(foundRole.Id, foundRole.Name, foundRole.Position);
            CoreProgram.database.DeleteSingle(roleInfo, "RoleID");
            await ReplyAsync("Successfully removed role from administrative list!");
        }
        #endregion
    }
}
