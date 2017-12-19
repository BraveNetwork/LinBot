using NadekoBot.Core.Services;

namespace NadekoBot.Modules.Example.Services
{
    public class ExampleService : INService
    {
        // Declare your private variables here, such as a service for accessing a different module
        //private readonly DbService _db;  // For accessing a database context


        // Set up your constructor
        //public ExampleService(DbService db)
        public ExampleService() // Add the source(s) for your private variable(s) as arguments for the constructor
        {
            // Here is where you do any initializations, such as setting your private variables
            //_db = db;
        }
    }
}
