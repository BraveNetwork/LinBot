using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using System.Collections.Generic;
using NadekoBot.Common.Collections;
using NadekoBot.Common.ModuleBehaviors;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace NadekoBot.Modules.Permissions.Services
{
    public class GlobalWhitelistService : INService
    {
        private readonly DbService _db;
		private readonly DiscordSocketClient _client;

		private string enabledText = Format.Code("✅");
		private string disabledText = Format.Code("❌");
		public readonly int numPerPage = 9;

        public GlobalWhitelistService(DiscordSocketClient client, DbService db)
        {
            _db = db;
			_client = client;
		}

		#region Resolve ulong IDs
        public string[] GetNameOrMentionFromId(GlobalWhitelistType type, ulong[] ids)
        {
            string[] str = new string[ids.Length];

            switch (type) {
                case GlobalWhitelistType.User:
                    for (var i = 0; i < ids.Length; i++) {
                      str[i] = MentionUtils.MentionUser(ids[i]);
                    }
                    break;

                case GlobalWhitelistType.Channel:
                    for (var i = 0; i < ids.Length; i++) {
                      str[i] = MentionUtils.MentionChannel(ids[i]);
                    }
                    break;

                case GlobalWhitelistType.Server:
                    for (var i = 0; i < ids.Length; i++) {
						var guild = _client.Guilds.FirstOrDefault(g => g.Id.Equals(ids[i]));
                    	str[i] = (guild != null) ? $" [{guild.Name}](https://discordapp.com/channels/{ids[i]}/ '{ids[i]}') " : ids[i].ToString();
                    }
                    break;

                default:
                    for (var i = 0; i < ids.Length; i++) {
                      str[i] = ids[i].ToString();
                    }
                    break;
            }

            return str;
        }
        public string GetNameOrMentionFromId(GlobalWhitelistType type, ulong id)
        {
            string str = "";

            switch (type) {
                case GlobalWhitelistType.User:
                    str = MentionUtils.MentionUser(id);
                    break;

                case GlobalWhitelistType.Channel:
                    str = MentionUtils.MentionChannel(id);
                    break;

                case GlobalWhitelistType.Server:
					var guild = _client.Guilds.FirstOrDefault(g => g.Id.Equals(id));
                    str = (guild != null) ? $" [{guild.Name}](https://discordapp.com/channels/{id}/ '{id}') " : id.ToString();
                    break;

                default:
                    str = id.ToString();
                    break;
            }

            return str;
        }

		#endregion Resolve ulong IDs

		#region CheckUnblocked
		public bool CheckIfUnblockedFor(string ubName, UnblockedType ubType, ulong memID, GlobalWhitelistType memType, out string[] lists)
		{
			using (var uow = _db.UnitOfWork)
            {
				lists = uow._context.Set<UnblockedCmdOrMdl>()
					.Where(x => x.Type.Equals(ubType))
					.Where(x => x.Name.Equals(ubName))
					.Join(uow._context.Set<GlobalUnblockedSet>(), 
						ub => ub.Id, gub => gub.UnblockedPK, 
						(ub,gub) => gub.ListPK)
					.Join(uow._context.Set<GlobalWhitelistSet>()
						,//.Where(g => g.IsEnabled.Equals(true)),
						gub => gub, g => g.Id,
						(gub, g) => new {
							ListText = (g.IsEnabled) ? enabledText + g.ListName : disabledText + g.ListName,
							g.Id
						})
					.Join(uow._context.Set<GlobalWhitelistItemSet>(),
						g => g.Id, gi => gi.ListPK,
						(g, gi) => new {
							g.ListText,
							gi.ItemPK
						})
					.Join(uow._context.Set<GlobalWhitelistItem>()
						.Where(x => x.Type.Equals(memType))
						.Where(x => x.ItemId.Equals(memID)),
						gi => gi.ItemPK, i => i.Id,
						(gi, i) => gi.ListText)
					.ToArray();

				if (lists.Count() > 0) return true;
				return false;
			}
		}
		public bool CheckIfUnblocked(string ubName, UnblockedType ubType, ulong memID, GlobalWhitelistType memType)
		{
			// string sql = @"SELECT GlobalWhitelistSet.ListName FROM UnblockedCmdOrMdl
			// 	INNER JOIN GlobalUnblockedSet ON UnblockedCmdOrMdl.Id = GlobalUnblockedSet.UnblockedPK
			// 	INNER JOIN GlobalWhitelistSet ON GlobalUnblockedSet.ListPK = GlobalWhitelistSet.Id
			// 	INNER JOIN GlobalWhitelistItemSet ON GlobalWhitelistSet.Id = GlobalWhitelistItemSet.ListPK
			// 	INNER JOIN GlobalWhitelistItem ON GlobalWhitelistItemSet.ItemPK = GlobalWhitelistItem.Id
			// 	WHERE UnblockedCmdOrMdl.Type = @p0 AND 
			// 	UnblockedCmdOrMdl.Name = '@p1' AND 
			// 	GlobalWhitelistItem.Type = @p2 AND 
			// 	GlobalWhitelistItem.ItemId = @p3;";

			using (var uow = _db.UnitOfWork)
            {
				// var result = uow._context.Database.ExecuteSqlCommand(sql, ubType,ubName,memType,memID);
				// uow._context.SaveChanges();
				// uow.Complete();

				var result = uow._context.Set<UnblockedCmdOrMdl>()
					.Where(x => x.Type.Equals(ubType))
					.Where(x => x.Name.Equals(ubName))
					.Join(uow._context.Set<GlobalUnblockedSet>(), 
						ub => ub.Id, gub => gub.UnblockedPK, 
						(ub,gub) => gub.ListPK)
					.Join(uow._context.Set<GlobalWhitelistSet>()
						.Where(g => g.IsEnabled.Equals(true)),
						gubPK => gubPK, g => g.Id,
						(gubPK, g) => g.Id)
					.Join(uow._context.Set<GlobalWhitelistItemSet>(),
						gId => gId, gi => gi.ListPK,
						(gId, gi) => gi.ItemPK)
					.Join(uow._context.Set<GlobalWhitelistItem>()
						.Where(x => x.Type.Equals(memType))
						.Where(x => x.ItemId.Equals(memID)),
						giPK => giPK, i => i.Id,
						(giPK, i) => giPK)
					.Count();

				// System.Console.WriteLine(result);
				if (result > 0) return true;
				return false;
			}
		}
		#endregion CheckUnblocked

		#region General Whitelist Actions

        public bool CreateWhitelist(string name)
        {
            using (var uow = _db.UnitOfWork)
            {
                var bc = uow.BotConfig.GetOrCreate(set => set
                    .Include(x => x.GlobalWhitelistGroups));

                var group = new GlobalWhitelistSet()
                {
                    ListName = name
                };
                bc.GlobalWhitelistGroups.Add(group);

                uow.Complete();
            }

            return true;
        }

		public bool RenameWhitelist(string oldName, string name)
		{
			using (var uow = _db.UnitOfWork)
            {
                GlobalWhitelistSet group = uow._context.Set<GlobalWhitelistSet>()
					.Where(g => g.ListName.ToLowerInvariant().Equals(oldName))
					.SingleOrDefault();

				if (group == null) return false;

				group.ListName = name;
				uow._context.SaveChanges();
				
                uow.Complete();
            }
			return true;
		}

		public bool SetEnabledStatus(string listName, bool status)
		{
			using (var uow = _db.UnitOfWork)
            {
                GlobalWhitelistSet group = uow._context.Set<GlobalWhitelistSet>()
					.Where(g => g.ListName.ToLowerInvariant().Equals(listName))
					.SingleOrDefault();

				if (group == null) return false;

				group.IsEnabled = status;
				uow._context.SaveChanges();
				
                uow.Complete();
            }
			return true;
		}

		public bool ClearGroupMembers(GlobalWhitelistSet group)
		{
			int result;
			using (var uow = _db.UnitOfWork)
			{
				string sql = "DELETE FROM GlobalWhitelistItemSet WHERE GlobalWhitelistItemSet.ListPK = " + group.Id + ";";
				result = uow._context.Database.ExecuteSqlCommand(sql);
				uow._context.SaveChanges();
				uow.Complete();
			}
			//System.Console.WriteLine("Query Result: ",result);
			return true;
		}

        public bool DeleteWhitelist(string name)
        {
            using (var uow = _db.UnitOfWork)
            {
                // NOTE: using bc to remove will only set the BotConfigId FK to null

                // Delete the whitelist record and all relation records
                uow._context.Set<GlobalWhitelistSet>().Remove( 
                    uow._context.Set<GlobalWhitelistSet>()
                    .Where( x => x.ListName.ToLowerInvariant().Equals(name) ).FirstOrDefault()
                );
                uow.Complete();
            }
            return true;
        }
        public bool GetAllNames(int page, out string[] names, out int count)
        {
			names = null;
            using (var uow = _db.UnitOfWork)
            {
				count = uow._context.Set<GlobalWhitelistSet>().Count();

				if (count <= 0) return false;

				int numSkip = page*numPerPage;
				if (numSkip > count) numSkip = numPerPage * ((count-1)/numPerPage);
				// System.Console.WriteLine("Skip {0}, Count {1}, Page {2}", numSkip, count, page);

				names = uow._context.Set<GlobalWhitelistSet>()
					.OrderBy(g => g.DateAdded)
					.Skip(numSkip)
                	.Take(numPerPage)
					.Select(g => (g.IsEnabled) ? enabledText + g.ListName : disabledText + g.ListName)
					.ToArray();

                uow.Complete();
            }
            return true;
        }

        public string[] GetNamesByMember(ulong id, GlobalWhitelistType type, int page)
        {
            var names = new List<string>();

            using (var uow = _db.UnitOfWork)
            {
                var bc = uow.BotConfig.GetOrCreate();
                uow._context.SaveChanges();

                // First, Retrieve the WhitelistItem given id,type
                var item = bc.GlobalWhitelistMembers
                        .Where( x => x.ItemId.Equals(id) )
                        .Where( x => x.Type.Equals(type) )
                        .FirstOrDefault();

                if (item == null) return names.ToArray();

                // Second, Collect all lists related to item
                var groups = uow.GlobalWhitelists.GetWhitelistGroupsByMember(item.Id,page);

                // Third, Take just the names
                for ( var i=0; i<groups.Length; i++ ) 
                {
                    string text = (groups[i].IsEnabled) ? enabledText + groups[i].ListName : disabledText + groups[i].ListName;
                    names.Add(text);
                }

                uow.Complete();
            }
            return names.ToArray();
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

		#endregion General Whitelist Actions

		#region GlobalWhitelist Member Actions

        public ulong[] GetGroupMembers(GlobalWhitelistSet group, GlobalWhitelistType type, int page)
        {
            ulong[] results = null;

            using (var uow = _db.UnitOfWork)
            {
                var bc = uow.BotConfig.GetOrCreate();
                uow._context.SaveChanges();

                results = group.GlobalWhitelistItemSets
                  .Join(bc.GlobalWhitelistMembers.Where(i => i.Type == type), 
                      p => p.ItemPK, 
                      m => m.Id, 
                      (pair,member) => member.ItemId)
                  .ToArray();

                uow.Complete();
            }

            //System.Console.WriteLine("Type {0} Results: {1}", type, results.Length);

            return results;
        }

        public bool IsMemberInGroup(ulong id, GlobalWhitelistSet group)
        {
            var result = true;

            using (var uow = _db.UnitOfWork)
            {
                var bc = uow.BotConfig.GetOrCreate(set => set
                    .Include(x => x.GlobalWhitelistMembers));
                
                var temp = bc.GlobalWhitelistMembers
                .Where( x => x.ItemId == id ) // GWLItem.Id for which id is the GWLItem.ItemId
                .Join(
                    uow._context.Set<GlobalWhitelistItemSet>(), 
                    m => m.Id, i => i.ItemPK,
                    (m,i) => new {
                        m.ItemId,
                        i.ListPK
                    })
                .Where( y => y.ListPK == group.Id ) // Temporary table for which list matches group
                .FirstOrDefault();

                uow.Complete();
                
                if (temp != null) {
                  //System.Console.WriteLine("Item {0}, List {0}", temp.ItemId,temp.ListPK);
                  result = true;
                } else {
                  //System.Console.WriteLine("Null!");
                  result = false;
                }
            }
            return result;
        }

        public bool AddItemToGroup(ulong id, GlobalWhitelistType type, GlobalWhitelistSet group)
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
                    //uow._context.Set<GlobalWhitelistItem>().Add(item); // don't do this as it sets BotConfigId to null!
                    uow._context.SaveChanges();
                    uow._context.Set<GlobalWhitelistItemSet>().Add(new GlobalWhitelistItemSet
                    {
                        ListPK = group.Id,
                        ItemPK = item.Id
                    });
                } else {
                    // Second Check that id is not already in the group
                    var itemInGroup = group.GlobalWhitelistItemSets
                        .FirstOrDefault(x => x.ItemPK == item.Id);

                    if (itemInGroup != null) return false; // already exists!
                    
                    // Third Create a new WhitelistItemSet
                    itemInGroup = new GlobalWhitelistItemSet
                    {
                        ListPK = group.Id,
                        ItemPK = item.Id
                    };

                    // Finally add the WhitelistItemSet
                    uow._context.Set<GlobalWhitelistItemSet>().Add(itemInGroup);
                }
                uow.Complete();
            }
            return true;
        }
        public bool RemoveItemFromGroup(ulong id, GlobalWhitelistType type, GlobalWhitelistSet group)
        {
            using (var uow = _db.UnitOfWork)
            {
                bool exists;
                GlobalWhitelistItem item;

                // Get the item
                exists = GetMemberByIdType(id, type, out item);

                System.Console.WriteLine("Item id {0}, type {1}, dId {2}", item.Id, item.Type, item.ItemId );
                System.Console.WriteLine("Group id {0}, name {1}", group.Id, group.ListName);

                if (item == null) return false;

                // Second, Find the ItemInSet record
                var itemInSet = group.GlobalWhitelistItemSets
                    .FirstOrDefault(x => x.ItemPK == item.Id);

                // Finally remove the Item from the Set
                uow._context.Set<GlobalWhitelistItemSet>().Remove(itemInSet);

                uow.Complete();
            }
            return true;
        }

        public bool GetMemberByIdType(ulong id, GlobalWhitelistType type, out GlobalWhitelistItem item)
        {
            item = null;

            using (var uow = _db.UnitOfWork)
            {
                var bc = uow.BotConfig.GetOrCreate(set => set
                    .Include(x => x.GlobalWhitelistMembers));

                // Retrieve the member item given name
                item = bc.GlobalWhitelistMembers
                    .Where( x => x.Type.Equals(type) )
                    .Where( x => x.ItemId.Equals(id) )
                    .FirstOrDefault();

                if (item == null) { return false; }
                else { return true; }
            }
        }

		public bool AddItemToGroupBulk(ulong[] items, GlobalWhitelistType type, GlobalWhitelistSet group, out ulong[] successList)
		{
			successList = null;

			using (var uow = _db.UnitOfWork)
            {
				// Get the necessary botconfig ID
				var bc = uow.BotConfig.GetOrCreate();

				// For each non-existing member, add it to the database
				// Fetch all member names already in the database
				var curItems = uow._context.Set<GlobalWhitelistItem>()
					.Where(x => x.Type.Equals(type))
					.Select(x => x.ItemId)
					.ToArray();
				// Focus only on given names that aren't yet in the database
				var excludedItems = items.Except(curItems);
				if (excludedItems.Count() > 0) {
					string[] sqlInsertItemList = new string[excludedItems.Count()];
					for (int i=0; i<excludedItems.Count(); i++) {
						sqlInsertItemList[i] = $"({bc.Id}, {excludedItems.ElementAt(i)}, {(int)type}, datetime('now'))";
					}
					string sqlInsertItem = "INSERT INTO GlobalWhitelistItem ('BotConfigId', 'ItemId', 'Type', 'DateAdded') VALUES " + string.Join(", ", sqlInsertItemList) + ";";
					//System.Console.WriteLine(sqlInsertItem);
					uow._context.Database.ExecuteSqlCommand(sqlInsertItem);
					uow._context.SaveChanges();
				}

				// For each non-existing relationship, add it to the database
				// Fetch all member IDs existing in DB with given type and name in list
				var curIDs = uow._context.Set<GlobalWhitelistItem>()
					.Where(x => x.Type.Equals(type))
					.Where(x => items.Contains(x.ItemId))
					.Select(x => x.Id)
					.ToArray();
				// Fetch all member IDs already related to group
				var curRel = uow._context.Set<GlobalWhitelistItemSet>()
					.Where(x => x.ListPK.Equals(group.Id))
					.Select(x => x.ItemPK)
					.ToArray();
				// Focus only on given IDs that aren't yet related to group
				var excludedIDs = curIDs.Except(curRel);			
				if (excludedIDs.Count() > 0) {
					string[] sqlInsertRelList = new string[excludedIDs.Count()];
					for (int i=0; i<excludedIDs.Count(); i++) {
						sqlInsertRelList[i] = $"({group.Id}, {excludedIDs.ElementAt(i)})";
					}
					string sqlInsertRel = "INSERT INTO GlobalWhitelistItemSet ('ListPK', 'ItemPK') VALUES " + string.Join(", ", sqlInsertRelList) + ";";
					//System.Console.WriteLine(sqlInsertRel);
					uow._context.Database.ExecuteSqlCommand(sqlInsertRel);
					uow._context.SaveChanges();
				}				

				// Return list of all newly added relationships
				successList = uow._context.Set<GlobalWhitelistItem>()
					.Where(x => excludedIDs.Contains(x.Id))
					.Select(x => x.ItemId)
					.ToArray();

				uow.Complete();
				if (successList.Count() > 0) return true;
				return false;
			}
		}

		public bool RemoveItemFromGroupBulk(ulong[] items, GlobalWhitelistType type, GlobalWhitelistSet group, out ulong[] successList)
		{
			successList = null;

			using (var uow = _db.UnitOfWork)
            {
				// For each non-existing relationship, add it to the database
				// Fetch all member IDs existing in DB with given type and name in list
				var curIDs = uow._context.Set<GlobalWhitelistItem>()
					.Where(x => x.Type.Equals(type))
					.Where(x => items.Contains(x.ItemId))
					.Select(x => x.Id)
					.ToArray();
				// Fetch all member IDs related to the given group BEFORE delete
				var relIDs = uow._context.Set<GlobalWhitelistItemSet>()
					.Where(x => x.ListPK.Equals(group.Id))
					.Where(x => curIDs.Contains(x.ItemPK))
					.Select(x => x.ItemPK)
					.ToArray();

				// Delete all existing IDs where type and name matches		
				if (curIDs.Count() > 0) {
					string sqlInsertRel = $"DELETE FROM GlobalWhitelistItemSet WHERE ListPK = {group.Id} AND ItemPK IN (" + string.Join(", ", curIDs) + ");";
					//System.Console.WriteLine(sqlInsertRel);
					uow._context.Database.ExecuteSqlCommand(sqlInsertRel);
					uow._context.SaveChanges();
				}
				// Fetch all member IDs related to the given group AFTER delete
				var relIDsRemain = uow._context.Set<GlobalWhitelistItemSet>()
					.Where(x => x.ListPK.Equals(group.Id))
					.Where(x => curIDs.Contains(x.ItemPK))
					.Select(x => x.ItemPK)
					.ToArray();

				var deletedIDs = relIDs.Except(relIDsRemain);

				// Return list of all deleted relationships
				successList = uow._context.Set<GlobalWhitelistItem>()
					.Where(x => deletedIDs.Contains(x.Id))
					.Select(x => x.ItemId)
					.ToArray();

				uow.Complete();
				if (successList.Count() > 0) return true;
				return false;
			}
		}

		#endregion GlobalWhitelist Member Actions

		#region UnblockedCmdOrMdl Actions

		public string[] GetGroupNamesFromUbItem(string name, UnblockedType type)
		{
			string[] names;

			// Get the item
			UnblockedCmdOrMdl item;
			bool exists = GetUbItemByNameType(name, type, out item);

			if (!exists) return null;

			using (var uow = _db.UnitOfWork)
            {
                // Retrieve a list of set names linked to GlobalUnblockedSets.ListPK
                names = uow._context.Set<GlobalWhitelistSet>()
					.Join(
						uow._context.Set<GlobalUnblockedSet>().Where(u => u.UnblockedPK.Equals(item.Id)), 
						g => g.Id, gu => gu.ListPK,
						(group, relation) => (group.IsEnabled) ? enabledText + group.ListName : disabledText + group.ListName
						)
					.ToArray();
                uow.Complete();
            }
			return names;
		}

		public bool ClearGroupUbItems(GlobalWhitelistSet group)
		{
			int result;
			using (var uow = _db.UnitOfWork)
			{
				string sql = "DELETE FROM GlobalUnblockedSet WHERE GlobalUnblockedSet.ListPK = " + group.Id + ";";
				result = uow._context.Database.ExecuteSqlCommand(sql);
				uow._context.SaveChanges();
				uow.Complete();
			}
			//System.Console.WriteLine("Query Result: ",result);
			return true;
		}

        public bool AddUbItemToGroup(string name, UnblockedType type, GlobalWhitelistSet group)
        {   
            GlobalUnblockedSet itemInGroup;

            using (var uow = _db.UnitOfWork)
            {
                var bc = uow.BotConfig.GetOrCreate(set => set
                    .Include(x => x.UnblockedModules)
                      .ThenInclude(x => x.GlobalUnblockedSets)
                    .Include(x => x.UnblockedCommands)
                      .ThenInclude(x => x.GlobalUnblockedSets));

                // Get the item
                UnblockedCmdOrMdl item;
				bool exists = GetUbItemByNameType(name, type, out item);

                // Check if item already exists
                if (!exists) {
                    // Add new item to DB
                    item = new UnblockedCmdOrMdl
                    {
                        Name = name,
						Type = type
                    };                        
                    if (type == UnblockedType.Command) {
                      bc.UnblockedCommands.Add(item);
                    } else {
                      bc.UnblockedModules.Add(item);
                    }
                    uow.Complete();
                }

                // Check if relationship already exists
                itemInGroup = group.GlobalUnblockedSets
                    .FirstOrDefault(x => x.UnblockedPK == item.Id);

                if (itemInGroup != null) return false; // already exists!
                
                // Add relationship to DB
                itemInGroup = new GlobalUnblockedSet
                {
                    ListPK = group.Id,
                    UnblockedPK = item.Id
                };
                uow._context.Set<GlobalUnblockedSet>().Add(itemInGroup);
                uow.Complete();
            }
            return true;
        }
        public bool RemoveUbItemFromGroup(string name, UnblockedType type, GlobalWhitelistSet group)
        {
            using (var uow = _db.UnitOfWork)
            {
				// Get the item
                UnblockedCmdOrMdl item;
				bool exists = GetUbItemByNameType(name, type, out item);

                if (!exists) return false;

                // Second, Find the relationship record
                var itemInSet = group.GlobalUnblockedSets
                    .FirstOrDefault(x => x.UnblockedPK == item.Id);

                // Finally remove the Item from the Set
                uow._context.Set<GlobalUnblockedSet>().Remove(itemInSet);

                uow.Complete();
            }
            return true;
        }

        public bool GetUbItemByNameType(string name, UnblockedType type, out UnblockedCmdOrMdl item)
        {
            item = null;

            if (string.IsNullOrWhiteSpace(name)) return false;

            using (var uow = _db.UnitOfWork)
            {
                // Retrieve the UnblockedCmdOrMdl item given name
                item = uow._context.Set<UnblockedCmdOrMdl>()
					.Where( x => x.Name.Equals(name) )
					.Where( x => x.Type.Equals(type) )
					.FirstOrDefault();

				uow.Complete();

                if (item == null) { return false; }
                else { return true; }
            }
        }

		public bool AddUbItemToGroupBulk(string[] names, UnblockedType type, GlobalWhitelistSet group, out string[] successList)
		{
			successList = null;

			using (var uow = _db.UnitOfWork)
            {
				// Get the necessary botconfig ID
				var bc = uow.BotConfig.GetOrCreate();
				var bcField = (type.Equals(UnblockedType.Module)) ? "BotConfigId1" : "BotConfigId";

				// For each non-existing ub, add it to the database
				// Fetch all ub names already in the database
				var curNames = uow._context.Set<UnblockedCmdOrMdl>()
					.Where(x => x.Type.Equals(type))
					.Select(x => x.Name)
					.ToArray();
				// Focus only on given names that aren't yet in the database
				var excludedNames = names.Except(curNames);
				if (excludedNames.Count() > 0) {
					string[] sqlInsertItemList = new string[excludedNames.Count()];
					for (int i=0; i<excludedNames.Count(); i++) {
						sqlInsertItemList[i] = $"({bc.Id}, '{excludedNames.ElementAt(i)}', {(int)type}, datetime('now'))";
					}
					string sqlInsertItem = $"INSERT INTO UnblockedCmdOrMdl ('{bcField}', 'Name', 'Type', 'DateAdded') VALUES " + string.Join(", ", sqlInsertItemList) + ";";
					//System.Console.WriteLine(sqlInsertItem);
					uow._context.Database.ExecuteSqlCommand(sqlInsertItem);
					uow._context.SaveChanges();
				}

				// For each non-existing relationship, add it to the database
				// Fetch all ub IDs existing in DB with given type and name in list
				var curIDs = uow._context.Set<UnblockedCmdOrMdl>()
					.Where(x => x.Type.Equals(type))
					.Where(x => names.Contains(x.Name))
					.Select(x => x.Id)
					.ToArray();
				// Fetch all ub IDs already related to group
				var curRel = uow._context.Set<GlobalUnblockedSet>()
					.Where(x => x.ListPK.Equals(group.Id))
					.Select(x => x.UnblockedPK)
					.ToArray();
				// Focus only on given IDs that aren't yet related to group
				var excludedIDs = curIDs.Except(curRel);			
				if (excludedIDs.Count() > 0) {
					string[] sqlInsertRelList = new string[excludedIDs.Count()];
					for (int i=0; i<excludedIDs.Count(); i++) {
						sqlInsertRelList[i] = $"({group.Id}, {excludedIDs.ElementAt(i)})";
					}
					string sqlInsertRel = "INSERT INTO GlobalUnblockedSet ('ListPK', 'UnblockedPK') VALUES " + string.Join(", ", sqlInsertRelList) + ";";
					//System.Console.WriteLine(sqlInsertRel);
					uow._context.Database.ExecuteSqlCommand(sqlInsertRel);
					uow._context.SaveChanges();
				}				

				// Return list of all newly added relationships
				successList = uow._context.Set<UnblockedCmdOrMdl>()
					.Where(x => excludedIDs.Contains(x.Id))
					.Select(x => x.Name)
					.ToArray();

				uow.Complete();
				if (successList.Count() > 0) return true;
				return false;
			}
		}

		public bool RemoveUbItemFromGroupBulk(string[] names, UnblockedType type, GlobalWhitelistSet group, out string[] successList)
		{
			successList = null;

			using (var uow = _db.UnitOfWork)
            {
				// For each non-existing relationship, add it to the database
				// Fetch all ub IDs existing in DB with given type and name in list
				var curIDs = uow._context.Set<UnblockedCmdOrMdl>()
					.Where(x => x.Type.Equals(type))
					.Where(x => names.Contains(x.Name))
					.Select(x => x.Id)
					.ToArray();
				// Fetch all ub IDs related to the given group BEFORE delete
				var relIDs = uow._context.Set<GlobalUnblockedSet>()
					.Where(x => x.ListPK.Equals(group.Id))
					.Where(x => curIDs.Contains(x.UnblockedPK))
					.Select(x => x.UnblockedPK)
					.ToArray();

				// Delete all existing IDs where type and name matches		
				if (curIDs.Count() > 0) {
					string sqlInsertRel = $"DELETE FROM GlobalUnblockedSet WHERE ListPK = {group.Id} AND UnblockedPK IN (" + string.Join(", ", curIDs) + ");";
					//System.Console.WriteLine(sqlInsertRel);
					uow._context.Database.ExecuteSqlCommand(sqlInsertRel);
					uow._context.SaveChanges();
				}
				// Fetch all ub IDs related to the given group AFTER delete
				var relIDsRemain = uow._context.Set<GlobalUnblockedSet>()
					.Where(x => x.ListPK.Equals(group.Id))
					.Where(x => curIDs.Contains(x.UnblockedPK))
					.Select(x => x.UnblockedPK)
					.ToArray();

				var deletedIDs = relIDs.Except(relIDsRemain);

				// Return list of all deleted relationships
				successList = uow._context.Set<UnblockedCmdOrMdl>()
					.Where(x => deletedIDs.Contains(x.Id))
					.Select(x => x.Name)
					.ToArray();

				uow.Complete();
				if (successList.Count() > 0) return true;
				return false;
			}
		}

		public string[] GetGroupUnblockedNames(GlobalWhitelistSet group, UnblockedType type)
		{
			string[] names;
			using (var uow = _db.UnitOfWork)
            {
                // Retrieve a list of unblocked names linked to group on GlobalUnblockedSets.UnblockedPK
                names = group.GlobalUnblockedSets
					.Join(
						uow._context.Set<UnblockedCmdOrMdl>().Where(u => u.Type.Equals(type)), 
						gu => gu.UnblockedPK, u => u.Id,
						(relation, unblocked) => unblocked.Name
						)
					.ToArray();
                uow.Complete();
            }
			return names;
		}

		public string[] GetUnblockedNames(UnblockedType type)
		{
			string[] nameCounts;
			using (var uow = _db.UnitOfWork)
            {
                // Retrieve a list of unblocked names with at least one relationship record
                var anon = uow._context.Set<UnblockedCmdOrMdl>()
					.Where(u => u.Type.Equals(type))
					.GroupJoin(
						uow._context.Set<GlobalUnblockedSet>(), 
						u => u.Id, gu => gu.UnblockedPK,
						(unblocked, relations) => new {
							Name = unblocked.Name,
							NumRelations = relations.Count()
						})
					.ToArray();

				uow.Complete();

				nameCounts = new string[anon.Length];
				for (int i=0; i<anon.Length; i++)
				{
					if (anon[i].NumRelations > 0) 
					{
						string lists = (anon[i].NumRelations > 1) ? " lists)" : " list)";
						nameCounts[i] = anon[i].Name + " (in " + anon[i].NumRelations + lists;
					} else {
						nameCounts[i] = anon[i].Name + " (unassigned)";
					}
				}                
            }
			return nameCounts;
		}

		public string[] GetUnblockedNamesForMember(UnblockedType type, ulong id, GlobalWhitelistType memType)
		{
			string[] names;
			using (var uow = _db.UnitOfWork)
            {
                names = uow._context.Set<GlobalWhitelistItem>()
					.Where(x => x.ItemId.Equals(id))
					.Where(x => x.Type.Equals(memType))
					.Join(uow._context.Set<GlobalWhitelistItemSet>(),
						i => i.Id, gi => gi.ItemPK,
						(i, gi) => gi.ListPK)
					.Join(uow._context.Set<GlobalWhitelistSet>()
						.Where(g => g.IsEnabled.Equals(true)),
						listPK => listPK, g => g.Id,
						(listPK, g) => g.Id)
					.Join(uow._context.Set<GlobalUnblockedSet>(),
						listPK => listPK, gub => gub.ListPK,
						(listPK, gub) => gub.UnblockedPK)
					.Join(uow._context.Set<UnblockedCmdOrMdl>()
						.Where(x => x.Type.Equals(type)),
						uPK => uPK, ub => ub.Id,
						(uPK, ub) => ub.Name)
					.ToArray();
				uow.Complete();
            }
			return names;
		}

		#endregion UnblockedCmdOrMdl Actions
    }
}
