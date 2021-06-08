using Discord.Commands;
using Discord;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DismantledBot
{
    // Forces a command to fail
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class ForceFailureAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            return Task.FromResult(PreconditionResult.FromError("I am a deliberate failure! :)"));
        }
    }

    // Checks if command is run by the bot admin
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class IsBotAdminAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            return Task.FromResult(context.User.Id == Functions.Implementations.GET_ADMIN_FUNC() ? PreconditionResult.FromSuccess() : PreconditionResult.FromError("User is not bot admin"));
        }
    }

    // Checks if a command is run by a user, or set of users
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class IsUserAttribute : PreconditionAttribute
    {
        private Func<ulong>[] UserIDs;

        public IsUserAttribute(params string[] idMappingFuncs)
        {
            UserIDs = idMappingFuncs.Select(x => Functions.GetFunc<ulong>(x)).ToArray();
        }

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            return Task.FromResult(UserIDs.Select(x => x()).Contains(context.User.Id) ? PreconditionResult.FromSuccess() : PreconditionResult.FromError("User is not authorized!"));
        }
    }

    // Checks if a command is run by a user with a minimum role
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class HasMinimumRoleAttribute : PreconditionAttribute
    {
        public ulong MinimumRole;

        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            // Pray that roles are correctly ordered
            List<ulong> roles = new List<IRole>(context.Guild.Roles).Select(x => x.Id).ToList();

            int roleHeight = roles.IndexOf(MinimumRole);
            List<int> userRoleHeights = new List<ulong>((await context.Guild.GetUserAsync(context.User.Id)).RoleIds).Select(x => roles.IndexOf(x)).ToList();
            
            if(userRoleHeights.Any(x => x >= roleHeight))
            {
                return PreconditionResult.FromSuccess();
            } else
            {
                return PreconditionResult.FromError("User does not have a high enough role!");
            }
        }
    }

    // Checks if a command is run by someone with a certain role
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class HasRoleAttribute : PreconditionAttribute
    {
        private Func<ulong>[] RoleIds;

        public HasRoleAttribute(params string[] idMappingFuncs)
        {
            RoleIds = idMappingFuncs.Select(x => Functions.GetFunc<ulong>(x)).ToArray();
        }

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            List<ulong> userRoles = CoreProgram.BoundGuild.GetUser(context.User.Id).Roles.Select(x => x.Id).ToList();

            if(RoleIds.Any(x => userRoles.Contains(x())))
            {
                return Task.FromResult(PreconditionResult.FromSuccess());
            } else
            {
                return Task.FromResult(PreconditionResult.FromError("User does not have a required role!"));
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class IsMessageNotDMAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            return Task.FromResult(context.Guild != null ? PreconditionResult.FromSuccess() : PreconditionResult.FromError("Message is DM!"));
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class IsMessageDMAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            return Task.FromResult(context.Guild == null ? PreconditionResult.FromSuccess() : PreconditionResult.FromError("Message is DM!"));
        }
    }
}
