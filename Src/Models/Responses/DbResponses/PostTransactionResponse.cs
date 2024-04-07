
/// <summary>
/// The main objective of this object is simply provide a type system for dapper's queries
/// </summary>
public class PostTransactionResponse
{
    public bool success { get; set; }
    public int client_limit { get; set; }
    public int new_balance { get; set; }
}