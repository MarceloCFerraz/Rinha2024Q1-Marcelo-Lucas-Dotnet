using System.Data.Common;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // var dbHostName = Environment.GetEnvironmentVariable("DB_HOSTNAME") ?? "localhost"; // uncomment if testing with `dotnet run`. Remember to comment the line below
        var dbHostName = Environment.GetEnvironmentVariable("DB_HOSTNAME") ?? "db";
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
            MinPoolSize = 20,
            MaxPoolSize = 20,
        };

        await using var dataSource = NpgsqlDataSource.Create(conStringBuilder.ConnectionString);

        builder.Services.AddSingleton(provider => dataSource);

        var app = builder.Build();

        // API Port Configuration
        var port = Environment.GetEnvironmentVariable("API_PORT") ?? "8081";  // Default to 8081
        app.Urls.Add($"http://+:{port}");

        app.MapPost("/clientes/{id}/transacoes", async ([FromRoute] int id, [FromBody] Transaction transaction, NpgsqlDataSource dataSource) =>
        {
            if (transaction.IsInvalid()) return Results.UnprocessableEntity();

            // saving processing time :)
            if (Client.IsInvalid(id))
                return Results.NotFound();

            var operation = transaction.tipo == 'c' ? "credit" : "debit";
            using (var connection = await dataSource.OpenConnectionAsync())
            {
                await using var update = new NpgsqlCommand(
                    $"SELECT success, client_limit, new_balance FROM {operation}($1, $2, $3)",
                    connection
                );
                update.Parameters.AddWithValue(id);
                update.Parameters.AddWithValue((int)transaction.valor);
                update.Parameters.AddWithValue(transaction.descricao);

                try
                {
                    using (var reader = await update.ExecuteReaderAsync())
                    {
                        await reader.ReadAsync();

                        if (reader.GetBoolean(0)) // if (success)
                            return Results.Ok(new NewTransactionResponse(
                                reader.GetInt32(ordinal: 1),
                                reader.GetInt32(2))
                            );
                    }
                }
                catch (DbException ex)
                {
                    Console.WriteLine($"Something went wrong and the operation was cancelled:\n{ex}");
                }

                // if the operation fails, the database will rollback and the api will return status code 422
                return Results.UnprocessableEntity();
            }
        });

        app.MapGet("/clientes/{id}/extrato", async ([FromRoute] int id, NpgsqlDataSource dataSource) =>
        {
            // saving processing time :)
            if (Client.IsInvalid(id))
                return Results.NotFound();

            var response = new ExtratoResponse();
            var details = new ExtratoDetails();
            try
            {
                using (var connection = await dataSource.OpenConnectionAsync()) // maybe close connection after each operation?
                {
                    await using var search = new NpgsqlCommand(
                        "SELECT customer_balance, customer_limit, report_date, last_transactions FROM get_client_data($1);",
                        connection
                    );
                    search.Parameters.AddWithValue(id);


                    using (var reader = await search.ExecuteReaderAsync())
                    {
                        await reader.ReadAsync();
                        details.total = reader.GetInt32(0);
                        details.limite = reader.GetInt32(1);
                        details.data_extrato = reader.GetDateTime(2);
                        // ultimas_transacoes arrives here as string, so need to deserialize it
                        response.ultimas_transacoes = JsonSerializer
                            .Deserialize<List<TransactionHistory>>(reader.GetString(3))
                            ?? [];
                        // ultimas_transacoes = JsonSerializer.Deserialize<List<Transaction>>(reader.GetString(3)) ?? new List<Transaction>();
                    }
                }
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: $"Database operation failed\n{ex}", statusCode: 500);
            }
            response.saldo = details;

            // Console.WriteLine(JsonSerializer.Serialize(response));

            return Results.Ok(response);
        });

        app.Run();
    }
}