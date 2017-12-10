using System.Linq;
using System.Threading.Tasks;
using Discord;
using NadekoBot.Common.Collections;
using NadekoBot.Common.ModuleBehaviors;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.Database.Models;

namespace NadekoBot.Modules.Permissions.Services
{
    public class GlobalWhitelistService : INService
    {
        private readonly DbService _db;
        public ConcurrentHashSet<ulong> GlobalWhitelistedUsers { get; }
        public ConcurrentHashSet<ulong> GlobalWhitelistedGuilds { get; }
        public ConcurrentHashSet<ulong> GlobalWhitelistedChannels { get; }

        public GlobalWhitelistService(IBotConfigProvider bc, DbService db)
        {
            _db = db;

            var GlobalWhitelist = bc.BotConfig.GlobalWhitelistMembers;
            GlobalWhitelistedUsers = new ConcurrentHashSet<ulong>(GlobalWhitelist.Where(bi => bi.Type == GlobalWhitelistType.User).Select(c => c.ItemId));
            GlobalWhitelistedGuilds = new ConcurrentHashSet<ulong>(GlobalWhitelist.Where(bi => bi.Type == GlobalWhitelistType.Server).Select(c => c.ItemId));
            GlobalWhitelistedChannels = new ConcurrentHashSet<ulong>(GlobalWhitelist.Where(bi => bi.Type == GlobalWhitelistType.Channel).Select(c => c.ItemId));
        }

        public bool CreateWhitelist(string name)
        {
            using (var uow = _db.UnitOfWork)
            {
                var group = new GlobalWhitelistSet()
                {
                    ListName = name
                };
                uow.GlobalWhitelists.Add(group);

                uow.Complete();
            }

            return true;
        }
        public string[] GetAllNames(int page)
        {
            var names = new string[0];

            using (var uow = _db.UnitOfWork)
            {
                var bc = uow.BotConfig.GetOrCreate();
                uow._context.SaveChanges();

                // First, Collect all lists
                var groups = uow.GlobalWhitelists.GetWhitelistGroups(page);

                // Third, Take just the names
                for ( var i=0; i<groups.Length; i++ ) 
                {
                    names[i] = groups[i].ListName;
                }

                uow.Complete();
            }
            return names;
        }

        public string[] GetNamesByMember(ulong id, GlobalWhitelistType type, int page)
        {
            var names = new string[0];

            using (var uow = _db.UnitOfWork)
            {
                var bc = uow.BotConfig.GetOrCreate();
                uow._context.SaveChanges();

                // First, Retrieve the WhitelistItem given id,type
                var item = bc.GlobalWhitelistMembers
                        .Where( x => x.ItemId.Equals(id) )
                        .Where( x => x.Type.Equals(type) )
                        .FirstOrDefault();

                if (item == null) return names;

                // Second, Collect all lists related to item
                var groups = uow.GlobalWhitelists.GetWhitelistGroupsByMember(item.Id,page);

                // Third, Take just the names
                for ( var i=0; i<groups.Length; i++ ) 
                {
                    names[i] = groups[i].ListName;
                }

                uow.Complete();
            }
            return names;
        }

        public bool GetGroupByName(string listName, out GlobalWhitelistSet group)
        {
            group = null;

            if (string.IsNullOrWhiteSpace(listName)) return false;

            using (var uow = _db.UnitOfWork)
            {
                group = uow.GlobalWhitelists.GetByName(listName);

                if (group == null) { return false; }
                else { return true; }
            }
        }

        public bool GetGroupMembers(string listName, out IGrouping<GlobalWhitelistType,GlobalWhitelistItem>[] members)
        {
            members = null;

            using (var uow = _db.UnitOfWork)
            {
                var bc = uow.BotConfig.GetOrCreate();
                uow._context.SaveChanges();

                // First, Get the group
                GetGroupByName(listName, out GlobalWhitelistSet group);

                if (group == null) return false;

                // Second, Get the members
                var results = group.GlobalWhitelistItemSets
                    .Select(x => x.Item)
                    .GroupBy( x => x.Type )
                    .ToArray();

                uow.Complete();
            }

            return false;
        }

        public bool AddItemToGroup(ulong id, GlobalWhitelistType type, string listName)
        {
            using (var uow = _db.UnitOfWork)
            {
                var bc = uow.BotConfig.GetOrCreate();
                uow._context.SaveChanges();

                // First GetOrCreate the WhitelistItem given id and type
                var item = bc.GlobalWhitelistMembers
                        .Where( x => x.ItemId.Equals(id) )
                        .Where( x => x.Type.Equals(type) )
                        .FirstOrDefault();

                if (item == null) {
                    item = new GlobalWhitelistItem
                    {
                        ItemId = id,
                        Type = type
                    };
                    bc.GlobalWhitelistMembers.Add(item);
                }

                // Second Retrieve the WhitelistSet given name
                GetGroupByName(listName, out GlobalWhitelistSet group);

                if (group == null) return false;

                // Third, Check that id is not already in the group
                var itemInGroup = group.GlobalWhitelistItemSets
                    .FirstOrDefault(x => x.ItemPK == item.Id);

                if (itemInGroup != null) return false;

                // Fourth Create a new WhitelistItemSet
                itemInGroup = new GlobalWhitelistItemSet
                {
                    //ListPK = group.Id,
                    //ItemPK = item.Id
                    List = group,
                    Item = item
                };

                // Finally add the WhitelistItemSet
                //uow._context.Set<GlobalWhitelistItemSet>().Add(itemInGroup);
                group.GlobalWhitelistItemSets.Add(itemInGroup);

                uow.Complete();
            }
            return true;
        }
        public bool RemoveItemFromGroup(ulong id, GlobalWhitelistType type, string listName)
        {
            using (var uow = _db.UnitOfWork)
            {
                var bc = uow.BotConfig.GetOrCreate();
                uow._context.SaveChanges();

                // First Retrieve the WhitelistSet given name
                GetGroupByName(listName, out GlobalWhitelistSet group);

                // Second, Retrieve the WhitelistItem given id,type
                var item = bc.GlobalWhitelistMembers
                        .Where( x => x.ItemId.Equals(id) )
                        .Where( x => x.Type.Equals(type) )
                        .FirstOrDefault();

                if (item == null) return false;

                // Third, Find the ItemInSet record
                var itemInSet = group.GlobalWhitelistItemSets
                    .FirstOrDefault(x => x.ItemPK == item.Id);

                // Finally remove the Item from the Set
                group.GlobalWhitelistItemSets.Remove(itemInSet);

                uow.Complete();
            }
            return true;
        }
    }
}
