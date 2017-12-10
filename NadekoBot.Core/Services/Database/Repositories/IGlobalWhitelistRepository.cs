using Microsoft.EntityFrameworkCore;
using NadekoBot.Core.Services.Database.Models;
using System;
using System.Linq;

namespace NadekoBot.Core.Services.Database.Repositories
{
    public interface IGlobalWhitelistRepository : IRepository<GlobalWhitelistSet>
    {
        GlobalWhitelistSet GetByName(string v, Func<DbSet<GlobalWhitelistSet>, IQueryable<GlobalWhitelistSet>> func = null);
        GlobalWhitelistSet[] GetWhitelistGroups(int page);
        GlobalWhitelistSet[] GetWhitelistGroupsByMember(int itemPK, int page);
    }
}
