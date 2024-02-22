**Data Limite**: `10/03/2024`

**Data Resultados**: `14/03/2024`

**Um projeto por participante?**: Não. Cada equipe ou participante pode mandar quantos projetos quiser

**Para participar**:

-   Fazer um pull request [neste repositório](https://github.com/zanfranceschi/rinha-de-backend-2024-q1/tree/main) incluindo um subdiretório em [participantes](https://github.com/zanfranceschi/rinha-de-backend-2024-q1/blob/main/participantes) (com nome dos participantes e, opcionalmente, com stack ou identificador - e.g. `marcelo-dotnet8` ou `marcelo-1`) com os arquivos:
    -   `docker-compose.yml`: contendo a declaração dos serviços que compõe sua API respeitando as [restrições de CPU/memória](https://github.com/zanfranceschi/rinha-de-backend-2024-q1/tree/main#restricoes) e [arquitetura mínima](https://github.com/zanfranceschi/rinha-de-backend-2024-q1/tree/main#arquitetura).
        -   todos os serviços declarados aqui devem estar publicamente disponíveis!
        -   Para isso, você pode criar uma conta em [hub.docker.com](hub.docker.com) para disponibilizar suas imagens.
        -   Essa imagens geralmente terão o formato `user/image-name:tag` – por exemplo, `zanfranceschi/rinha-api:latest`.
        -   O ambiente em que os testes serão executados é Linux x64. Portanto, fazer o build do docker da seguinte forma: `docker buildx build --platform linux/amd64`. Exemplo:
            -   `docker buildx build --platform linux/amd64 -t ana/minha-api-matadora:latest .`
    -   `README.md`: deve incluir no mínimo:
        -   nome(s) dos participante(s),
        -   stack,
        -   link para o repositório **público** do código fonte da API, e
        -   alguma forma de entrar em contato caso vença.
        -   Informações adicionais como link para site, linkedin, etc são opcionais mas são permitidas.
    -   quaisquer outros diretórios/arquivos necessários para que seus contêineres subam corretamente como, por exemplo:
        -   `nginx.conf`
        -   `banco.sql`

[Exemplo de submissão](https://github.com/zanfranceschi/rinha-de-backend-2024-q1/blob/main/participantes/exemplo)

---

# Arquitetura Mínima

No mínimo, a arquitetura deve conter os seguintes serviços:

-   **1 load balancer** que faça a distribuição de tráfego usando o algoritmo **round robin**.
    -   O load balancer será o serviço que receberá as requisições do teste e ele **precisa aceitar requisições na porta 9999**!
-   **2 instâncias de servidores web** que atenderão às requisições HTTP (distribuídas pelo load balancer).
-   **1 banco de dados relacional ou não relacional** (exceto mem-cache).

---

# Restrições de CPU/Memória

Dentro do arquivo `docker-compose.yml`, os recursos devem ser limitados de acordo com os valores abaixo:

-   `deploy.resources.limits.cpu`: 1.5
    -   uma unidade e meia de CPU para ser dividida entre todos os serviços (nginx, apis e db)
-   `deploy.resources.limits.memory`: 550MB
    -   550 mega bytes de memória para ser dividida entre todos os serviços (nginx, apis e db)

Obs.: Por favor, use `MB` para unidade de medida de memória; isso facilita as verificações de restrições.

```yaml
# exemplo de parte de configuração de um serviço dentro do um arquivo docker-compose.yml
---
nginx:
    image: nginx:latest
    volumes:
        - ./nginx.conf:/etc/nginx/nginx.conf:ro
    depends_on:
        - api01
        - api02
    ports:
        - "9999:9999"
    deploy:
        resources:
            limits:
                cpus: "0.17" # sobra 1.33 pro resto dos serviços
                memory: "10MB" # sobra 540 MB pro resto dos serviços
```

[Arquivos de Conf de Exemplo](https://github.com/zanfranceschi/rinha-de-backend-2024-q1/tree/main#arquivos-de-exemplo-para-te-ajudar)

---

# Testando

A ferramenta Gatling será usada para realizar o teste de performance. O script de teste para avaliação está disponível em [load-test](https://github.com/zanfranceschi/rinha-de-backend-2024-q1/blob/main/load-test).

1. Baixe o Gatling em [https://gatling.io/open-source/](https://gatling.io/open-source/)
2. Certifique-se de que tenha o JDK instalado (64bits OpenJDK LTS (Long Term Support) versions: 11, 17 e 21) [https://gatling.io/docs/gatling/tutorials/installation/](https://gatling.io/docs/gatling/tutorials/installation/)
3. Certifique-se de configurar a variável de ambiente GATLING_HOME para o diretório da instalação do Gatling. Para se certificar de que a variável está correta, os seguinte caminhos precisam ser válidos: `$GATLING_HOME/bin/gatling.sh` no Linux e `%GATLING_HOME%\bin\gatling.bat` no Windows.
4. Configure o script `./executar-teste-local.sh` (ou `.\executar-teste-local.ps1` se estiver no Windows)
5. Suba sua API (ou load balancer) na porta 9999
6. Execute `./executar-teste-local.sh` (ou `.\executar-teste-local.ps1` se estiver no Windows)
7. Agora é só aguardar o teste terminar e abrir o relatório O caminho do relatório é exibido ao término da simulação. Os resultados/relatórios são salvos em `./load-test/user-files/results`.

Antes do teste iniciar, um script verificará se a API está respondendo corretamente via `GET /clientes/1/extrato` por até 40 segundos em intervalos de 2 segundos a cada tentativa. Por isso, **certifique-se de que todos seus serviços não demorem mais do que 40 segundos para estarem aptos a receberem requisições**!

---

# Endpoints:

## `POST /clientes/[id]/transacoes`:

### Query Params

-   `id (int)`: **REQUIRED**. id do cliente

### Request Body

```json
{
    "valor": 1000,
    "tipo": "c",
    "descricao": "descricao"
}
```

-   `valor (int)`: **REQUIRED**. valor em centavos a decrementar do saldo do cliente. EX: R$ 10,00 = 1000 centavos. o valor deve ser sempre positivo e deve ser armazenado no banco como positivo e convertido para negativo sob demanda.
-   `tipo (char)`: **REQUIRED**.
    -   `c`: crédito incrementa valor do saldo (considerar como `income` ao invés de crédito de cartão)
    -   `d`: débito decrementa valor do saldo (ver regras)
    -   `outro`: sem regras explícitas, ver regras abaixo
-   `descricao (string)`: **REQUIRED**. identificador textual da transação de **1 a 10 caracteres**. Não precisa ser único.

### Regras

-   Transações com valor não inteiro ou devem ser rejeitadas com **status code 422 ou 400** (sem obrigatoriedade de ter response body) de acordo com o teste de carga
-   Transações com `tipo` diferente de `c` ou `d` devem ser rejeitadas com **status code 422** (sem obrigatoriedade de ter response body) de acordo com o teste de carga
-   Transações com `descricao` com mais de 10 caracteres devem ser rejeitadas com **status code 422** (sem obrigatoriedade de ter response body) de acordo com o teste de carga
-   Transações com valor nulo devem ser rejeitadas com **status code 422 ou 400** (sem obrigatoriedade de ter response body) de acordo com o teste de carga
-   Transações de débito que reduziriam o saldo do cliente para um valor menor que seu limite disponível devem ser rejeitadas com **status code 422** (sem obrigatoriedade de ter response body).
    -   Ex: um cliente com limite de 1000 (R$ 10) nunca deverá ter o saldo menor que -1000 (R$ -10).
-   Transações com `id` de um cliente não existente devem ser rejeitadas com **status code 404** sem obrigatoriedade de ter response body.

### Response bodies

-   Transação bem sucedida:
    Retornar status code 200 com response body conforme exemplo:

```json
{
    "limite": 100000,
    "saldo": -9098
}
```

-   Transação mal sucedida:
    Não obrigatório

### Notas

-   Cadastrar timestamp de transações bem sucedidas no banco, pois serão usadas

---

## `GET /clientes/[id]/extrato`

### Query Params

-   `id (int)`: **REQUIRED**. id do cliente

### Request Body

-   Não haverá

### Regras

-   Requisições com`id` não existente devem ser rejeitadas com ==**status code 404**== sem obrigatoriedade de ter response body.

### Response bodies

-   Transação bem sucedida:
    Retornar status code 200 com response body conforme exemplo:

```jsonc
{
    "saldo": {
        "total": -9098, // saldo atual do cliente
        "data_extrato": "2024-01-17T02:34:41.217753Z", // timestamp do pedido de extrato
        "limite": 100000 // limite cadastrado do cliente
    },
    "ultimas_transacoes": [
        // lista das últimas 10 transações ordenada por data/hora de forma decrescente (mais recente para mais antiga)
        {
            "valor": 10,
            "tipo": "c",
            "descricao": "descricao",
            "realizada_em": "2024-01-17T02:34:38.543030Z" // timestamp da transação
        },
        {
            "valor": 90000,
            "tipo": "d",
            "descricao": "descricao",
            "realizada_em": "2024-01-17T02:34:38.543030Z" // timestamp da transação
        }
    ]
}
```

-   Transação mal sucedida:
    Não obrigatório

---

# Cadastro inicial de clientes:

| id  | limite   | saldo inicial |
| --- | -------- | ------------- |
| 1   | 100000   | 0             |
| 2   | 80000    | 0             |
| 3   | 1000000  | 0             |
| 4   | 10000000 | 0             |
| 5   | 500000   | 0             |

## Regras

-   Não cadastrar cliente com ID 6, pois parte do teste irá verificar se o cliente com o ID 6 realmente não existe e a API retorna HTTP 404!

---
