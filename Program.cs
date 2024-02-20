using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var app = builder.Build();

        // API Port Configuration
        var port = Environment.GetEnvironmentVariable("API_PORT") ?? "8081";  // Default to 8081

        app.Urls.Add($"http://+:{port}"); // Listen on the specified port

        // Add services to the container (This is minimal; add only what's essential)
        var dbHostName = Environment.GetEnvironmentVariable("DB_HOSTNAME") ?? "db";
        var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "rinha";
        var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "admin";
        var dbPass = Environment.GetEnvironmentVariable("DB_PASS") ?? "123";

        string connectionString = $"Host={dbHostName};Database={dbName};Username={dbUser};Password={dbPass}";

        await using var dataSource = NpgsqlDataSource.Create(connectionString);

        builder.Services.AddScoped(provider => dataSource);
        builder.Services.AddScoped(provider =>
            new DatabaseHelper(connectionString)
        );
        // builder.Services.AddDbContext<MyDatabaseContext>();

        app.MapPost("/clientes/{id}/transacoes", async ([FromRoute] int id, [FromBody] Transaction transaction, NpgsqlDataSource db) =>
        {
            await using var connection = await db.OpenConnectionAsync();
            // 1. Input Validation
            if (transaction.tipo != TransactionType.Credit.ToString() && transaction.tipo != TransactionType.Debit.ToString())
            {
                Console.WriteLine("Invalid Transaction, but rules don't say it should be rejected");
                // return Results.BadRequest("Invalid transaction type");
            }

            // 2. Fetch Client (Handling non-existence)
            // TODO: fetch for client with id
            if (client == null)
            {
                return Results.NotFound();
            }

            // 3. Balance Logic
            int newBalance = client.Balance;
            if (transaction.tipo == TransactionType.Credit.ToString())
            {
                newBalance += transaction.valor;
            }
            else if (transaction.tipo == TransactionType.Debit.ToString())
            {
                newBalance -= transaction.valor;
                if (newBalance < -client.Limit)
                {
                    return Results.StatusCode(422); // Unprocessable due to limit
                }
            }

            // 4. Update Database (Optimistic Concurrency Recommended)
            client.Balance = newBalance;
            // TODO: save new balance intto the database

            return Results.Ok(new { limite = client.Limit, saldo = client.Balance });
        });

        app.MapGet("/clientes/{id}/extrato", async ([FromRoute] int id, NpgsqlDataSource db) =>
        {
            await using var connection = await db.OpenConnectionAsync();

            // TODO: fetch top 10 transacoes from transacao where id_cliente == id
            // ... Implement fetching the client and last 10 transactions
            // ... Format the response object as required
            return Results.Ok(/* formatted extracted data */);
        });

        app.Run();
    }
}