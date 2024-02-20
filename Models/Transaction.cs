public class Transaction
{
    public int id { get; set; }
    public int valor { get; set; }
    public string descricao { get; set; }
    public string tipo { get; set; } // Using 'string' here, if 'c' or 'd'
    public DateTime hora_criacao { get; set; }
    public int id_cliente { get; set; }

    // id serial PRIMARY KEY,
    // valor integer NOT NULL,
    // descricao varchar(10) NOT NULL,
    // tipo char NOT NULL,
    // hora_criacao timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
    // id_cliente integer NOT NULL

}
