# BookStore

API REST de catálogo e empréstimos de biblioteca em .NET 10, com PostgreSQL, Entity Framework Core/Npgsql e Redis. O projeto trata concorrência entre réplicas, idempotência e preservação de histórico com auditoria transacional.

## Pré-requisitos

- Docker com Compose v2 e suporte a containers Linux.
- SDK .NET 10 para migrations, build e testes.
- PowerShell 7.1 ou superior para os comandos de configuração abaixo.
- Portas locais `15080` para a API e `15432` para o PostgreSQL disponíveis.

A API, o PostgreSQL 16 e o Redis 8 executam no Docker Compose. Não é necessário instalar os bancos no host ou ter conta Azure, DataDog ou cluster Kubernetes.

## Executar localmente

Execute os comandos na raiz do repositório.

### 1. Configurar o ambiente

```powershell
Copy-Item .env.example .env
```

Edite o `.env` e substitua os exemplos de `POSTGRES_USER` e `POSTGRES_PASSWORD` por valores locais próprios. O arquivo está ignorado pelo Git. `ASPNETCORE_ENVIRONMENT=Development` habilita OpenAPI e Scalar.

### 2. Iniciar PostgreSQL e Redis

```powershell
docker compose up -d --wait postgres redis
dotnet restore BookStore.sln
dotnet tool restore
```

O manifesto local fixa `dotnet-ef` em `10.0.13`. O Compose publica PostgreSQL em `localhost:15432`; o banco se chama `bookstore_db`. Redis fica acessível à API pela rede interna, sem porta publicada no host.

### 3. Aplicar as migrations

A API não aplica migrations no startup. Prepare o schema antes de iniciá-la ou implantar uma nova versão.

As ferramentas EF executam no host. Configure a conexão PostgreSQL no processo atual, usando os mesmos usuário e senha definidos no `.env`. Informe a connection string pelo prompt com entrada mascarada; os valores não precisam entrar no histórico de comandos.

```powershell
dotnet ef database update --project src/BookStore.Infrastructure --startup-project src/BookStore.Api
```

Caso você altere o POSTGRES_USER e POSTGRES_PASSWORD no .env antes de rodar a primeira vez. É necessário atualizar no appsettings.Development para que a migração consiga estabelecer conexão com o banco.

### 4. Iniciar a API

```powershell
docker compose up --build -d bookstore.api
docker compose ps
Invoke-RestMethod http://localhost:15080/health/live
Invoke-RestMethod http://localhost:15080/health/ready
```

Base da API: **http://localhost:15080**. O container pode estar em execução antes de Kestrel aceitar conexões; se uma consulta inicial falhar por conexão recusada, aguarde a inicialização e repita os health checks. Em `Development`, o host expõe OpenAPI e Scalar. A API aguarda os health checks iniciais de PostgreSQL e Redis; essa espera não aplica nem verifica a versão do schema.

Para acompanhar a saída ou encerrar os serviços:

```powershell
docker compose logs -f bookstore.api
docker compose down
```

O volume PostgreSQL preserva os dados após `down`. As migrations devem ser aplicadas ao banco que corresponde ao ambiente escolhido.

## Configuração

A aplicação usa `appsettings.json` e variáveis de ambiente. O separador `__` representa uma seção de configuração do .NET. O Compose lê os valores de `.env`; o inventário sem segredos está em [.env.example](.env.example).

| Variável do Compose | Configuração da API / finalidade | Padrão do Compose |
|---|---|---|
| `POSTGRES_USER` | Usuário do banco e composição da conexão da API | Definir localmente |
| `POSTGRES_PASSWORD` | Senha do banco e composição da conexão da API | Definir localmente |
| `ASPNETCORE_ENVIRONMENT` | Ambiente do host | `Development` no exemplo |
| `LOG_LEVEL` | `Serilog__MinimumLevel__Default` | `Verbose` |
| `LOAN_DURATION_DAYS` | `Loans__DurationDays` | `7` dias |
| `PAGINATION_DEFAULT_PAGE_SIZE` | `Pagination__DefaultPageSize` | `20` |
| `CATALOG_CACHE_TTL_SECONDS` | `CatalogCache__TimeToLiveSeconds` | `60` segundos |
| `CATALOG_CACHE_OPERATION_TIMEOUT_MS` | `CatalogCache__OperationTimeoutMilliseconds` | `200` milissegundos |
| `OTEL_SERVICE_NAME` | Nome do serviço na telemetria | `BookStore.Api` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Endereço de um receptor OTLP acessível à API | Vazio: sem exportação de rede |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | Protocolo de exportação: `http/protobuf` ou `grpc` | `http/protobuf` |

O Compose monta `ConnectionStrings__Postgres` com o host interno `postgres:5432` e fornece `ConnectionStrings__Redis=redis:6379`. Para ferramentas no host, configure `ConnectionStrings__Postgres` com a porta publicada, conforme o procedimento de migrations.

O prazo e os valores do cache devem ser positivos; o tamanho padrão da página deve estar entre 1 e 100. Em consultas, `pageSize` explícito prevalece sobre o padrão após validação. Para reduzir o volume de logs, pode-se definir `LOG_LEVEL=Information` no ambiente local.



## API

Os identificadores de recursos são GUIDs. As operações abaixo estão disponíveis:

| Método e rota | Comportamento |
|---|---|
| `POST /books` | Cria livro com título, ISBN, autor e quantidade; retorna `201` |
| `GET /books` | Lista o catálogo com paginação |
| `GET /books/{id}` | Consulta os dados de um livro |
| `PATCH /books/{id}` | Atualiza título, autor e/ou quantidade; retorna `204` |
| `DELETE /books/{id}` | Desativa o livro, preservando histórico; retorna `204` |
| `POST /users` | Cadastra usuário com nome; retorna `201` com identificador e nome |
| `GET /users/{id}/loans` | Consulta o histórico de empréstimos do usuário |
| `POST /loans` | Cria ou reproduz o resultado de um empréstimo; retorna `201` |
| `GET /loans/{id}` | Consulta um empréstimo |
| `POST /loans/{id}/return` | Registra devolução; retorna `204` |
| `POST /loans/{id}/cancel` | Registra cancelamento; retorna `204` |
| `GET /books/{id}/availability` | Consulta capacidade, empréstimos ativos e disponibilidade |
| `GET /books/{id}/history` | Consulta o histórico de empréstimos do livro |
| `GET /audit-events` | Consulta a auditoria com paginação e filtros |
| `GET /health/live` | Verifica o processo, sem consultar dependências |
| `GET /health/ready` | Verifica conectividade com PostgreSQL e Redis |

### Headers e corpos das requisições

As mutações de livros e empréstimos exigem `X-User-Id` com o GUID do **ator declarado**. Esse valor identifica quem solicita a mudança; o `userId` do empréstimo identifica o beneficiário. Não há autenticação implementada. Cadastro de usuário, consultas e health checks não exigem esse header.

`POST /loans` também exige `Idempotency-Key`, com uma única ocorrência e até 128 caracteres. Use uma chave nova para cada intenção de criação e preserve-a ao repetir a mesma intenção.

`X-Correlation-Id` é opcional: quando válido, acompanha a requisição nos logs de negócio, na auditoria e na resposta. Sem ele, a API usa o identificador gerado pelo servidor. O valor deve ser único no header, não vazio, sem caracteres de controle e ter até 128 caracteres.

Exemplos de corpos JSON:

```json
{
  "title": "Clean Code",
  "isbn": "9780132350884",
  "author": "Robert C. Martin",
  "quantity": 2
}
```

O corpo acima corresponde a `POST /books`. Para `POST /users`:

```json
{
  "name": "Maria"
}
```

Depois de obter os identificadores nas respostas, use-os no corpo de `POST /loans`:

```json
{
  "bookId": "<id do livro>",
  "userId": "<id do usuario>"
}
```

Em `PATCH /books/{id}`, envie apenas os campos a alterar. Campos ausentes mantêm o valor atual; `null` explícito e corpo sem campos editáveis são rejeitados. O ISBN não é alterado por esse endpoint. Quantidade menor que o número de empréstimos ativos resulta em `409`.

Repetir a devolução ou o cancelamento já confirmado retorna `204` sem novos efeitos. Tentar outra ação de encerramento para o mesmo empréstimo retorna `409`. O cancelamento mantém o registro e o histórico.

As consultas paginadas aceitam `page` e `pageSize`, com página inicial 1 e tamanho máximo 100, e retornam `items`, `page`, `pageSize` e `totalCount`. Os históricos incluem empréstimos ativos, devolvidos e cancelados, inclusive de livros desativados, ordenados por data do empréstimo e identificador decrescentes.

`GET /audit-events` aceita os filtros opcionais `entityName`, `entityId` e `correlationId`, combinados com AND. A resposta mantém `details` como string contendo o JSON armazenado e `action` como enum numérico.

A API usa `400` para entrada inválida, `404` para recurso inexistente e `409` para conflito de negócio. O tratamento global retorna `application/problem+json`, inclui `correlationId` e preserva mensagem genérica em falhas inesperadas `500`. Erros escritos por outros componentes do pipeline podem não incluir a extensão de correlação no corpo; o header continua sendo o meio de identificação da chamada.

## Arquitetura e decisões

O projeto organiza as dependências com Onion Architecture e os casos de uso por Vertical Slice:

```text
src/
  BookStore.Api/             Endpoints, headers, configuração e observabilidade
  BookStore.Application/     Casos de uso, validação e interfaces de persistência
  BookStore.Domain/          Entidades e regras de negócio
  BookStore.Infrastructure/  EF Core, PostgreSQL, migrations e Redis
tests/
  BookStore.UnitTests/
  BookStore.EndToEndTests/
deploy/k8s/
```

A Application coordena as operações sem depender de HTTP ou do provider de persistência. A Infrastructure implementa suas interfaces. MediatR encaminha os casos de uso, e FluentValidation valida comandos e consultas. O Domain mantém o estado e as transições das entidades.

### Modelo e histórico

`Book.Quantity` representa a capacidade total de unidades indistintas. Cada `Loan` ativo ocupa uma unidade; a disponibilidade é a diferença entre quantidade e empréstimos ativos. Não há identidade física por exemplar, porque o exercício pode ser atendido pela capacidade agregada.

O livro armazena título, ISBN normalizado, autor e estado ativo. Um índice único no PostgreSQL garante unicidade de ISBN, inclusive diante de requisições concorrentes. `DELETE` desativa em vez de apagar; novos empréstimos são rejeitados, mas os existentes continuam disponíveis para devolução ou cancelamento.

O empréstimo registra beneficiário, livro, datas UTC, previsão de devolução, estado e data de encerramento. O prazo padrão é de sete dias, configurável. Devolução e cancelamento liberam capacidade sem apagar o empréstimo. FKs restritivas impedem remover os recursos referenciados pelos empréstimos.

### Concorrência entre réplicas

As operações que afetam a capacidade de um livro usam transação `ReadCommitted` e lock de linha PostgreSQL com `SELECT ... FOR UPDATE` no registro de `Book`. A criação obtém o lock antes de verificar a quantidade de empréstimos ativos e confirmar a nova operação. Atualização de quantidade e encerramento de empréstimos seguem o mesmo protocolo; quando necessário, a ordem dos locks é `Book` antes de `Loan`.

Com uma unidade disponível, duas tentativas com chaves distintas disputam o mesmo registro. A segunda aguarda o commit da primeira, reavalia a disponibilidade e recebe `409`; somente uma cria o empréstimo com `201`. O PostgreSQL coordena essa decisão entre processos, sem lock em memória na API.

Essa escolha simplifica a proteção da regra de capacidade. O custo é serializar operações do mesmo livro e ocupar uma conexão durante a transação; transações devem permanecer curtas. Livros diferentes podem avançar em paralelo. Uma atualização condicional atômica também seria possível, mas exigiria outro modelo de controle de capacidade.

**Entre 2 e 11 réplicas**, todas as APIs precisam compartilhar o mesmo PostgreSQL e seguir esse protocolo. A quantidade de réplicas altera espera por locks, conexões e vazão, mas não o ponto de coordenação da regra. O índice único da chave de idempotência e a transação de auditoria permanecem no mesmo banco. Redis não participa da decisão de emprestar.

O lock protege as decisões que passam pelo protocolo da aplicação. Ele não detecta que um cliente editou metadados com base numa versão antiga: duas alterações de título podem seguir a política de última gravação. Controle otimista com versão/ETag seria uma evolução para esse cenário.

### Idempotência

A chave fica no próprio `Loan`, com índice único global. A criação consulta a chave antes e depois de obter o lock do livro. Se outra transação confirmar a mesma chave durante uma corrida, o handler descarta o tracking da tentativa revertida e lê o empréstimo confirmado.

Mesmo `bookId` e `userId` retornam o resultado de criação semanticamente equivalente, sem novo empréstimo, ocupação ou evento de auditoria. Reutilizar a chave com dados diferentes retorna `409`. O replay também funciona após devolução ou cancelamento, preservando os dados de criação.

Essa abordagem evita uma tabela adicional para o único endpoint idempotente do exercício. O custo é vincular a retenção da chave à do empréstimo e limitar o replay a esse contrato. A chave não é removida nem reutilizada. Uma infraestrutura genérica de idempotência faria sentido com mais endpoints ou necessidade de reproduzir respostas completas.

### Auditoria

Criação, alteração e desativação de livro; criação, devolução e cancelamento de empréstimo geram `AuditEvent`. Cada registro contém entidade, identificador, ação, ator declarado, timestamp UTC, correlação e detalhes JSON selecionados para explicar a mudança.

Book/Loan e auditoria usam o mesmo contexto e a mesma confirmação transacional. Se a gravação da auditoria falhar, a mudança de negócio não pode confirmar isoladamente. `UnitOfWork` coordena a persistência; as operações com leitura protegida por lock também controlam a transação explícita.

Detalhes específicos de cada ação evitam serializar a entidade inteira e expor campos sem necessidade. A alteração de livro registra antes/depois; encerramentos registram a transição. A opção aumenta a manutenção dos formatos de auditoria, mas preserva uma trilha compreensível. Logs técnicos não substituem esses registros.

### Cache

`GET /books` usa cache-aside: consulta Redis, busca PostgreSQL em caso de miss e armazena a página com TTL. A chave inclui geração do catálogo, página e tamanho. A geração é capturada antes da leitura do banco; criação, alteração e desativação de livro trocam essa geração após o commit.

Uma consulta que termina de preencher uma geração antiga não repopula a geração nova. Usamos StackExchange.Redis para o `SET NX` necessário à inicialização da geração, operação fora do contrato de `IDistributedCache`. A conexão é compartilhada e as operações têm timeout próprio.

O catálogo cacheado contém metadados e capacidade total, sem disponibilidade. Empréstimos e encerramentos não alteram esses campos; por isso não invalidam essa consulta. A disponibilidade consulta PostgreSQL, e a criação sempre revalida a capacidade na transação.

Falhas operacionais do cache fazem a leitura recorrer ao banco. Redis e PostgreSQL não confirmam numa transação conjunta: se a invalidação pós-commit falhar, páginas antigas podem permanecer até o TTL. Esse custo de consistência eventual afeta a listagem, não a decisão de criar empréstimos.

## Health checks e telemetria

`/health/live` consulta apenas o processo; `/health/ready` usa checks de bibliotecas para PostgreSQL e Redis. Os endpoints retornam o estado em texto com `200` ou `503`. Readiness confirma conectividade, não a aplicação das migrations.

Os handlers e serviços usam `ILogger<T>` com templates estruturados; Serilog escreve JSON em stdout. Os eventos de negócio incluem correlação, identificadores pertinentes e ator declarado nas mutações. `EventId` fica agrupado por funcionalidade em campos estáticos imutáveis. Logs de sucesso ocorrem após confirmação; replay e no-op são distintos de nova criação. Há pendências na emissão do resumo HTTP e no diagnóstico central de erros, listadas abaixo.

OpenTelemetry instrumenta HTTP e chamadas PostgreSQL/Npgsql. Os contadores de negócio são:

- `bookstore.loans.created`: empréstimos com commit confirmado;
- `bookstore.loans.rejected.unavailable`: rejeições por falta de capacidade;
- `bookstore.loans.idempotent.replays`: repetições idempotentes com dados equivalentes.

A métrica HTTP `http.server.request.duration` permite acompanhar a latência por método e rota, incluindo a criação de empréstimos. SQL parametrizado pode compor os traces PostgreSQL, sem habilitar registro de valores dos parâmetros. Redis não possui tracing nesta implementação.

A exportação OTLP é opcional e depende de um receptor acessível à API. O Compose não inclui Collector ou dashboard. Os sinais reiniciam com o processo e podem se perder em falhas; agregação entre réplicas pertence ao backend de observabilidade. A auditoria mantém sua persistência no PostgreSQL.

## Testes e verificações

```powershell
dotnet test BookStore.sln -c Release
```

Para executar cada projeto:

```powershell
dotnet test tests/BookStore.UnitTests/BookStore.UnitTests.csproj -c Release
dotnet test tests/BookStore.EndToEndTests/BookStore.EndToEndTests.csproj -c Release
```

A suíte contém 20 casos unitários e 19 E2E. Os E2E criam PostgreSQL e Redis próprios via Testcontainers, aplicam as migrations nesse banco vazio e iniciam duas APIs Kestrel com conexões independentes. Não usam o `.env` nem os dados locais do Compose.

A cobertura inclui regras do domínio, disputa pela última unidade, capacidade para duas criações, redução concorrente da quantidade, idempotência simultânea, chave global disputada entre livros, encerramentos concorrentes, histórico após devolução/cancelamento, auditoria, rollback e invalidação do catálogo. Os testes observam as duas requisições na cadeia de bloqueio do PostgreSQL antes de liberar a disputa. Duas APIs no mesmo processo de teste não equivalem a onze Pods ou a um teste de carga.

Para compilar e executar as verificações de qualidade:

```powershell
dotnet build BookStore.sln -c Release
dotnet format BookStore.sln --verify-no-changes --no-restore
dotnet list BookStore.sln package --vulnerable --include-transitive
```

Na revisão de 21/10/2026, os 39 testes passaram e a consulta de pacotes não reportou vulnerabilidades. A verificação global de formatação apontou sete ocorrências de whitespace em `UnitOfWork.cs`. Os testes comprovam os cenários executados, não ausência de falhas em todas as intercalações ou comportamentos de infraestrutura.

## Kubernetes

Os manifests em [deploy/k8s](deploy/k8s) incluem Deployment com duas réplicas, Service interno, ConfigMap, referências a Secret externo, requests/limits de CPU e memória, liveness e readiness probes. O container executa sem root e sem token automático de ServiceAccount.

Para utilizar os manifests, prepare PostgreSQL e Redis compartilhados, aplique as migrations, disponibilize a imagem nos nodes e crie por um mecanismo seguro o Secret `bookstore-api-connections` com as chaves `ConnectionStrings__Postgres` e `ConnectionStrings__Redis`. Os manifests não contêm valores reais nem provisionam os bancos.

```powershell
docker build -t bookstore.api:case -f src/BookStore.Api/Dockerfile .
kubectl apply -f deploy/k8s/
```

A imagem de exemplo usa `IfNotPresent`; em outro ambiente, ajuste a referência para uma imagem acessível ao cluster. Execute uma migração coordenada antes do rollout, em vez de permitir que cada réplica altere o schema no startup. O exercício não exige provisionar ou executar um cluster.

## Limitações conhecidas

- A invalidação de cache pode ser um problema durante uma queda do sistema, já que não está sendo executada de maneira atômica com o banco.
- O handler global não registra a causa das exceções tratadas. No .NET 10, os diagnósticos de exceções tratadas são suprimidos por padrão. Falta recuperar um evento sanitizado e correlacionado para falhas inesperadas.
- `X-User-Id` é uma declaração do cliente. Não há autenticação, autorização ou garantia da identidade do ator.
- O cache aceita dados antigos até o TTL em falha de invalidação. As transações do banco não abrangem Redis nem a entrega dos logs.
- Não há controle otimista de versões de metadados, garantia de todas as intercalações concorrentes ou dimensionamento de capacidade entre 2 e 11 réplicas.

Para produção, as prioridades seriam externalizar credenciais e gerir segredos; autenticar usuários e autorizar operações; fechar as pendências de logging; definir retenção/acesso de auditoria e logs; exercitar falhas de dependências e rollout; dimensionar pools de conexões e recursos sob carga; e avaliar controle de versão para edições de metadados. Se o negócio exigir consistência mais forte no catálogo, também seria necessário tornar a invalidação recuperável após falhas entre commit e Redis. Podendo ser utilizado o padrão Outbox aqui, mas deixei de lado pois fugia muito do escopo em relação ao tempo que tive para entrega.
