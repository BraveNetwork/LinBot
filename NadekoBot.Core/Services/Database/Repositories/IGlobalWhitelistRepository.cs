using Microsoft.EntityFrameworkCore;
using NadekoBot.Core.Services.Database.Models;
using System;
using System.Linq;

namespace NadekoBot.Core.Services.Database.Repositories
{
    public interface IGlobalWhitelistRepository : IRepository<GlobalWhitelistSet>
    {
        GlobalWhitelistSet GetByName(string name);
        GlobalWhitelistSet[] GetWhitelistGroups(int page);
        GlobalWhitelistSet[] GetWhitelistGroupsByMember(int itemPK, int page);
    }
}
