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
		public readonly int numPerPage = 5;

		public enum FieldType {
			ALL = 0, EVERYTHING = ALL,
			CMD = 1, COMMAND = CMD, COMMANDS = CMD, CMDS = CMD,
			MOD = 2, MODULE = MOD, MODULES = MOD, MODS = MOD, MDL = MOD, MDLS = MOD,
			S = 3, SRVR = S, SERVER = S, SERVERS = S, SRVRS = S, 
			G = S, GUILD = G, GUILDS = G,
			C = 4, CHNL = C, CHANNEL = C, CHANNELS = C, CHNLS = C,
			U = 5, USR = U, USER = U, USERS = U, USRS = U,
			UB = 6, UNBLOCK = UB, UNBLOCKED = UB,
			MEM = 7, MEMBER = MEM, MEMBERS = MEM
		};

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
                      // str[i] = MentionUtils.MentionUser(ids[i]);
					  str[i] = MentionUtils.MentionUser(ids[i]) + "\n\t" + ids[i].ToString();
                    }
                    break;

                case GlobalWhitelistType.Channel:
                    for (var i = 0; i < ids.Length; i++) {
                      // str[i] = MentionUtils.MentionChannel(ids[i]);
					  str[i] = MentionUtils.MentionChannel(ids[i]) + "\n\t" + ids[i].ToString();
                    }
                    break;

                case GlobalWhitelistType.Server:
                    for (var i = 0; i < ids.Length; i++) {
						var guild = _client.Guilds.FirstOrDefault(g => g.Id.Equals(ids[i]));
                    	// str[i] = (guild != null) ? $"[{guild.Name}](https://discordapp.com/channels/{ids[i]}/ '{ids[i]}') " : ids[i].ToString();
						string name = (guild != null) ? guild.Name : "Null";
						str[i] = $"[{name}](https://discordapp.com/channels/{ids[i]}/ '{ids[i]}')\n\t{ids[i]}";
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
                    // str = MentionUtils.MentionUser(id);
					str = MentionUtils.MentionUser(id) + " " + id.ToString();
                    break;

                case GlobalWhitelistType.Channel:
                    // str = MentionUtils.MentionChannel(id);
					str = MentionUtils.MentionChannel(id) + " " + id.ToString();
                    break;

                case GlobalWhitelistType.Server:
					var guild = _client.Guilds.FirstOrDefault(g => g.Id.Equals(id));
                    // str = (guild != null) ? $"[{guild.Name}](https://discordapp.com/channels/{id}/ '{id}') " : id.ToString();
                    str = (guild != null) ? $"[{guild.Name}](https://discordapp.com/channels/{id}/ '{id}') {id}" : id.ToString();
					break;

                default:
                    str = id.ToString();
                    break;
            }

            return str;
        }

		#endregion Resolve ulong IDs

		#region CheckUnblocked
		public bool CheckIfUnblockedFor(string ubName, UnblockedType ubType, ulong memID, GlobalWhitelistType memType, int page, out string[] lists, out int count)
		{
			lists = null;
			using (var uow = _db.UnitOfWork)
            {
				var allnames = uow._context.Set<UnblockedCmdOrMdl>()
					.Where(x => x.Type.Equals(ubType))
					.Where(x => x.Name.Equals(ubName))
					.Join(uow._context.Set<GlobalUnblockedSet>(), 
						ub => ub.Id, gub => gub.UnblockedPK, 
						(ub,gub) => gub.ListPK)
					.Join(uow._context.Set<GlobalWhitelistSet>()
						,//.Where(g => g.IsEnabled.Equals(true)),
						gub => gub, g => g.Id,
						(gub, g) => g
						// new {
						//	 ListText = (g.IsEnabled) ? enabledText + g.ListName : disabledText + g.ListName,
						//	 g.Id
						// })
						)
					.Join(uow._context.Set<GlobalWhitelistItemSet>(),
						g => g.Id, gi => gi.ListPK,
						(g, gi) => new {
							// g.ListText,
							g,
							gi.ItemPK
						})
					.Join(uow._context.Set<GlobalWhitelistItem>()
						.Where(x => x.Type.Equals(memType))
						.Where(x => x.ItemId.Equals(memID)),
						gi => gi.ItemPK, i => i.Id,
						// (gi, i) => gi.ListText);
						(gi, i) => gi.g);
				
				uow.Complete();

				count = allnames.Count();
				if (count <= 0) return false;

				int numSkip = page*numPerPage;
				if (numSkip > count) numSkip = numPerPage * ((count-1)/numPerPage);
				// System.Console.WriteLine("Skip {0}, Count {1}, Page {2}", numSkip, count, page);

				lists = allnames
					.OrderBy(g => g.ListName.ToLowerInvariant())
					.Skip(numSkip)
                	.Take(numPerPage)
					.Select(g => (g.IsEnabled) ? enabledText + g.ListName : disabledText + g.ListName)
					.ToArray();
			}
			return true;
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
				
				uow.Complete();

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
                bc.GlobalWhitelistGroups.Add(group); // Necessary for setting BotConfigId

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
                uow.Complete();
            }
			return true;
		}

		public bool ClearGroupMembers(GlobalWhitelistSet group)
		{
			int result;
			using (var uow = _db.UnitOfWork)
			{
				string sql = "DELETE FROM GlobalWhitelistItemSet WHERE GlobalWhitelistItemSet.ListPK = @p0;";
				result = uow._context.Database.ExecuteSqlCommand(sql, group.Id);
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
				if (numSkip >= count) numSkip = numPerPage * ((count-1)/numPerPage);
				// System.Console.WriteLine("Skip {0}, Count {1}, Page {2}", numSkip, count, page);

				names = uow._context.Set<GlobalWhitelistSet>()
					.OrderBy(g => g.ListName.ToLowerInvariant())
					.Skip(numSkip)
                	.Take(numPerPage)
					.Select(g => (g.IsEnabled) ? enabledText + g.ListName : disabledText + g.ListName)
					.ToArray();

                uow.Complete();
            }
            return true;
        }

        

        public bool GetGroupByName(string listName, out GlobalWhitelistSet group)
        {
            group = null;

            if (string.IsNullOrWhiteSpace(listName)) return false;

            using (var uow = _db.UnitOfWork)
            {
                group = uow._context.Set<GlobalWhitelistSet>()
					.Where(x => x.ListName.ToLowerInvariant().Equals(listName))
					.Include(x => x.GlobalUnblockedSets)
					.Include(x => x.GlobalWhitelistItemSets)
					.FirstOrDefault();

                if (group == null) { return false; }
                else { return true; }
            }
        }

		#endregion General Whitelist Actions

		#region GlobalWhitelist Member Actions

        public bool GetGroupMembers(GlobalWhitelistSet group, GlobalWhitelistType type, int page, out ulong[] results, out int count)
        {
            results = null;
            using (var uow = _db.UnitOfWork)
            {
                var anon = group.GlobalWhitelistItemSets
                	.Join(uow._context.Set<GlobalWhitelistItem>()
				  		.Where(m => m.Type.Equals(type)), 
							p => p.ItemPK, 
							m => m.Id, 
							(pair,member) => member.ItemId)
					.OrderBy(id => id);
                uow.Complete();

				count = anon.Count();
				if (count <= 0) return false;

				int numSkip = page*numPerPage;
				if (numSkip >= count) numSkip = numPerPage * ((count-1)/numPerPage);
				
				results = anon.Skip(numSkip).Take(numPerPage).ToArray();
            }
            return true;
        }

        public bool GetMemberByIdType(ulong id, GlobalWhitelistType type, out GlobalWhitelistItem item)
        {
            item = null;

            using (var uow = _db.UnitOfWork)
            {
                // Retrieve the member item given name
                item = uow._context.Set<GlobalWhitelistItem>()
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
				// Focus only on given names that aren't yet in the database, while simultaneously removing dupes
				var excludedItems = items.Except(curItems);
				if (excludedItems.Count() > 0) {
					for (int i=0; i<excludedItems.Count(); i++) {
						uow._context.Database.ExecuteSqlCommand(
							"INSERT INTO GlobalWhitelistItem ('BotConfigId', 'ItemId', 'Type', 'DateAdded') VALUES (@p0,@p1,@p2,datetime('now'));", 
							bc.Id, 
							excludedItems.ElementAt(i), 
							(int)type);
						// System.Console.WriteLine("Result {0}: {1}", i, resultInsert);
					}
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
				// Focus only on given IDs that aren't yet related to group (automatically removes dupes)
				var excludedIDs = curIDs.Except(curRel);			
				if (excludedIDs.Count() > 0) {
					for (int i=0; i<excludedIDs.Count(); i++) {
						uow._context.Database.ExecuteSqlCommand(
							"INSERT INTO GlobalWhitelistItemSet ('ListPK', 'ItemPK') VALUES (@p0,@p1);", 
							group.Id, 
							excludedIDs.ElementAt(i));
						// System.Console.WriteLine("Result {0}: {1}", i, resultInsert);
					}
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

				// Delete all existing IDs where type and name matches (above lines ensure no dupes)	
				if (curIDs.Count() > 0) {
					for (int i=0; i<curIDs.Count(); i++) {
						uow._context.Database.ExecuteSqlCommand(
							"DELETE FROM GlobalWhitelistItemSet WHERE ListPK = @p0 AND ItemPK = @p1;",
							group.Id,
							curIDs[i]);
						// System.Console.WriteLine("Remove Result {0}: {1}", i, resultRemove);
					}
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

		

		public bool ClearGroupUbItems(GlobalWhitelistSet group)
		{
			int result;
			using (var uow = _db.UnitOfWork)
			{
				string sql = "DELETE FROM GlobalUnblockedSet WHERE GlobalUnblockedSet.ListPK = @p0;";
				result = uow._context.Database.ExecuteSqlCommand(sql, group.Id);
				uow.Complete();
			}
			//System.Console.WriteLine("Query Result: ",result);
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
				// Focus only on given names that aren't yet in the database (and auto remove dupes)
				var excludedNames = names.Except(curNames);
				if (excludedNames.Count() > 0) {
					for (int i=0; i<excludedNames.Count(); i++) {
						uow._context.Database.ExecuteSqlCommand(
							$"INSERT INTO UnblockedCmdOrMdl ('{bcField}', 'Name', 'Type', 'DateAdded') VALUES (@p0,@p1,@p2,datetime('now'));", 
							bc.Id,
							excludedNames.ElementAt(i),
							(int)type);
						// System.Console.WriteLine("Result {0}: {1}", i, resultInsert);
					}
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
				// Focus only on given IDs that aren't yet related to group (and auto remove dupes)
				var excludedIDs = curIDs.Except(curRel);			
				if (excludedIDs.Count() > 0) {
					for (int i=0; i<excludedIDs.Count(); i++) {
						uow._context.Database.ExecuteSqlCommand(
							"INSERT INTO GlobalUnblockedSet ('ListPK', 'UnblockedPK') VALUES (@p0,@p1);", 
							group.Id,
							excludedIDs.ElementAt(i));
						// System.Console.WriteLine("Result {0}: {1}", i, resultInsert);
					}
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

				// Delete all existing IDs where type and name matches (above lines ensure no dupes)	
				if (curIDs.Count() > 0) {
					for (int i=0; i<curIDs.Count(); i++) {
						uow._context.Database.ExecuteSqlCommand(
							"DELETE FROM GlobalUnblockedSet WHERE ListPK = @p0 AND UnblockedPK = @p1;",
							group.Id,
							curIDs[i]);
						// System.Console.WriteLine("Remove Result {0}: {1}", i, resultRemove);
					}
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

		public bool GetGroupUnblockedNames(GlobalWhitelistSet group, UnblockedType type, int page, out string[] names, out int count)
		{
			names = null;
			using (var uow = _db.UnitOfWork)
            {
                // Retrieve a list of unblocked names linked to group on GlobalUnblockedSets.UnblockedPK
                var anon = group.GlobalUnblockedSets
					.Join(uow._context.Set<UnblockedCmdOrMdl>()
					  	.Where(u => u.Type.Equals(type)), 
							gu => gu.UnblockedPK, u => u.Id,
							(relation, unblocked) => unblocked.Name)
					.OrderBy(a => a.ToLowerInvariant());
                uow.Complete();

				count = anon.Count();
				if (count <= 0) return false;

				int numSkip = page*numPerPage;
				if (numSkip >= count) numSkip = numPerPage * ((count-1)/numPerPage);
				
				names = anon.Skip(numSkip).Take(numPerPage).ToArray();
            }
			return true;
		}

		public bool GetUnblockedNames(UnblockedType type, int page, out string[] names, out int count)
		{
			names = null;
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
					.OrderBy(a => a.Name.ToLowerInvariant());

				uow.Complete();

				count = anon.Count();
				if (count <= 0) return false;

				int numSkip = page*numPerPage;
				if (numSkip >= count) numSkip = numPerPage * ((count-1)/numPerPage);
				
				var subset = anon.Skip(numSkip).Take(numPerPage).ToArray();

				names = new string[subset.Count()];
				for (int i=0; i<subset.Length; i++)
				{
					if (subset[i].NumRelations > 0) 
					{
						string lists = (subset[i].NumRelations > 1) ? " lists)" : " list)";
						names[i] = subset[i].Name + " (" + subset[i].NumRelations + lists;
					} else {
						names[i] = subset[i].Name + " (0 lists)";
					}
				}
            }
			return true;
		}

		public bool GetUnblockedNamesForMember(UnblockedType type, ulong id, GlobalWhitelistType memType, int page, out string[] names, out int count)
		{
			names= null;
			using (var uow = _db.UnitOfWork)
            {
                var anon = uow._context.Set<GlobalWhitelistItem>()
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
					.OrderBy(a => a.ToLowerInvariant());
				uow.Complete();

				count = anon.Count();
				if (count <= 0) return false;

				int numSkip = page*numPerPage;
				if (numSkip >= count) numSkip = numPerPage * ((count-1)/numPerPage);
				
				names = anon.Skip(numSkip).Take(numPerPage).ToArray();
            }
			return true;
		}

		#endregion UnblockedCmdOrMdl Actions

		#region Clear

		public bool ClearAll(GlobalWhitelistSet group)
		{
			return ClearMembers(group) && ClearUnblocked(group);
		}

		public bool ClearMembers(GlobalWhitelistSet group)
		{
			int result;
			using (var uow = _db.UnitOfWork)
			{
				string sql = "DELETE FROM GlobalWhitelistItemSet WHERE GlobalWhitelistItemSet.ListPK = @p0;";
				result = uow._context.Database.ExecuteSqlCommand(sql, group.Id);
				uow.Complete();
			}
			//System.Console.WriteLine("Query Result: ",result);
			return true;
		}

		public bool ClearMembers(GlobalWhitelistSet group, GlobalWhitelistType type)
		{
			int result;
			using (var uow = _db.UnitOfWork)
			{
				string sql = @"DELETE FROM GlobalWhitelistItemSet WHERE GlobalWhitelistItemSet.ListPK = @p0 AND GlobalWhitelistItemSet.ItemPK IN
					(SELECT Id FROM GlobalWhitelistItem WHERE GlobalWhitelistItem.Type = @p1);";
				result = uow._context.Database.ExecuteSqlCommand(sql, group.Id, type);
				uow.Complete();
			}
			return true;
		}

		public bool ClearUnblocked(GlobalWhitelistSet group)
		{
			int result;
			using (var uow = _db.UnitOfWork)
			{
				string sql = "DELETE FROM GlobalUnblockedSet WHERE GlobalUnblockedSet.ListPK = @p0;";
				result = uow._context.Database.ExecuteSqlCommand(sql, group.Id);
				uow.Complete();
			}
			//System.Console.WriteLine("Query Result: ",result);
			return true;
		}
		public bool ClearUnblocked(GlobalWhitelistSet group, UnblockedType type)
		{
			int result;
			using (var uow = _db.UnitOfWork)
			{
				string sql = @"DELETE FROM GlobalUnblockedSet WHERE GlobalUnblockedSet.ListPK = @p0 AND GlobalUnblockedSet.UnblockedPK IN
					(SELECT Id FROM UnblockedCmdOrMdl WHERE UnblockedCmdOrMdl.Type = @p1);";
				result = uow._context.Database.ExecuteSqlCommand(sql, group.Id, type);
				uow.Complete();
			}
			return true;
		}

		#endregion Clear

		#region Purge

		public bool PurgeMember(GlobalWhitelistType type, ulong id)
		{
			using (var uow = _db.UnitOfWork)
			{
				uow._context.Set<GlobalWhitelistItem>().Remove( 
					uow._context.Set<GlobalWhitelistItem>()
					.Where( x => x.Type.Equals(type) )
					.Where( x => x.ItemId.Equals(id) )
					.FirstOrDefault()
				);
				uow.Complete();
			}
			return true;
		}

		public bool PurgeUnblocked(UnblockedType type, string name)
		{
			using (var uow = _db.UnitOfWork)
			{
				/*var bc = uow.BotConfig.GetOrCreate(set => set.Include(x => x.UnblockedCommands));
				bc.UnblockedCommands.RemoveWhere(x => x.Name == itemName);
				uow.Complete();*/ // this only sets the BotConfigId FK to null

				// Delete the unblockedcmd record and all relation records
				uow._context.Set<UnblockedCmdOrMdl>().Remove( 
					uow._context.Set<UnblockedCmdOrMdl>()
					.Where( x => x.Type.Equals(type) )
					.Where( x => x.Name.Equals(name) )
					.FirstOrDefault()
				);
				uow.Complete();
			}
			return true;
		}

		#endregion Purge

		#region IsInGroup

		public bool IsMemberInGroup(ulong id, GlobalWhitelistType type, GlobalWhitelistSet group)
        {
            var result = true;

            using (var uow = _db.UnitOfWork)
            {
                var temp = uow._context.Set<GlobalWhitelistItem>()
					.Where( x => x.Type.Equals(type) )
					.Where( x => x.ItemId.Equals(id) )
					.Join(
						uow._context.Set<GlobalWhitelistItemSet>(), 
						i => i.Id, gi => gi.ItemPK,
						(i,gi) => new {
							i.ItemId,
							gi.ListPK
						})
					.Where( y => y.ListPK.Equals(group.Id) )
					.FirstOrDefault();

                uow.Complete();
                
                if (temp != null) {
                  result = true;
                } else {
                  result = false;
                }
            }
            return result;
        }
		public bool IsUnblockedInGroup(string name, UnblockedType type, GlobalWhitelistSet group)
		{
			var result = true;

            using (var uow = _db.UnitOfWork)
            {
                var temp = uow._context.Set<UnblockedCmdOrMdl>()
					.Where( x => x.Type.Equals(type) )
					.Where( x => x.Name.Equals(name) )
					.Join(
						uow._context.Set<GlobalUnblockedSet>(), 
						u => u.Id, gu => gu.UnblockedPK,
						(u,gu) => new {
							u.Name,
							gu.ListPK
						})
					.Where( y => y.ListPK.Equals(group.Id) )
					.FirstOrDefault();

                uow.Complete();
                
                if (temp != null) {
                  result = true;
                } else {
                  result = false;
                }
            }
            return result;
		}

		#endregion IsInGroup
	
		public bool GetGroupNamesByMember(ulong id, GlobalWhitelistType type, int page, out string[] names, out int count)
        {
            names = null;
            using (var uow = _db.UnitOfWork)
            {
				var allnames = uow._context.Set<GlobalWhitelistItem>()
					.Where(i => i.Type.Equals(type))
					.Where(i => i.ItemId.Equals(id))
					.Join(uow._context.Set<GlobalWhitelistItemSet>(),
						i => i.Id, gi => gi.ItemPK,
						(i, gi) => gi.ListPK)
					.Join(uow._context.Set<GlobalWhitelistSet>(),
						listPK => listPK, g => g.Id,
						(listPK, g) => g);
				
				count = allnames.Count();
				if (count <= 0) return false;

				int numSkip = page*numPerPage;
				if (numSkip >= count) numSkip = numPerPage * ((count-1)/numPerPage);
				// System.Console.WriteLine("Skip {0}, Count {1}, Page {2}", numSkip, count, page);

				names = allnames
					.OrderBy(g => g.ListName.ToLowerInvariant())
					.Skip(numSkip)
                	.Take(numPerPage)
					.Select(g => (g.IsEnabled) ? enabledText + g.ListName : disabledText + g.ListName)
					.ToArray();

                uow.Complete();
            }
            return true;
        }
		public bool GetGroupNamesByUnblocked(string name, UnblockedType type, int page, out string[] names, out int count)
		{
			names = null;
			count = 0;

			// Get the item
			UnblockedCmdOrMdl item;
			bool exists = GetUbItemByNameType(name, type, out item);

			if (!exists) return false;

			using (var uow = _db.UnitOfWork)
            {
                // Retrieve a list of set names linked to GlobalUnblockedSets.ListPK
                var allnames = uow._context.Set<GlobalWhitelistSet>()
					.Join(
						uow._context.Set<GlobalUnblockedSet>()
							.Where(u => u.UnblockedPK.Equals(item.Id)), 
						g => g.Id, gu => gu.ListPK,
						// (group, relation) => (group.IsEnabled) ? enabledText + group.ListName : disabledText + group.ListName
						(group, relation) => group);
                uow.Complete();

				count = allnames.Count();
				if (count <= 0) return false;

				int numSkip = page*numPerPage;
				if (numSkip >= count) numSkip = numPerPage * ((count-1)/numPerPage);
				// System.Console.WriteLine("Skip {0}, Count {1}, Page {2}", numSkip, count, page);

				names = allnames
					.OrderBy(g => g.ListName.ToLowerInvariant())
					.Skip(numSkip)
                	.Take(numPerPage)
					.Select(g => (g.IsEnabled) ? enabledText + g.ListName : disabledText + g.ListName)
					.ToArray();
            }
			return true;
		}

	}
}
