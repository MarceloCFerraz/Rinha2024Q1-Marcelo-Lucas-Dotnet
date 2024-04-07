using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Dapper;
using Helper;
using Npgsql;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<DbHelper>();

var app = builder.Build();

// API Port Configuration
var port = Environment.GetEnvironmentVariable("API_PORT") ?? "8081";  // Default to 8081
app.Urls.Add($"http://+:{port}");

app.MapPost("/clientes/{id}/transacoes", async ([FromRoute] int id, [FromBody] Transaction transaction, DbHelper dbHelper) =>
{
    // Console.WriteLine(transaction.ToString());
    if (transaction.IsInvalid()) return Results.UnprocessableEntity();

    // saving processing time :)
    if (Client.IsInvalid(id))
        return Results.NotFound();

    string function = transaction.tipo == 'c' ? "credit" : "debit";

    try
    {
        using (var connection = await dbHelper.DataSource.OpenConnectionAsync())
        {
            var result = await connection.QueryFirstOrDefaultAsync<PostTransactionResponse>(
                $"SELECT success, client_limit, new_balance FROM {function}(@id, @valor, @descricao);",
                new { id, valor = (int)transaction.valor, transaction.descricao }
            );

            // if (!dbHelper.EnqueuePGConnection(connection))
            //     Console.WriteLine("Queue is already completed");

            if (result != null)
                if (result.success)
                    return Results.Ok(new NewTransactionResponse(
                        result.client_limit,
                        result.new_balance)
                    );

            // if the operation fails, the database will rollback and the api will return status code 422
            return Results.UnprocessableEntity();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database operation failed\n{ex}");
        return Results.Problem();
    }
});

app.MapGet("/clientes/{id}/extrato", async ([FromRoute] int id, DbHelper dbHelper) =>
{
    // saving processing time :)
    if (Client.IsInvalid(id))
        return Results.NotFound();

    try
    {
        using (var connection = await dbHelper.DataSource.OpenConnectionAsync())
        {
            var result = await connection.QueryFirstOrDefaultAsync<GetReportResponse>(
                "SELECT customer_balance, customer_limit, report_date, last_transactions FROM get_client_data(@id);",
                new { id }
            );

            // if (!dbHelper.EnqueuePGConnection(connection))
            //     Console.WriteLine("Queue is already completed");

            if (result != null)
                return Results.Ok(new ExtratoResponse(
                    result.customer_balance,
                    result.report_date,
                    result.customer_limit,
                    JsonSerializer.Deserialize<List<TransactionHistory>>(result.last_transactions) ?? []
                ));

            throw new NpgsqlException($"Result is null for extrato {id}.");
            // if this function is returning invalid transactions (an empty list), it's probably because it's failing in the deserialize function for some reason
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database operation failed\n{ex}");
        return Results.Problem();
    }
});

app.Run();
