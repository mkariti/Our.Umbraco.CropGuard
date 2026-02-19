using Microsoft.Data.Sqlite;
using Umbraco.Cms.Persistence.Sqlite;

// Initialise the native SQLite library before Umbraco's early DB health-check runs.
SQLitePCL.Batteries_V2.Init();

// Resolve the SQLite DB path relative to the content root so the TestSite works on any machine.
// Microsoft.Data.Sqlite with Cache=Shared bypasses |DataDirectory| substitution on macOS/Linux,
// so we inject an absolute path via in-memory config instead.
var dataDirectory = Path.Combine(Directory.GetCurrentDirectory(), "umbraco", "Data");
var dbFilePath = Path.Combine(dataDirectory, "Umbraco.sqlite.db");

// Pre-create the database file if it doesn't exist.
// Cache=Shared cannot create a new file from scratch on macOS/Linux (SQLite Error 14).
if (!File.Exists(dbFilePath) || new FileInfo(dbFilePath).Length == 0)
{
    Directory.CreateDirectory(dataDirectory);
    using var initConn = new SqliteConnection($"Data Source={dbFilePath}");
    initConn.Open();
    using var cmd = initConn.CreateCommand();
    cmd.CommandText = "PRAGMA journal_mode=WAL;";
    cmd.ExecuteScalar();
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Override the connection string with the resolved absolute path.
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ConnectionStrings:umbracoDbDSN"] =
        $"Data Source={dbFilePath};Cache=Shared;Foreign Keys=True;Pooling=True",
});

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers()
    .AddUmbracoSqliteSupport()
    .Build();

WebApplication app = builder.Build();

await app.BootUmbracoAsync();

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
