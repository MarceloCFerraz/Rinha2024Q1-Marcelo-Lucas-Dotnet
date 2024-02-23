
using System.Collections.Concurrent;
using Npgsql;
using NpgsqlTypes;

public class CrebitosPool
{
    private readonly ConcurrentQueue<NpgsqlBatch> batch_pool;
    private const int POOL_SIZE = 4000;

    public CrebitosPool()
    {
        batch_pool = InitPool();
    }

    private ConcurrentQueue<NpgsqlBatch> InitPool()
    {
        Console.WriteLine($"Filling Crebitos Pool with {POOL_SIZE} Npgsql Insert Commands");
        var result = new ConcurrentQueue<NpgsqlBatch>();

        for (int i = 0; i < POOL_SIZE; i++)
            result.Enqueue(CreateBatch());

        return result;
    }

    private NpgsqlBatch CreateBatch()
    {
        // pre-configuring command inputs in order
        var update = new NpgsqlBatchCommand("UPDATE cliente SET saldo = $1 WHERE id = $2");
        update.Parameters.Add(new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer });
        update.Parameters.Add(new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer });

        var insert = new NpgsqlBatchCommand("INSERT INTO transacao(valor, tipo, descricao, id_cliente) VALUES ($1, $2, $3, $4);");
        insert.Parameters.Add(new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer });
        insert.Parameters.Add(new NpgsqlParameter<char>() { NpgsqlDbType = NpgsqlDbType.Char });
        insert.Parameters.Add(new NpgsqlParameter<string>() { NpgsqlDbType = NpgsqlDbType.Varchar });
        insert.Parameters.Add(new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer });

        var batch = new NpgsqlBatch() { BatchCommands = { update, insert } };
        return batch;
    }

    public NpgsqlBatch DequeueBatch()
    {
        if (
            batch_pool.IsEmpty
            || !batch_pool.TryDequeue(out var command)
        ) return CreateBatch();
        else return command;
    }

    public void RequeueBatch(NpgsqlBatch batch)
    {
        batch.BatchCommands[0].Parameters[0] = new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer };
        batch.BatchCommands[0].Parameters[1] = new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer };

        batch.BatchCommands[1].Parameters[0] = new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer };
        batch.BatchCommands[1].Parameters[1] = new NpgsqlParameter<char>() { NpgsqlDbType = NpgsqlDbType.Varchar };
        batch.BatchCommands[1].Parameters[2] = new NpgsqlParameter<string>() { NpgsqlDbType = NpgsqlDbType.Integer };
        batch.BatchCommands[1].Parameters[3] = new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer };

        batch.Connection = null;
        // command.Connection = null;  // Connection is not handled by NpgsqlBatchCommand, but by NpgsqlTransaction

        batch_pool.Enqueue(batch);
    }
}