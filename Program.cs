using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // var dbHostName = Environment.GetEnvironmentVariable("DB_HOSTNAME") ?? "localhost"; // uncomment if testing with `dotnet run`
        var dbHostName = Environment.GetEnvironmentVariable("DB_HOSTNAME") ?? "db"; // comment if testing with `dotnet run`
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
            MinPoolSize = 75,
            MaxPoolSize = 75,
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
                || transaction.valor % 1 != 0 // reject double values
                || transaction.valor <= 0 // reject useless and negative valued transactions
            ) return Results.UnprocessableEntity();

            // saving processing time :)
            if (validClientIds.FirstOrDefault(item => item == id) == null)
                return Results.NotFound();

            var client = new Client();
            using (var connection = await dataSource.OpenConnectionAsync())
            {
                await using var search = new NpgsqlCommand("SELECT * FROM cliente WHERE id = @id LIMIT 1", connection);
                search.Parameters.Add("@id", NpgsqlDbType.Integer).Value = id;


                using (var reader = await search.ExecuteReaderAsync())
                {
                    // this would be more appropriate in a real world scenario
                    // if (!reader.HasRows)
                    //     return Results.NotFound();

                    await reader.ReadAsync();

                    client.id = id;
                    client.limite = reader.GetInt32(1);
                    client.saldo = reader.GetInt32(2);
                }

                if (transaction.tipo == 'c') // credit
                    client.saldo += (int)transaction.valor;
                else if (transaction.tipo == 'd') // debit
                {
                    if (client.saldo - transaction.valor < -client.limite)
                        return Results.UnprocessableEntity();
                    client.saldo -= (int)transaction.valor;
                }

                using (var trans = await connection.BeginTransactionAsync())
                {
                    try
                    {
                        var update = new NpgsqlBatchCommand(
                            "UPDATE cliente SET saldo = @newBalance WHERE id = @id"
                        );
                        update.Parameters.Add("@newBalance", NpgsqlDbType.Integer).Value = client.saldo;
                        update.Parameters.Add("@id", NpgsqlDbType.Integer).Value = client.id;

                        var newTransaction = new NpgsqlBatchCommand(
                            "INSERT INTO transacao(valor, tipo, descricao, id_cliente) VALUES (@valor, @tipo, @descricao, @id);"
                        );
                        newTransaction.Parameters.Add("@valor", NpgsqlDbType.Integer).Value = transaction.valor;
                        newTransaction.Parameters.Add("@tipo", NpgsqlDbType.Char).Value = transaction.tipo;
                        newTransaction.Parameters.Add("@descricao", NpgsqlDbType.Varchar).Value = transaction.descricao;
                        newTransaction.Parameters.Add("@id", NpgsqlDbType.Integer).Value = id;

                        await using var batch = new NpgsqlBatch(connection) { BatchCommands = { update, newTransaction } };
                        try
                        {
                            var result = await batch.ExecuteNonQueryAsync();
                            await trans.CommitAsync();
                            return Results.Ok(new { limite = client.limite, saldo = client.saldo });
                        }
                        catch (DbException)
                        {
                            Console.WriteLine("One of the transactions (INSERT or UPDATE) failed. Rolling back");
                            throw;
                        }
                    }
                    catch (NpgsqlException ex)
                    {
                        await trans.RollbackAsync();
                        return Results.Problem(detail: $"Database operation failed\n{ex}", statusCode: 500);
                    }
                }
            }
        });

        app.MapGet("/clientes/{id}/extrato", async ([FromRoute] int id, NpgsqlDataSource dataSource) =>
        {
            var client = new Client() { id = id };
            var ultimas_transacoes = new List<object>();

            try
            {
                using (var connection = await dataSource.OpenConnectionAsync()) // maybe close connection after each operation?
                {
                    // saving processing time :)
                    if (new List<int?>() { 1, 2, 3, 4, 5 }.FirstOrDefault(item => item == id) == null)
                        return Results.NotFound();

                    await using var search = new NpgsqlCommand(
                        @"SELECT * FROM cliente WHERE id = @id LIMIT 1;",
                        connection
                    );
                    search.Parameters.Add("@id", NpgsqlDbType.Integer).Value = id;

                    using (var reader = await search.ExecuteReaderAsync())
                    {
                        // this would be more appropriate in a real world scenario
                        // if (!reader.HasRows)
                        //     return Results.NotFound();
                        await reader.ReadAsync();
                        client.limite = reader.GetInt32(1);
                        client.saldo = reader.GetInt32(2);
                    }

                    await using var lastTransactions = new NpgsqlCommand(
                        @"SELECT valor, tipo, descricao, realizada_em
                        FROM transacao
                        WHERE id_cliente = @id
                        ORDER BY realizada_em DESC LIMIT 10;",
                        connection
                    );
                    lastTransactions.Parameters.Add("@id", NpgsqlDbType.Integer).Value = id;

                    using (var extratoReader = await lastTransactions.ExecuteReaderAsync())
                    {
                        if (!extratoReader.HasRows)
                        {
                            return Results.Ok(new
                            {
                                saldo = new
                                {
                                    total = client.saldo,
                                    data_extrato = DateTime.UtcNow,
                                    limite = client.limite
                                },
                                ultimas_transacoes = ultimas_transacoes
                            });
                        };
                        while (await extratoReader.ReadAsync())
                        {
                            ultimas_transacoes.Add(new
                            {
                                valor = extratoReader.GetInt32(0),
                                tipo = extratoReader.GetChar(1),
                                descricao = extratoReader.GetString(2),
                                realizada_em = extratoReader.GetDateTime(3)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: $"Database operation failed\n{ex}", statusCode: 500);
            }

            var extrato = new
            {
                saldo = new
                {
                    total = client.saldo,
                    data_extrato = DateTime.UtcNow,
                    client.limite
                },
                ultimas_transacoes
            };
            return Results.Ok(extrato);
        });

        app.Run();
    }
}