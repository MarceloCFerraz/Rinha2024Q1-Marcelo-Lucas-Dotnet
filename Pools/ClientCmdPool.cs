using System.Collections.Concurrent;
using Npgsql;
using NpgsqlTypes;

public class ClientPool
{
    private readonly ConcurrentQueue<NpgsqlCommand> command_pool;
    private const int POOL_SIZE = 500;

    public ClientPool()
    {
        command_pool = InitPool();
    }

    private ConcurrentQueue<NpgsqlCommand> InitPool()
    {
        Console.WriteLine($"Filling Crebitos Pool with {POOL_SIZE} Npgsql Insert Commands");
        var result = new ConcurrentQueue<NpgsqlCommand>();

        for (int i = 0; i < POOL_SIZE; i++)
            result.Enqueue(CreateCommand());

        Console.WriteLine("Done");
        return result;
    }

    private NpgsqlCommand CreateCommand()
    {
        // pre-configuring command to select client inputs
        var selectClient = new NpgsqlCommand("SELECT * FROM cliente WHERE id = $1 LIMIT 1;");
        selectClient.Parameters.Add(new NpgsqlParameter<int>() { NpgsqlDbType = NpgsqlDbType.Integer });

        return selectClient;
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