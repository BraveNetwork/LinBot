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
			A = 0, ALL = A, EVERYTHING = A, GENERAL = A, GEN = A,
			CMD = 1, COMMAND = CMD, COMMANDS = CMD, CMDS = CMD,
			MOD = 2, MODULE = MOD, MODULES = MOD, MODS = MOD, MDL = MOD, MDLS = MOD,
			S = 3, SRVR = S, SERVER = S, SERVERS = S, SRVRS = S, 
			G = S, GUILD = G, GUILDS = G,
			C = 4, CHNL = C, CHANNEL = C, CHANNELS = C, CHNLS = C,
			U = 5, USR = U, USER = U, USERS = U, USRS = U,
			R = 6, ROLE = R, ROLES = R,
			UB = 7, UNBLOCK = UB, UNBLOCKED = UB,
			M = 8, MEM = M, MEMBER = M, MEMBERS = M
		};

        public GlobalWhitelistService(DiscordSocketClient client, DbService db)
        {
            _db = db;
			_client = client;
		}

		#region General Whitelist Actions

        public bool CreateWhitelist(string listName, GWLType type)
        {
            using (var uow = _db.UnitOfWork)
            {
				uow._context.Database.ExecuteSqlCommand(
					"INSERT INTO GWLSet ('ListName', 'Type') VALUES (@p0,@p1);",
					listName,
					type);

                uow.Complete();
            }

            return true;
        }

		public bool RenameWhitelist(string oldName, string listName)
		{
			using (var uow = _db.UnitOfWork)
            {
                GWLSet group = uow._context.Set<GWLSet>()
					.Where(g => g.ListName.ToLowerInvariant().Equals(oldName))
					.SingleOrDefault();

				if (group == null) return false;

				group.ListName = listName;				
                uow.Complete();
            }
			return true;
		}

		public bool SetEnabledStatus(string listName, bool status)
		{
			using (var uow = _db.UnitOfWork)
            {
                GWLSet group = uow._context.Set<GWLSet>()
					.Where(g => g.ListName.ToLowerInvariant().Equals(listName))
					.SingleOrDefault();

				if (group == null) return false;

				group.IsEnabled = status;
                uow.Complete();
            }
			return true;
		}

        public bool DeleteWhitelist(string listName)
        {
            using (var uow = _db.UnitOfWork)
            {
                // Delete the whitelist record and all relation records
                uow._context.Set<GWLSet>().Remove( 
                    uow._context.Set<GWLSet>()
                    .Where( x => x.ListName.ToLowerInvariant().Equals(listName) ).FirstOrDefault()
                );
                uow.Complete();
            }
            return true;
        }

		#endregion General Whitelist Actions

		#region Add/Remove

		public bool AddMemberToGroup(ulong[] items, GWLItemType type, GWLSet group, out ulong[] successList)
		{
			successList = null;

			using (var uow = _db.UnitOfWork)
            {
				// For each non-existing member, add it to the database
				// Fetch all member names already in the database
				var curItems = uow._context.Set<GWLItem>()
					.Where(x => x.Type.Equals(type))
					.Select(x => x.ItemId)
					.ToArray();
				// Focus only on given names that aren't yet in the database, while simultaneously removing dupes
				var excludedItems = items.Except(curItems);
				if (excludedItems.Count() > 0) {
					for (int i=0; i<excludedItems.Count(); i++) {
						uow._context.Database.ExecuteSqlCommand(
							"INSERT INTO GWLItem ('ItemId', 'Type') VALUES (@p0,@p1);",
							excludedItems.ElementAt(i), 
							(int)type);
						// System.Console.WriteLine("Result {0}: {1}", i, resultInsert);
					}
					uow._context.SaveChanges();
				}

				// For each non-existing relationship, add it to the database
				// Fetch all member IDs existing in DB with given type and name in list
				var curIDs = uow._context.Set<GWLItem>()
					.Where(x => x.Type.Equals(type))
					.Where(x => items.Contains(x.ItemId))
					.Select(x => x.Id)
					.ToArray();
				// Fetch all member IDs already related to group
				var curRel = uow._context.Set<GWLItemSet>()
					.Where(x => x.ListPK.Equals(group.Id))
					.Select(x => x.ItemPK)
					.ToArray();
				// Focus only on given IDs that aren't yet related to group (automatically removes dupes)
				var excludedIDs = curIDs.Except(curRel);			
				if (excludedIDs.Count() > 0) {
					for (int i=0; i<excludedIDs.Count(); i++) {
						uow._context.Database.ExecuteSqlCommand(
							"INSERT INTO GWLItemSet ('ListPK', 'ItemPK') VALUES (@p0,@p1);", 
							group.Id, 
							excludedIDs.ElementAt(i));
						// System.Console.WriteLine("Result {0}: {1}", i, resultInsert);
					}
					uow._context.SaveChanges();
				}				

				// Return list of all newly added relationships
				successList = uow._context.Set<GWLItem>()
					.Where(x => excludedIDs.Contains(x.Id))
					.Select(x => x.ItemId)
					.ToArray();

				uow.Complete();
				if (successList.Count() > 0) return true;
				return false;
			}
		}

		public bool RemoveMemberFromGroup(ulong[] items, GWLItemType type, GWLSet group, out ulong[] successList)
		{
			successList = null;

			using (var uow = _db.UnitOfWork)
            {
				// For each non-existing relationship, add it to the database
				// Fetch all member IDs existing in DB with given type and name in list
				var curIDs = uow._context.Set<GWLItem>()
					.Where(x => x.Type.Equals(type))
					.Where(x => items.Contains(x.ItemId))
					.Select(x => x.Id)
					.ToArray();
				// Fetch all member IDs related to the given group BEFORE delete
				var relIDs = uow._context.Set<GWLItemSet>()
					.Where(x => x.ListPK.Equals(group.Id))
					.Where(x => curIDs.Contains(x.ItemPK))
					.Select(x => x.ItemPK)
					.ToArray();

				// Delete all existing IDs where type and name matches (above lines ensure no dupes)	
				if (curIDs.Count() > 0) {
					for (int i=0; i<curIDs.Count(); i++) {
						uow._context.Database.ExecuteSqlCommand(
							"DELETE FROM GWLItemSet WHERE ListPK = @p0 AND ItemPK = @p1;",
							group.Id,
							curIDs[i]);
						// System.Console.WriteLine("Remove Result {0}: {1}", i, resultRemove);
					}
					uow._context.SaveChanges();					
				}
				// Fetch all member IDs related to the given group AFTER delete
				var relIDsRemain = uow._context.Set<GWLItemSet>()
					.Where(x => x.ListPK.Equals(group.Id))
					.Where(x => curIDs.Contains(x.ItemPK))
					.Select(x => x.ItemPK)
					.ToArray();

				var deletedIDs = relIDs.Except(relIDsRemain);

				// Return list of all deleted relationships
				successList = uow._context.Set<GWLItem>()
					.Where(x => deletedIDs.Contains(x.Id))
					.Select(x => x.ItemId)
					.ToArray();

				uow.Complete();
				if (successList.Count() > 0) return true;
				return false;
			}
		}

		public bool AddRoleToGroup(ulong serverID, ulong[] items, GWLSet group, out ulong[] successList)
		{
			GWLItemType type = GWLItemType.Role;
			successList = null;

			using (var uow = _db.UnitOfWork)
            {
				// For each non-existing member, add it to the database
				// Fetch all member names already in the database
				var curItems = uow._context.Set<GWLItem>()
					.Where(x => x.Type.Equals(type))
					.Where(x => x.RoleServerId.Equals(serverID))
					.Select(x => x.ItemId)
					.ToArray();
				// Focus only on given names that aren't yet in the database, while simultaneously removing dupes
				var excludedItems = items.Except(curItems);
				if (excludedItems.Count() > 0) {
					for (int i=0; i<excludedItems.Count(); i++) {
						uow._context.Database.ExecuteSqlCommand(
							"INSERT INTO GWLItem ('ItemId', 'Type', 'RoleServerId') VALUES (@p0,@p1,@p2);",
							excludedItems.ElementAt(i), 
							(int)type,
							serverID);
						// System.Console.WriteLine("Result {0}: {1}", i, resultInsert);
					}
					uow._context.SaveChanges();
				}

				// For each non-existing relationship, add it to the database
				// Fetch all member IDs existing in DB with given type and name in list
				var curIDs = uow._context.Set<GWLItem>()
					.Where(x => x.Type.Equals(type))
					.Where(x => x.RoleServerId.Equals(serverID))
					.Where(x => items.Contains(x.ItemId))
					.Select(x => x.Id)
					.ToArray();
				// Fetch all member IDs already related to group
				var curRel = uow._context.Set<GWLItemSet>()
					.Where(x => x.ListPK.Equals(group.Id))
					.Select(x => x.ItemPK)
					.ToArray();
				// Focus only on given IDs that aren't yet related to group (automatically removes dupes)
				var excludedIDs = curIDs.Except(curRel);			
				if (excludedIDs.Count() > 0) {
					for (int i=0; i<excludedIDs.Count(); i++) {
						uow._context.Database.ExecuteSqlCommand(
							"INSERT INTO GWLItemSet ('ListPK', 'ItemPK') VALUES (@p0,@p1);", 
							group.Id, 
							excludedIDs.ElementAt(i));
						// System.Console.WriteLine("Result {0}: {1}", i, resultInsert);
					}
					uow._context.SaveChanges();
				}				

				// Return list of all newly added relationships
				successList = uow._context.Set<GWLItem>()
					.Where(x => x.RoleServerId.Equals(serverID))
					.Where(x => excludedIDs.Contains(x.Id))
					.Select(x => x.ItemId)
					.ToArray();

				uow.Complete();
				if (successList.Count() > 0) return true;
				return false;
			}
		}

		public bool RemoveRoleFromGroup(ulong serverID, ulong[] items, GWLSet group, out ulong[] successList)
		{
			GWLItemType type = GWLItemType.Role;
			successList = null;

			using (var uow = _db.UnitOfWork)
            {
				// For each non-existing relationship, add it to the database
				// Fetch all member IDs existing in DB with given type and name in list
				var curIDs = uow._context.Set<GWLItem>()
					.Where(x => x.Type.Equals(type))
					.Where(x => x.RoleServerId.Equals(serverID))
					.Where(x => items.Contains(x.ItemId))
					.Select(x => x.Id)
					.ToArray();
				// Fetch all member IDs related to the given group BEFORE delete
				var relIDs = uow._context.Set<GWLItemSet>()
					.Where(x => x.ListPK.Equals(group.Id))
					.Where(x => curIDs.Contains(x.ItemPK))
					.Select(x => x.ItemPK)
					.ToArray();

				// Delete all existing IDs where type and name matches (above lines ensure no dupes)	
				if (curIDs.Count() > 0) {
					for (int i=0; i<curIDs.Count(); i++) {
						uow._context.Database.ExecuteSqlCommand(
							"DELETE FROM GWLItemSet WHERE ListPK = @p0 AND ItemPK = @p1;",
							group.Id,
							curIDs[i]);
						// System.Console.WriteLine("Remove Result {0}: {1}", i, resultRemove);
					}
					uow._context.SaveChanges();					
				}
				// Fetch all member IDs related to the given group AFTER delete
				var relIDsRemain = uow._context.Set<GWLItemSet>()
					.Where(x => x.ListPK.Equals(group.Id))
					.Where(x => curIDs.Contains(x.ItemPK))
					.Select(x => x.ItemPK)
					.ToArray();

				var deletedIDs = relIDs.Except(relIDsRemain);

				// Return list of all deleted relationships
				successList = uow._context.Set<GWLItem>()
					.Where(x => x.RoleServerId.Equals(serverID))
					.Where(x => deletedIDs.Contains(x.Id))
					.Select(x => x.ItemId)
					.ToArray();

				uow.Complete();
				if (successList.Count() > 0) return true;
				return false;
			}
		}

		public bool AddUnblockedToGroup(string[] names, UnblockedType type, GWLSet group, out string[] successList)
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
							$"INSERT INTO UnblockedCmdOrMdl ('{bcField}', 'Name', 'Type') VALUES (@p0,@p1,@p2);", 
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

		public bool RemoveUnblockedFromGroup(string[] names, UnblockedType type, GWLSet group, out string[] successList)
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

		#endregion Add/Remove

		#region Clear

		public bool ClearAll(GWLSet group)
		{
			return ClearMembers(group) && ClearUnblocked(group);
		}

		public bool ClearMembers(GWLSet group)
		{
			int result;
			using (var uow = _db.UnitOfWork)
			{
				string sql = "DELETE FROM GWLItemSet WHERE GWLItemSet.ListPK = @p0;";
				result = uow._context.Database.ExecuteSqlCommand(sql, group.Id);
				uow.Complete();
			}
			//System.Console.WriteLine("Query Result: ",result);
			return true;
		}

		public bool ClearMembers(GWLSet group, GWLItemType type)
		{
			int result;
			using (var uow = _db.UnitOfWork)
			{
				string sql = @"DELETE FROM GWLItemSet WHERE GWLItemSet.ListPK = @p0 AND GWLItemSet.ItemPK IN
					(SELECT Id FROM GWLItem WHERE GWLItem.Type = @p1);";
				result = uow._context.Database.ExecuteSqlCommand(sql, group.Id, type);
				uow.Complete();
			}
			return true;
		}

		public bool ClearUnblocked(GWLSet group)
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
		public bool ClearUnblocked(GWLSet group, UnblockedType type)
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

		public bool PurgeMember(GWLItemType type, ulong id)
		{
			using (var uow = _db.UnitOfWork)
			{
				uow._context.Set<GWLItem>().Remove( 
					uow._context.Set<GWLItem>()
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

		public bool IsMemberInGroup(ulong id, GWLItemType type, GWLSet group)
        {
            var result = true;

            using (var uow = _db.UnitOfWork)
            {
                var temp = uow._context.Set<GWLItem>()
					.Where( x => x.Type.Equals(type) )
					.Where( x => x.ItemId.Equals(id) )
					.Join(
						uow._context.Set<GWLItemSet>(), 
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
		public bool IsUnblockedInGroup(string name, UnblockedType type, GWLSet group)
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

		#region CheckUnblocked
		
		public bool CheckIfUnblockedFor(string ubName, UnblockedType ubType, ulong memID, GWLItemType memType, int page, out string[] lists, out int count)
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
					.Join(uow._context.Set<GWLSet>(),
						gub => gub, g => g.Id,
						(gub, g) => g
						)
					.Join(uow._context.Set<GWLItemSet>(),
						g => g.Id, gi => gi.ListPK,
						(g, gi) => new {
							g,
							gi.ItemPK
						})
					.Join(uow._context.Set<GWLItem>()
						.Where(x => x.Type.Equals(memType))
						.Where(x => x.ItemId.Equals(memID)),
						gi => gi.ItemPK, i => i.Id,
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
		public bool CheckIfUnblocked(string ubName, UnblockedType ubType, ulong memID, GWLItemType memType)
		{
			using (var uow = _db.UnitOfWork)
            {
				var result = uow._context.Set<UnblockedCmdOrMdl>()
					.Where(x => x.Type.Equals(ubType))
					.Where(x => x.Name.Equals(ubName))
					.Join(uow._context.Set<GlobalUnblockedSet>(), 
						ub => ub.Id, gub => gub.UnblockedPK, 
						(ub,gub) => gub.ListPK)
					.Join(uow._context.Set<GWLSet>()
						.Where(g => g.IsEnabled.Equals(true)),
						gubPK => gubPK, g => g.Id,
						(gubPK, g) => g.Id)
					.Join(uow._context.Set<GWLItemSet>(),
						gId => gId, gi => gi.ListPK,
						(gId, gi) => gi.ItemPK)
					.Join(uow._context.Set<GWLItem>()
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

		#region GetObject
		public bool GetGroupByName(string listName, out GWLSet group)
        {
            group = null;

            if (string.IsNullOrWhiteSpace(listName)) return false;

            using (var uow = _db.UnitOfWork)
            {
                group = uow._context.Set<GWLSet>()
					.Where(x => x.ListName.ToLowerInvariant().Equals(listName))
					.Include(x => x.GlobalUnblockedSets)
					.Include(x => x.GWLItemSets)
					.FirstOrDefault();

                if (group == null) { return false; }
                else { return true; }
            }
        }

		public bool GetMemberByIdType(ulong id, GWLItemType type, out GWLItem item)
        {
            item = null;

            using (var uow = _db.UnitOfWork)
            {
                // Retrieve the member item given name
                item = uow._context.Set<GWLItem>()
                    .Where( x => x.Type.Equals(type) )
                    .Where( x => x.ItemId.Equals(id) )
                    .FirstOrDefault();

                if (item == null) { return false; }
                else { return true; }
            }
        }

		public bool GetUnblockedByNameType(string name, UnblockedType type, out UnblockedCmdOrMdl item)
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

		#endregion GetObject

		#region GetGroupNames

		 public bool GetGroupNames(GWLType type, int page, out string[] names, out int count)
        {
			names = null;
            using (var uow = _db.UnitOfWork)
            {
				var allnames = uow._context.Set<GWLSet>()
					.Where(g => g.Type.Equals(type));
				
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

		/// <summary>
		/// Output a list of GWL with GWLType.Member for which there is 
		/// at least one member with the given type and id
		/// </summary>
		public bool GetGroupNamesByMemberType(ulong id, GWLItemType type, int page, out string[] names, out int count)
        {
            names = null;
            using (var uow = _db.UnitOfWork)
            {
				var allnames = uow._context.Set<GWLItem>()
				.Where(i => i.Type.Equals(type))
				.Where(i => i.RoleServerId.Equals(0))
				.Where(i => i.ItemId.Equals(id))
				.Join(uow._context.Set<GWLItemSet>(),
					i => i.Id, gi => gi.ItemPK,
					(i, gi) => gi.ListPK)
				.Join(uow._context.Set<GWLSet>()
					.Where(g=>g.Type.Equals(GWLType.Member)),
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

		/// <summary>
		/// Output a list of GWL with GWLType.Role for which there is at least one RoleServerID-ItemID pair 
		/// that matches the given list of roles (presumably from a user/channel/server)
		/// </summary>
		public bool GetGroupNamesByMemberRoles(ulong serverID, ulong[] ids, int page, out string[] names, out int count)
		{
            names = null;
            using (var uow = _db.UnitOfWork)
            {
				var allnames = uow._context.Set<GWLItem>()
				.Where(i => i.Type.Equals(GWLItemType.Role))
				.Where(i => i.RoleServerId.Equals(serverID))
				.Where(i => ids.Contains(i.ItemId))
				.Join(uow._context.Set<GWLItemSet>(),
					i => i.Id, gi => gi.ItemPK,
					(i, gi) => gi.ListPK)
				.Join(uow._context.Set<GWLSet>()
					.Where(g=>g.Type.Equals(GWLType.Role)),
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

		/// <summary>
		/// Iterate over a dictionary of ServerID-RoleIDs and combine the results of each iteration
		/// </summary>
		public bool GetGroupNamesByMemberRoles(Dictionary<ulong,ulong[]> servRoles, int page, out string[] names, out int count)
		{
			names = null;
			count = 0;
			IEnumerable<string> tempRoles = null;
			for (int k=0; k<servRoles.Keys.Count(); k++) {
				ulong sID = servRoles.Keys.ElementAt(k);
				if (GetGroupNamesByMemberRoles(sID, servRoles.GetValueOrDefault(sID), page, out string[] temp, out int countT)) {
					if (tempRoles == null) {
						tempRoles = temp;
					} else {
						tempRoles = tempRoles.Union(temp);
					}
				}
			}
			if (tempRoles != null) count = tempRoles.Count();
			if (count <= 0) return false;
			names = tempRoles.ToArray();
			return true;
		}

		/// <summary>
		/// Output a list of GWL with GWLType.Role for which there is at least one 
		/// RoleServerID-ItemID pair that matches the given server and role ID
		/// </summary>
		public bool GetGroupNamesByServerRole(ulong serverID, ulong roleID, int page, out string[] names, out int count)
		{
            names = null;
            using (var uow = _db.UnitOfWork)
            {
				var allnames = uow._context.Set<GWLItem>()
				.Where(i => i.Type.Equals(GWLItemType.Role))
				.Where(i => i.RoleServerId.Equals(serverID))
				.Where(i => i.ItemId.Equals(roleID))
				.Join(uow._context.Set<GWLItemSet>(),
					i => i.Id, gi => gi.ItemPK,
					(i, gi) => gi.ListPK)
				.Join(uow._context.Set<GWLSet>()
					.Where(g=>g.Type.Equals(GWLType.Role)),
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

		/// <summary>
		/// Output a list of GWL with GWLType.Role for which there is at least one 
		/// RoleServerID that matches serverID
		/// </summary>
		public bool GetGroupNamesByServer(ulong serverID, int page, out string[] names, out int count)
		{
            names = null;
            using (var uow = _db.UnitOfWork)
            {
				var allnames = uow._context.Set<GWLItem>()
				.Where(i => i.Type.Equals(GWLItemType.Role))
				.Where(i => i.RoleServerId.Equals(serverID))
				.Join(uow._context.Set<GWLItemSet>(),
					i => i.Id, gi => gi.ItemPK,
					(i, gi) => gi.ListPK)
				.Join(uow._context.Set<GWLSet>()
					.Where(g=>g.Type.Equals(GWLType.Role)),
					listPK => listPK, g => g.Id,
					(listPK, g) => g)
				.GroupBy(g=>g.ListName).Select(y=>y.First()); // since RoleServerID is non-unique, remove dupes https://stackoverflow.com/a/4095023
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

		public bool GetGroupNamesByUnblocked(string name, UnblockedType type, GWLType typeG, int page, out string[] names, out int count)
		{
			names = null;
			count = 0;

			// Get the item
			UnblockedCmdOrMdl item;
			bool exists = GetUnblockedByNameType(name, type, out item);

			if (!exists) return false;

			using (var uow = _db.UnitOfWork)
            {
                // Retrieve a list of set names linked to GlobalUnblockedSets.ListPK
                var allnames = uow._context.Set<GWLSet>()
						.Where(g=>g.Type.Equals(typeG))
					.Join(
						uow._context.Set<GlobalUnblockedSet>()
							.Where(u => u.UnblockedPK.Equals(item.Id)), 
						g => g.Id, gu => gu.ListPK,
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

		#endregion GetGroupNames

		#region Get Id/Name Lists

		public bool GetGroupMembers(GWLSet group, GWLItemType type, int page, out ulong[] results, out int count)
        {
            results = null;
            using (var uow = _db.UnitOfWork)
            {
                var anon = group.GWLItemSets
                	.Join(uow._context.Set<GWLItem>()
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

		public bool GetGroupUnblockedNames(GWLSet group, UnblockedType type, int page, out string[] names, out int count)
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

		public bool GetUnblockedNamesForMember(UnblockedType type, ulong id, GWLItemType memType, int page, out string[] names, out int count)
		{
			names= null;
			using (var uow = _db.UnitOfWork)
            {
                var anon = uow._context.Set<GWLItem>()
					.Where(x => x.ItemId.Equals(id))
					.Where(x => x.Type.Equals(memType))
					.Join(uow._context.Set<GWLItemSet>(),
						i => i.Id, gi => gi.ItemPK,
						(i, gi) => gi.ListPK)
					.Join(uow._context.Set<GWLSet>()
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

		#endregion Get Id/Name Lists

		#region Resolve ulong IDs

        public string[] GetNameOrMentionFromId(GWLItemType type, ulong[] ids)
        {
            string[] str = new string[ids.Length];

            switch (type) {
                case GWLItemType.User:
                    for (var i = 0; i < ids.Length; i++) {
					  str[i] = MentionUtils.MentionUser(ids[i]) + "\n\t" + ids[i].ToString();
                    }
                    break;

                case GWLItemType.Channel:
                    for (var i = 0; i < ids.Length; i++) {
					  str[i] = MentionUtils.MentionChannel(ids[i]) + "\n\t" + ids[i].ToString();
                    }
                    break;

                case GWLItemType.Server:
                    for (var i = 0; i < ids.Length; i++) {
						var guild = _client.Guilds.FirstOrDefault(g => g.Id.Equals(ids[i]));
                    	string name = (guild != null) ? guild.Name : "Null";
						str[i] = $"[{name}](https://discordapp.com/channels/{ids[i]}/ '{ids[i]}')\n\t{ids[i]}";
                    }
                    break;

				case GWLItemType.Role:
                    for (var i = 0; i < ids.Length; i++) {
					  str[i] = MentionUtils.MentionRole(ids[i]) + "\n\t" + ids[i].ToString();
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
        public string GetNameOrMentionFromId(GWLItemType type, ulong id)
        {
            string str = "";

            switch (type) {
                case GWLItemType.User:
					str = MentionUtils.MentionUser(id) + " " + id.ToString();
                    break;

                case GWLItemType.Channel:
					str = MentionUtils.MentionChannel(id) + " " + id.ToString();
                    break;

                case GWLItemType.Server:
					var guild = _client.Guilds.FirstOrDefault(g => g.Id.Equals(id));
                    str = (guild != null) ? $"[{guild.Name}](https://discordapp.com/channels/{id}/ '{id}') {id}" : id.ToString();
					break;

				case GWLItemType.Role:
					str = MentionUtils.MentionRole(id) + " " + id.ToString();
                    break;
				
                default:
                    str = id.ToString();
                    break;
            }

            return str;
        }

		#endregion Resolve ulong IDs

		#region Retrieve Server and Role IDs
			public Dictionary<ulong,ulong[]> GetRoleIDs(GWLItemType type, ulong id, ulong ctxSrvrId)
			{
				Dictionary<ulong,ulong[]> result = null;
				switch(type) {
					case GWLItemType.Role:
						// Output serverID: ctxSrvrId, RoleID: id
						result = new Dictionary<ulong, ulong[]>();
						result.Add(ctxSrvrId, new ulong[]{id});
						break;

					case GWLItemType.User:
						// Find all servers shared with the user
						// Then find all roles for the user in each server
						SocketUser user = _client.GetUser(id);
						if (user != null) {
							result = _client.Guilds
								.Where(g => g.GetUser(id) != null)
								.ToDictionary(s => s.Id, rs => rs.Roles
									.Where(r => r.Members.Contains(r.Guild.GetUser(id)))
									.Select(r => r.Id).ToArray());
						}
						break;

					case GWLItemType.Channel:
						// Find the channel's server
						// Then find all roles in the channel
						ulong sID = GetServerID(id);
						if (sID > 0) {
							result = new Dictionary<ulong, ulong[]>();
							result.Add(sID, new ulong[]{id});
						}
						break;

					case GWLItemType.Server:
						// Find all roles in this server
						SocketGuild srvr = _client.GetGuild(id);
						if (srvr != null) {
							ulong[] roleIDs = srvr.Roles.Select(r => r.Id).ToArray();
							if (roleIDs.Length > 0) {
								result = new Dictionary<ulong, ulong[]>();
								result.Add(id, roleIDs);
							}
						}
						break;

					default:
						break;
				}
				return result;
			}

			public ulong GetServerID(ulong cID)
			{
				ulong sID = 0;
				SocketGuildChannel chnl = _client.GetChannel(cID) as SocketGuildChannel;
				if (chnl != null) {
					sID = chnl.Guild.Id;
				}
				return sID;
			}
		#endregion Retrieve Server and Role IDs
	}
}
