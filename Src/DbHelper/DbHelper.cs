using System.Collections.Concurrent;
using System.Data;
using Npgsql;
using Microsoft.VisualStudio.Threading;

namespace Helper
{
    public class DbHelper
    {
        private int MinPoolSize { get; set; } = 20;
        private int MaxPoolSize { get; set; } = 20;
        private string ConnectionString { get; set; }
        public NpgsqlDataSource DataSource { get; set; }

        public DbHelper()
        {
            // init ConnectionString
            this.ConnectionString = InitConnectionString();

            // init Connections
            this.DataSource = NpgsqlDataSource.Create(this.ConnectionString);

            // for (int i = 0; i < this.MaxPoolSize; i++)
            //     this.PGConnections.Enqueue(CreateNewPGConnection());
        }

        private string InitConnectionString()
        {
            // init ConnectionString
            // var dbHostName = Environment.GetEnvironmentVariable("DB_HOSTNAME") ?? "localhost"; // uncomment me testing with `dotnet run`
            var dbHostName = Environment.GetEnvironmentVariable("DB_HOSTNAME") ?? "db"; // comment me if testing with `dotnet run`. Why? Don't know, it just doesn't work
            var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "rinha";
            var dbPort = int.Parse(Environment.GetEnvironmentVariable("DB_PORT") ?? "5432");
            var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "admin";
            var dbPass = Environment.GetEnvironmentVariable("DB_PASS") ?? "mystrongpassword";

            var conStringBuilder = new NpgsqlConnectionStringBuilder
            {
                Host = dbHostName,
                Database = dbName,
                Port = dbPort,
                Username = dbUser,
                Password = dbPass,
                Pooling = true,
                MinPoolSize = this.MinPoolSize,
                MaxPoolSize = this.MaxPoolSize,
            };

            return conStringBuilder.ConnectionString;
        }

        // private NpgsqlConnection CreateNewPGConnection()
        // {
        //     return new NpgsqlConnection(this.ConnectionString);
        // }

        // public async Task<NpgsqlConnection> DequeuePGConnectionAsync()
        // {
        //     return await this.PGConnections.DequeueAsync();
        // }

        // public bool EnqueuePGConnection(NpgsqlConnection conn)
        // {
        //     return this.PGConnections.TryEnqueue(conn);
        // }
    }
}