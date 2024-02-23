using System.Collections.Concurrent;
using Npgsql;
using NpgsqlTypes;

public class ExtratoPool
{
    private readonly ConcurrentQueue<NpgsqlCommand> command_pool;
    private const int POOL_SIZE = 500;

    public ExtratoPool()
    {
        command_pool = InitPool();
    }

    private ConcurrentQueue<NpgsqlCommand> InitPool()
    {
        Console.WriteLine($"Filling Crebitos Pool with {POOL_SIZE} Npgsql Insert Commands");
        var result = new ConcurrentQueue<NpgsqlCommand>();

        for (int i = 0; i < POOL_SIZE; i++)
            result.Enqueue(CreateCommand());

        return result;
    }

    private NpgsqlCommand CreateCommand()
    {
        // // pre-configuring command to select client inputs
        // var selectClient = new NpgsqlCommand("SELECT * FROM cliente WHERE id = $1 LIMIT 1;");
        // selectClient.Parameters.Add(new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer });

        // pre-configuring command to select client's transaction inputs
        var selectTransactions = new NpgsqlCommand(
            @"SELECT valor, tipo, descricao, realizada_em
            FROM transacao
            WHERE id_cliente = $1
            ORDER BY realizada_em DESC LIMIT 10;"
        );
        selectTransactions.Parameters.Add(new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer });

        return selectTransactions;
    }

    public NpgsqlCommand DequeueCommand()
    {
        if (
            command_pool.IsEmpty
            || !command_pool.TryDequeue(out var command)
        ) return CreateCommand();
        else return command;
    }

    public void RequeueCommand(NpgsqlCommand command)
    {
        command.Parameters[0] = new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer };
        command.Connection = null; // detaching the command from the data source connection but not closing it

        command_pool.Enqueue(command);
    }
}