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
    hora_criacao timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
    id_cliente integer NOT NULL
);

CREATE INDEX idx_customer_id ON cliente(id);

CREATE INDEX idx_transaction_customer_id ON transacao(id_cliente);

INSERT INTO cliente(id, limite, saldo)
    VALUES (1, 100000, 0),
(2, 80000, 0),
(3, 1000000, 0),
(4, 10000000, 0),
(5, 500000, 0);

-- CREATE TYPE saldo_limite AS (
--     saldo_cliente int4,
--     limite_cliente int4,
--     success bool
-- );

-- CREATE OR REPLACE FUNCTION criar_transacao_debito(valor integer, id_cliente int4, descricao varchar(10))
--     RETURNS saldo_limite
--     LANGUAGE plpgsql
--     AS $$
-- DECLARE
--     result saldo_limite;
-- BEGIN
--     UPDATE
--         cliente
--     SET
--         saldo = saldo - valor
--     WHERE
--         id = id_cliente
--         AND saldo - valor >= limite *(-1)
--     RETURNING
--         saldo,
--         limite INTO result.saldo_cliente,
--         result.limite_cliente;
--     IF result.saldo_cliente >= result.limite_cliente *(-1) THEN
--         result.success := TRUE;
--         INSERT INTO transacao(valor, tipo, descricao, id_cliente)
--             VALUES (valor, 'd', descricao, id_cliente);
--     ELSE
--         result.success := FALSE;
--     END IF;
--     RETURN result;
-- END;
-- $$;

-- CREATE OR REPLACE FUNCTION criar_transacao_credito(valor integer, id_cliente int4, descricao varchar(10))
--     RETURNS saldo_limite
--     LANGUAGE plpgsql
--     AS $$
-- DECLARE
--     result saldo_limite;
-- BEGIN
--     UPDATE
--         cliente
--     SET
--         saldo = saldo + valor
--     WHERE
--         id = id_cliente
--     RETURNING
--         saldo,
--         limite INTO result.saldo_cliente,
--         result.limite_cliente;
--     result.success := TRUE;
--     INSERT INTO transacao(valor, tipo, descricao, id_cliente)
--         VALUES (valor, 'c', descricao, id_cliente);
--     RETURN result;
-- END;
-- $$;

