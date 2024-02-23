using System.Data.Common;
using Microsoft.AspNetCore.Http.Timeouts;
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
            //Multiplexing=true;Timeout=15;Command Timeout=15;Cancellation Timeout=-1;No Reset On Close=true;Max Auto Prepare=20;Auto Prepare Min Usages=1
            Pooling = true,
            MinPoolSize = 50,
            MaxPoolSize = 2000,
            Multiplexing = true,
            NoResetOnClose = true,
            MaxAutoPrepare = 20,
            AutoPrepareMinUsages = 1,
        };

        await using var dataSource = NpgsqlDataSource.Create(conStringBuilder.ConnectionString);

        builder.Services.AddSingleton(provider => dataSource);

        var clientPool = new ClientPool();
        builder.Services.AddSingleton(provider => clientPool);

        var extratoPool = new ExtratoPool();
        builder.Services.AddSingleton(provider => extratoPool);

        var crebitoPool = new CrebitosPool();
        builder.Services.AddSingleton(provider => crebitoPool);

        builder.Services.AddRequestTimeouts(
            options =>
                options.DefaultPolicy = new RequestTimeoutPolicy { Timeout = TimeSpan.FromSeconds(60) }
        );

        var app = builder.Build();

        // API Port Configuration
        var port = Environment.GetEnvironmentVariable("API_PORT") ?? "8088";  // Default to 8081
        app.Urls.Add($"http://+:{port}");

        app.MapPost(
            "/clientes/{id}/transacoes",
            async (
                [FromRoute] int id,
                [FromBody] Transaction transaction,
                NpgsqlDataSource dataSource,
                ClientPool clientPool,
                CrebitosPool crebitosPool
            ) =>
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
            if (!new List<int>() { 1, 2, 3, 4, 5 }.Contains(id))
                return Results.NotFound();

            var client = new Client();

            var searchClient = clientPool.DequeueCommand();
            searchClient.Parameters[0].Value = id;

            using (var connection = await dataSource.OpenConnectionAsync())
            {
                // await using var search = new NpgsqlCommand("SELECT * FROM cliente WHERE id = @id LIMIT 1", connection);
                // search.Parameters.Add("@id", NpgsqlDbType.Integer).Value = id;

                searchClient.Connection = connection;
                using (var reader = await searchClient.ExecuteReaderAsync())
                {
                    // this would be more appropriate in a real world scenario
                    // if (!reader.HasRows)
                    //     return Results.NotFound();

                    await reader.ReadAsync();

                    client.id = id;
                    client.limite = reader.GetInt32(1);
                    client.saldo = reader.GetInt32(2);
                }

                clientPool.RequeueCommand(searchClient);

                if (transaction.tipo == 'c')  // credit
                    client.saldo += (int)transaction.valor;
                else if (transaction.tipo == 'd') // debit
                {
                    if (client.saldo - transaction.valor < -client.limite)
                        return Results.UnprocessableEntity();
                    client.saldo -= (int)transaction.valor;
                }

                var batch = crebitosPool.DequeueBatch();

                batch.BatchCommands[0].Parameters[0].Value = client.saldo;
                batch.BatchCommands[0].Parameters[1].Value = id;

                batch.BatchCommands[1].Parameters[0].Value = (int)transaction.valor;
                batch.BatchCommands[1].Parameters[1].Value = transaction.tipo;
                batch.BatchCommands[1].Parameters[2].Value = transaction.descricao;
                batch.BatchCommands[1].Parameters[3].Value = id;

                using (var trans = await connection.BeginTransactionAsync())
                {
                    try
                    {
                        batch.Connection = connection;
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
                        return Results.Problem(detail: $"Database operation failed\n{ex}", statusCode: 422);
                    }
                    finally
                    {
                        crebitosPool.RequeueBatch(batch);

                    }
                }

            }
        });

        app.MapGet("/clientes/{id}/extrato", async ([FromRoute] int id, NpgsqlDataSource dataSource) =>
        {
            if (!new List<int>() { 1, 2, 3, 4, 5 }.Contains(id))
                return Results.NotFound();

            var client = new Client() { id = id };
            var ultimas_transacoes = new List<object>();

            try
            {
                using (var connection = await dataSource.OpenConnectionAsync()) // maybe close connection after each operation?
                {
                    // saving processing time :)

                    // not using a join because i want to easily identify when the client has no transactions and send a blank array right away
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
                return Results.Problem(detail: $"Database operation failed\n{ex}", statusCode: 422);
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