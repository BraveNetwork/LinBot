using NadekoBot.Core.Services.Database.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System;

namespace NadekoBot.Core.Services.Database.Repositories.Impl
{
    public class GlobalWhitelistRepository : Repository<GlobalWhitelistSet>, IGlobalWhitelistRepository
    {
        public GlobalWhitelistRepository(DbContext context) : base(context)
        {
        }

        public GlobalWhitelistSet GetByName(string name)
        {
            return _set
                .Where(x => x.ListName.ToLowerInvariant().Equals(name))
                .Include(x => x.GlobalWhitelistItemSets)
                .Include(x => x.GlobalUnblockedSets)
                .FirstOrDefault();
        }

        public GlobalWhitelistSet[] GetWhitelistGroupsByMember(int itemPK, int page)
        {
            return _set
                .Include(x => x.GlobalWhitelistItemSets)
                .Where(x => x.GlobalWhitelistItemSets.Any(y => y.ItemPK.Equals(itemPK)))
                .OrderBy(x => x.ListName)
                .Skip( (page-1)  * 9)
                .Take(9)
                .ToArray();
        }
    }
}
