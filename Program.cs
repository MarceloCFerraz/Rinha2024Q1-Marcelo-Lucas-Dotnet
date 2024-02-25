using System.Data.Common;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;

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

        var validClientIds = new List<int?>() { 1, 2, 3, 4, 5 };

        builder.Services.AddSingleton(validClientIds);

        var app = builder.Build();

        // API Port Configuration
        var port = Environment.GetEnvironmentVariable("API_PORT") ?? "8081";  // Default to 8081
        app.Urls.Add($"http://+:{port}");

        app.MapPost("/clientes/{id}/transacoes", async ([FromRoute] int id, [FromBody] Transaction transaction, NpgsqlDataSource dataSource) =>
        {
            if (
                (transaction.tipo != 'c'
                && transaction.tipo != 'd') // reject transactions that are not credit or debit
                || string.IsNullOrEmpty(transaction.descricao) // reject null or empty descriptions
                || transaction.descricao.Length > 10 // reject descriptions with more than 10 chars
                || transaction.valor % 1 != 0 // reject double values, but *.0 values are ok
                || transaction.valor <= 0 // reject negative values and transactions with value 0
            ) return Results.UnprocessableEntity();

            // saving processing time :)
            if (!validClientIds.Contains(id))
                return Results.NotFound();

            // this would be more appropriate in a real world scenario
            // if (!reader.HasRows)
            //     return Results.NotFound();

            var operation = transaction.tipo == 'c' ? "credit" : "debit";
            using (var connection = await dataSource.OpenConnectionAsync())
            {
                await using var update = new NpgsqlCommand($"SELECT success, client_limit, new_balance FROM {operation}($1, $2, $3)", connection);
                update.Parameters.AddWithValue(id);
                update.Parameters.AddWithValue((int)transaction.valor);
                update.Parameters.AddWithValue(transaction.descricao);

                try
                {
                    using (var reader = await update.ExecuteReaderAsync())
                    {
                        await reader.ReadAsync();

                        bool success = reader.GetBoolean(0);

                        if (success)
                            return Results.Ok(new { limite = reader.GetInt32(1), saldo = reader.GetInt32(2) });
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
            int saldo, limite;
            DateTime data_extrato;
            List<Transaction> ultimas_transacoes;
            try
            {
                using (var connection = await dataSource.OpenConnectionAsync()) // maybe close connection after each operation?
                {
                    // saving processing time :)
                    if (!validClientIds.Contains(id))
                        return Results.NotFound();

                    // this would be more appropriate in a real world scenario
                    // if (!reader.HasRows)
                    //     return Results.NotFound();

                    /*
                        customer_balance int,
                        customer_limit int,
                        report_date timestamptz,
                        last_transactions json
                    */
                    await using var search = new NpgsqlCommand(
                        "SELECT customer_balance, customer_limit, report_date, last_transactions FROM get_client_data($1);",
                        connection
                    );
                    search.Parameters.AddWithValue(id);


                    using (var reader = await search.ExecuteReaderAsync())
                    {
                        await reader.ReadAsync();
                        saldo = reader.GetInt32(0);
                        limite = reader.GetInt32(1);
                        data_extrato = reader.GetDateTime(2);
                        Console.WriteLine(reader.GetFieldType(3));
                        ultimas_transacoes = JsonSerializer.Deserialize<List<Transaction>>(reader.GetString(3)) ?? new List<Transaction>();
                    }
                }
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: $"Database operation failed\n{ex}", statusCode: 500);
            }

            return Results.Ok(new
            {
                saldo = new
                {
                    total = saldo,
                    data_extrato = data_extrato,
                    limite = limite
                },
                ultimas_transacoes = ultimas_transacoes
            });

            //         await using var lastTransactions = new NpgsqlCommand(
            //             @"SELECT valor, tipo, descricao, realizada_em
            //             FROM transacao
            //             WHERE id_cliente = @id
            //             ORDER BY realizada_em DESC LIMIT 10;",
            //             connection
            //         );
            //         lastTransactions.Parameters.Add("@id", NpgsqlDbType.Integer).Value = id;

            //         using (var extratoReader = await lastTransactions.ExecuteReaderAsync())
            //         {
            //             if (!extratoReader.HasRows)
            //             {
            //                 return Results.Ok(new
            //                 {
            //                     saldo = new
            //                     {
            //                         total = client.saldo,
            //                         data_extrato = DateTime.UtcNow,
            //                         limite = client.limite
            //                     },
            //                     ultimas_transacoes = ultimas_transacoes
            //                 });
            //             };
            //             while (await extratoReader.ReadAsync())
            //             {
            //                 ultimas_transacoes.Add(new
            //                 {
            //                     valor = extratoReader.GetInt32(0),
            //                     tipo = extratoReader.GetChar(1),
            //                     descricao = extratoReader.GetString(2),
            //                     realizada_em = extratoReader.GetDateTime(3)
            //                 });
            //             }
            //         }
            //     }
            // }


            // var extrato = new
            // {
            //     saldo = new
            //     {
            //         total = client.saldo,
            //         data_extrato = DateTime.UtcNow,
            //         client.limite
            //     },
            //     ultimas_transacoes
            // };
            // return Results.Ok(extrato);
        });

        app.Run();
    }
}