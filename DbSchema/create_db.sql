SET timezone TO 'UTC';

SET statement_timeout = 0;

SET lock_timeout = 0;

SET idle_in_transaction_session_timeout = 0;

SET client_encoding = 'UTF8';

SET standard_conforming_strings = ON;

SET check_function_bodies = FALSE;

SET xmloption = content;

SET client_min_messages = warning;

SET row_security = OFF;

SET default_tablespace = '';

SET default_table_access_method = heap;

DROP TABLE IF EXISTS cliente;

DROP TABLE IF EXISTS transacao;

CREATE TABLE cliente(
    id serial PRIMARY KEY,
    limite integer NOT NULL,
    saldo integer NOT NULL DEFAULT 0
);

CREATE TABLE transacao(
    id serial PRIMARY KEY,
    valor integer NOT NULL,
    descricao varchar(10) NOT NULL,
    tipo char NOT NULL,
    realizada_em timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    id_cliente integer NOT NULL
);

CREATE INDEX idx_cliente_id ON cliente(id);

CREATE INDEX idx_transacao_cliente_tempo ON transacao(id_cliente, realizada_em DESC);

CREATE INDEX idx_transacao_cliente_id ON transacao(id_cliente);

CREATE MATERIALIZED VIEW recent_transactions AS
SELECT
    id_cliente,
    realizada_em,
    json_agg(json_build_object(
        'valor', valor,
        'tipo', tipo,
        'descricao', descricao,
        'realizada_em', realizada_em
    )) FILTER (WHERE valor IS NOT NULL)::jsonb as trans
FROM transacao
GROUP BY id_cliente, realizada_em;

CREATE UNIQUE INDEX idx_recent_transactions ON recent_transactions(id_cliente, realizada_em);


INSERT INTO cliente(id, limite, saldo)
    VALUES (1, 1000 * 100, 0),
(2, 800 * 100, 0),
(3, 10000 * 100, 0),
(4, 100000 * 100, 0),
(5, 5000 * 100, 0);

CREATE OR REPLACE PROCEDURE refresh_recent_transactions() AS $$
BEGIN
    REFRESH MATERIALIZED VIEW recent_transactions;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION debit(_customer_id int, _transaction_value int, _transaction_description varchar(10))
RETURNS TABLE(success boolean, client_limit int, new_balance int)
LANGUAGE plpgsql
AS $$
DECLARE
    client_balance int;
    client_limit int;
BEGIN
    PERFORM pg_advisory_xact_lock(_customer_id);

    SELECT
        saldo, limite
    INTO
        client_balance, client_limit
    FROM cliente
    WHERE id = _customer_id
    FOR UPDATE;

    IF (client_balance - _transaction_value) >= (-1 * client_limit) THEN
        UPDATE cliente
            SET saldo = saldo - _transaction_value
        WHERE id = _customer_id;

        INSERT INTO transacao(id_cliente, valor, tipo, descricao)
            VALUES (_customer_id, _transaction_value, 'd', _transaction_description);
        CALL refresh_recent_transactions();

        RETURN QUERY SELECT TRUE, client_limit, client_balance - _transaction_value;
    ELSE --tried doing the other way around (new balance < -limit), but apparently a return doesn't end the function call, so even if the operation is not allowed, the balance gets updated
        RETURN QUERY SELECT FALSE, -1, -1;
    END IF;

    EXCEPTION WHEN OTHERS THEN
        RAISE NOTICE 'Transaction rolled back: %', SQLERRM;
END;
$$;


CREATE OR REPLACE FUNCTION credit(_customer_id int, _transaction_value int, _transaction_description varchar(10))
RETURNS TABLE(success boolean, client_limit int, new_balance int)
LANGUAGE plpgsql
AS $$
DECLARE
    client_balance int;
    client_limit int;
BEGIN
    PERFORM pg_advisory_xact_lock(_customer_id);

    SELECT
        saldo, limite
    INTO
        client_balance, client_limit
    FROM cliente
    WHERE id = _customer_id
    FOR UPDATE;

    -- Update the balance
    UPDATE cliente
        SET saldo = saldo + _transaction_value
    WHERE id = _customer_id;

    -- Insert the transaction
    INSERT INTO transacao(id_cliente, valor, tipo, descricao)
        VALUES (_customer_id, _transaction_value, 'c', _transaction_description);

    -- Return success, limit, and the updated balance
    RETURN QUERY SELECT TRUE, client_limit, client_balance + _transaction_value;

    EXCEPTION WHEN OTHERS THEN
        RAISE NOTICE 'Transaction rolled back: %', SQLERRM;
END;
$$;

-- Still haven't tested using this one
CREATE OR REPLACE FUNCTION get_client_data(_customer_id int)
RETURNS TABLE(
    customer_balance int,
    customer_limit int,
    report_date timestamptz,
    last_transactions jsonb
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT saldo,
        limite,
        CURRENT_TIMESTAMP as data_extrato,
        COALESCE(recent_transactions.trans, '[]')
    FROM cliente
    LEFT JOIN recent_transactions ON cliente.id = recent_transactions.id_cliente
    WHERE cliente.id = _customer_id
    ORDER BY realizada_em DESC
    LIMIT 10;
END;
$$

-- SELECT saldo as total, limite, CURRENT_TIMESTAMP as data_extrato, json_agg(to_jsonb(trans) - 'id_cliente') as ultimas_transacoes FROM cliente LEFT OUTER JOIN ( SELECT valor, tipo, descricao, realizada_em, id_cliente FROM transacao WHERE id_cliente = 1 ORDER BY realizada_em DESC LIMIT 10 ) trans ON cliente.id = trans.id_cliente WHERE cliente.id = 1 GROUP BY saldo, limite;
-- OUTPUTS:
--  total | limite |         data_extrato          | ultimas_transacoes
-- -------+--------+-------------------------------+--------------------
--      0 | 100000 | 2024-02-25 14:05:55.663607+00 | [null]

-- SELECT saldo as total, limite, CURRENT_TIMESTAMP as data_extrato, COALESCE(json_agg(json_build_object('valor', trans.valor,'tipo', trans.tipo,'descricao', trans.descricao,'realizada_em', trans.realizada_em)) FILTER (WHERE trans.valor IS NOT NULL), '[]') as ultimas_transacoes FROM cliente LEFT OUTER JOIN ( SELECT * FROM transacao WHERE id_cliente = 1 ORDER BY realizada_em DESC LIMIT 10 ) trans ON cliente.id = trans.id_cliente WHERE cliente.id = 1 GROUP BY saldo, limite;

-- OUTPUTS:
--  total | limite |         data_extrato          |                              ultimas_transacoes
-- -------+--------+-------------------------------+------------------------------------------------------------------------------
--      0 | 100000 | 2024-02-25 14:05:34.139764+00 | [{"valor" : null, "tipo" : null, "descricao" : null, "realizada_em" : null}]

-- UPDATE cliente SET saldo = saldo + 100 WHERE id = 1; INSERT INTO transacao(id_cliente, valor, tipo, descricao) VALUES (1, 100, 'c', 'test');
-- UPDATE cliente SET saldo = 0 WHERE id = 1; DELETE FROM transacao WHERE id_cliente = 1;

-- SELECT * FROM cliente WHERE id = @id LIMIT 1;
-- SELECT valor, tipo, descricao, realizada_em FROM transacao WHERE id_cliente = @id ORDER BY realizada_em DESC LIMIT 10;
