using System.Globalization;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace PollBuilder.Contracts.Infrastructure
{
    /// <summary>
    /// Resolves the PostgreSQL connection string. Hosted providers (Neon, Render, Railway) hand out a
    /// URI like <c>postgresql://user:pass@host/db?sslmode=require</c>, which Npgsql does not accept
    /// directly, so URIs are converted to the key/value form here.
    /// </summary>
    public static class DatabaseConnectionString
    {
        /// <summary>
        /// Reads <c>ConnectionStrings:Postgres</c>, falling back to the <c>DATABASE_URL</c> environment
        /// variable that most PaaS providers inject automatically.
        /// </summary>
        public static string? Resolve(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var raw = configuration.GetConnectionString("Postgres");
            if (string.IsNullOrWhiteSpace(raw))
            {
                raw = configuration["DATABASE_URL"];
            }

            return string.IsNullOrWhiteSpace(raw) ? null : Normalise(raw);
        }

        /// <summary>Converts a postgres:// URI to Npgsql key/value form; other values pass through.</summary>
        public static string Normalise(string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
                && !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }

            var uri = new Uri(value);
            var userInfo = uri.UserInfo.Split(':', 2);

            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.IsDefaultPort ? 5432 : uri.Port,
                Database = uri.AbsolutePath.TrimStart('/'),
                Username = Uri.UnescapeDataString(userInfo[0]),
                Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
                SslMode = SslMode.Require
            };

            // Preserve anything the provider tacked on, e.g. channel_binding or a pooler endpoint id.
            foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                var key = Uri.UnescapeDataString(parts[0]);
                var setting = Uri.UnescapeDataString(parts[1]);

                if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase))
                {
                    builder.SslMode = Enum.Parse<SslMode>(setting, ignoreCase: true);
                }
                else
                {
                    builder[key] = setting;
                }
            }

            return builder.ToString();
        }

        /// <summary>Host and database only — safe to write to logs when diagnosing a bad deployment.</summary>
        public static string Describe(string connectionString)
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            return string.Format(CultureInfo.InvariantCulture, "{0}/{1}", builder.Host, builder.Database);
        }
    }
}
