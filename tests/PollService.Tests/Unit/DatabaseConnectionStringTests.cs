using Npgsql;
using PollBuilder.Contracts.Infrastructure;

namespace PollService.Tests.Unit
{
    /// <summary>
    /// Neon hands out a libpq-style URI that Npgsql cannot consume directly. These tests pin the
    /// conversion, because getting it wrong means the deployed service crashes on startup with a
    /// connection string that looks perfectly correct in the Render dashboard.
    /// </summary>
    public class DatabaseConnectionStringTests
    {
        /// <summary>The exact shape Neon shows on its dashboard, including channel_binding.</summary>
        private const string NeonUri =
            "postgresql://neondb_owner:npg_SeCrEt123@ep-misty-boat-a1b2c3-pooler.c-4.us-east-2.aws.neon.tech/neondb"
            + "?sslmode=require&channel_binding=require";

        [Fact]
        public void A_neon_uri_becomes_a_connection_string_npgsql_can_parse()
        {
            var result = DatabaseConnectionString.Normalise(NeonUri);

            var builder = new NpgsqlConnectionStringBuilder(result);
            Assert.Equal("ep-misty-boat-a1b2c3-pooler.c-4.us-east-2.aws.neon.tech", builder.Host);
            Assert.Equal(5432, builder.Port);
            Assert.Equal("neondb", builder.Database);
            Assert.Equal("neondb_owner", builder.Username);
            Assert.Equal("npg_SeCrEt123", builder.Password);
            Assert.Equal(SslMode.Require, builder.SslMode);
        }

        [Fact]
        public void Query_parameters_npgsql_does_not_know_are_dropped_rather_than_throwing()
        {
            // channel_binding is a libpq option with no Npgsql equivalent. Before this was handled,
            // pasting Neon's default URI threw ArgumentException during startup.
            var result = DatabaseConnectionString.Normalise(NeonUri);

            Assert.DoesNotContain("channel_binding", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void An_explicit_port_is_preserved()
        {
            var result = DatabaseConnectionString.Normalise(
                "postgres://user:pass@db.example.com:6543/appdb?sslmode=require");

            Assert.Equal(6543, new NpgsqlConnectionStringBuilder(result).Port);
        }

        [Fact]
        public void A_percent_encoded_password_is_decoded()
        {
            var result = DatabaseConnectionString.Normalise(
                "postgres://user:p%40ss%3Aword@db.example.com/appdb?sslmode=require");

            Assert.Equal("p@ss:word", new NpgsqlConnectionStringBuilder(result).Password);
        }

        [Fact]
        public void A_key_value_connection_string_is_passed_through_untouched()
        {
            const string keyValue = "Host=localhost;Port=5432;Database=pollbuilder;Username=postgres;Password=postgres";

            Assert.Equal(keyValue, DatabaseConnectionString.Normalise(keyValue));
        }

        [Fact]
        public void Describe_names_the_database_without_leaking_the_password()
        {
            var description = DatabaseConnectionString.Describe(DatabaseConnectionString.Normalise(NeonUri));

            Assert.Contains("neondb", description, StringComparison.Ordinal);
            Assert.DoesNotContain("npg_SeCrEt123", description, StringComparison.Ordinal);
        }
    }
}
