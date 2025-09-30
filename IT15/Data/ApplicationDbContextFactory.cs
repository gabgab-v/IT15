using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;
using System;

namespace IT15.Data
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            // Try to read DATABASE_URL from environment
            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

            if (string.IsNullOrEmpty(databaseUrl))
            {
                // Fallback for local dev (so migrations still work even without Render’s env var)
                return CreateLocalDbContext();
            }

            // ✅ Normalize scheme (Render gives postgresql://, Npgsql expects postgres://)
            databaseUrl = databaseUrl.Replace("postgresql://", "postgres://");

            var databaseUri = new Uri(databaseUrl);
            var userInfo = databaseUri.UserInfo.Split(':', 2);

            var connectionString = new NpgsqlConnectionStringBuilder
            {
                Host = databaseUri.Host,
                Port = databaseUri.Port > 0 ? databaseUri.Port : 5432,
                Username = userInfo[0],
                Password = userInfo.Length > 1 ? userInfo[1] : "",
                Database = databaseUri.AbsolutePath.TrimStart('/'),
                SslMode = SslMode.Require,
                TrustServerCertificate = true
            }.ToString();

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new ApplicationDbContext(optionsBuilder.Options);
        }

        private ApplicationDbContext CreateLocalDbContext()
        {
            var localConnectionString =
                "Host=localhost;Port=5432;Username=postgres;Password=yourpassword;Database=it15_db;";

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseNpgsql(localConnectionString);

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
