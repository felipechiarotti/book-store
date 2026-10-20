# BookStore

API de biblioteca para o desafio de empréstimos concorrentes e auditáveis em .NET 10, com PostgreSQL e Redis.

## Situação

Projeto em construção: fundação e cadastro de livro com quantidade e auditoria presentes; BookCopy foi removido do código.
A consulta por ID, listagem paginada e a migration de remoção de BookCopy estão presentes. A infraestrutura de usuários
conta com entidade, mapping, repositório scoped e migration AddUsers gerada, ainda não aplicada nesta etapa.
O código de POST /users está presente: recebe name e retorna201 com id/name normalizado, sem Location e sem exigir X-User-Id, conforme contrato aprovado.
Devolução e cancelamento também estão no código, com204, ClosedAt e auditoria transacional; repetir a mesma ação não gera novo efeito. Demais operações do catálogo, cache e verificação funcional ainda não estão concluídos. O contrato de
respostas HTTP está aprovado; a centralização dos erros foi adiada pelo arquiteto. Consulte
o [estado atual](docs/project/status.md) para evidências e limites.

A base de empréstimos conta com Loan/LoanStatus, FKs restritivas, chave de idempotência única e migration AddLoans
gerada sem acesso ao banco. O código de POST /loans e GET /loans/{id} está presente, com lock de Book, contagem de ativos, auditoria e recuperação idempotente. HTTP, persistência e concorrência real ainda não foram exercitados nesta etapa.

A consulta GET /books/{id}/availability está no código e retorna quantity, activeLoans, availableQuantity e isActive, sem ator/cache/lock pessimista. Livro inativo tem disponibilidade0. É uma fotografia, não uma reserva; POST /loans revalida a capacidade na transação. Ver [contrato e decisões](docs/project/availability.md). Execução HTTP e PostgreSQL permanecem pendentes.

## Documentação

| Leitor                       | Entrada                                        | Conteúdo                                                                |
|------------------------------|------------------------------------------------|-------------------------------------------------------------------------|
| Avaliação do case            | [Guia do case](docs/case/README.md)            | Enunciado, síntese das decisões, atendimento ao desafio e uso de IA     |
| Desenvolvimento e manutenção | [Documentação técnica](docs/project/README.md) | Arquitetura, regras, contratos, transações, correlação e ambiente local |
| Agentes Codex                | [AGENTS.md](AGENTS.md)                         | Instruções do repositório e navegação do processo assistido             |

O [índice de documentação](docs/README.md) define a responsabilidade de cada área.
O [enunciado original](docs/desafio-candidato-api-emprestimos-dotnet.md) é a fonte principal dos requisitos.

## Compilação

Pré-requisitos: SDK .NET 10 e Docker com Compose para executar PostgreSQL 16, Redis 8 e a API em containers. Não é
necessário instalar PostgreSQL/Redis no host nem provisionar um cluster Kubernetes para o ambiente local.

Na raiz, com SDK .NET 10:

```powershell
dotnet restore BookStore.sln
dotnet build BookStore.sln -c Release --no-restore
```

Build não comprova integridade transacional, concorrência, startup ou contratos HTTP. Preparação e limitações de
execução estão no [guia de desenvolvimento](docs/project/development.md).

## Configuração local

Não versionar .env, credenciais ou dados sensíveis. Usar .env.example somente com exemplos sanitizados. O .env está
ignorado; revisar o índice antes de commits autorizados. O hook de proteção preparado anteriormente está ausente no
último checkpoint, portanto não presumir que bloqueio adicional está ativo.

Os arquivos Compose e Dockerfile fazem parte da base local. O código atual chama MigrateAsync no startup da
API; iniciar o host pode alterar o banco configurado. O autor informou validação da migração para Book.Quantity; a assistência não repetiu sua aplicação ao banco. Consulte
as [instruções de migrations](docs/project/development.md#migrations-manuais) e o estado atual antes de iniciar serviços
ou executar migrations contra dados existentes.

### Variáveis de ambiente

| Variável                    | Uso                                                                               |
|-----------------------------|-----------------------------------------------------------------------------------|
| POSTGRES_USER               | Usuário do PostgreSQL local, fornecido ao Compose                                 |
| POSTGRES_PASSWORD           | Senha local, fornecida externamente; nunca versionar valores reais                |
| ASPNETCORE_ENVIRONMENT      | Ambiente da API; Development expõe OpenAPI/Scalar                                 |
| LOG_LEVEL                   | Nível Serilog no Compose, com fallback atual Verbose                              |
| LOAN_DURATION_DAYS | Prazo em dias no Compose; padrão7, positivo; alimenta Loans__DurationDays |
| Loans__DurationDays | Sobrescrita direta da configuração do prazo no host; padrão7 |
| ConnectionStrings__Postgres | Conexão usada pela API e ferramentas EF; o Compose monta a conexão para sua rede  |
| ConnectionStrings__Redis    | Conexão Redis fornecida à API pelo Compose; uso funcional do cache ainda pendente |

O inventário versionável está em [.env.example](.env.example). Para executar ferramentas no host, configurar a conexão
PostgreSQL externamente com localhost e a porta publicada 15432. Dentro do Compose, usar postgres:5432. O comando dotnet
ef não carrega .env automaticamente. Não imprimir conexão ou credenciais nos logs.

### Execução e migrations

Preparar o .env local com valores próprios a partir do exemplo e iniciar as dependências:

```powershell
docker compose up -d postgres redis
dotnet tool restore
```

Após o PostgreSQL estar pronto e ConnectionStrings__Postgres estar configurada no processo do host para o banco local
desejado, aplicar as migrations:

```powershell
dotnet ef database update --project src/BookStore.Infrastructure --startup-project src/BookStore.Api
```

Então iniciar a API em containers (porta publicada 15080) ou no host:

```powershell
docker compose up --build -d bookstore.api
```

```powershell
dotnet run --project src/BookStore.Api
```

No host, fornecer também o ambiente e as configurações necessárias por fontes externas; o ambiente do Compose não é
automaticamente herdado por dotnet run. Estes comandos descrevem o procedimento esperado; a sequência completa e as
migrations no PostgreSQL ainda precisam de verificação. Não executar contra banco existente sem identificar o destino e
obter autorização. O depends_on atual não aguarda prontidão das dependências.

## Testes

Testes automatizados foram adiados pelo autor para a etapa final. Os projetos e cenários de integração ainda estão
pendentes; executar dotnet test sem testes não comprova comportamento. Quando os projetos previstos estiverem
disponíveis, a execução será:

```powershell
dotnet test BookStore.sln -c Release
```

Os testes de integração usarão PostgreSQL/Redis descartáveis, sem banco de desenvolvimento ou credenciais do .env. Devem
cobrir especialmente disputa pelo último exemplar, idempotência, atomicidade da auditoria, invalidação de cache e
contratos HTTP. A matriz completa está no [plano](docs/codex/plans/2026-10-20-simplified-bookstore.md).

Os contratos de leitura usam records nomeados (BookAvailability e PagedResult<T>), separados dos modelos de resposta. Essa escolha melhora clareza e evolução sem alterar o JSON público; ver [decisões de arquitetura](docs/project/design.md).

## Decisões de consistência

As decisões abaixo estão aprovadas, mas os fluxos de empréstimo/cache e seus testes ainda não foram
implementados/verificados.

| Área         | Escolha e trade-off                                                                                                                                                                                                                                                                                                                                                                                                                   |
|--------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Concorrência | Controle pessimista no PostgreSQL: Read Committed, SELECT FOR UPDATE no Book e contagem de Loans ativos após adquirir o lock. Escritas seguem Book → Loan. Capacidade é Book.Quantity; não existe índice único por Loan.BookId para Active, pois mais de um empréstimo ativo é válido até essa capacidade. Todos os escritores precisam seguir o protocolo, com custo de serialização por título. |
| Idempotência | Idempotency-Key no Loan e comparação direta de BookId/UserId, sem hash; chave global única e retenção de sucessos. Mesma chave e mesmos dados recuperam a criação original; dados diferentes produzem conflito. Dispensa uma tabela genérica, mas acopla os metadados ao empréstimo e exige recuperação específica de corridas concorrentes.                                                                                                        |
| Cache        | Redis para catálogo, sem decidir disponibilidade de empréstimo. Cache-aside com geração de namespace e TTL; invalidar após commit. Capturar a geração antes da leitura do banco. Aceita consistência eventual e fallback ao banco; TTL limita dados antigos após falha de invalidação.                                                                                                                                                |
| Auditoria    | AuditEvent com AuditAction tipado (persistido como texto) no mesmo contexto/transação da operação, com ator declarado, UTC, correlação e detalhes JSON selecionados pela Application. Não substituir por logs ou serializar entidades indiscriminadamente. O histórico exige evolução cuidadosa do formato dos detalhes.                                                                                                                                                             |

Razões e alternativas estão no [desenho técnico](docs/project/design.md); transações e locks estão detalhados
no [guia transacional](docs/project/transactions.md).

## Limitações e evolução para produção

Simplificação aprovada, ainda em implementação: Book armazena Quantity e Loan referencia BookId; exemplares são indistintos, sem BookCopy, InventoryCode ou soft delete por unidade. Disponibilidade é Quantity menos empréstimos ativos para livro ativo. Redução só permite novo total maior ou igual aos empréstimos ativos, preservando todos os Loans. O contrato de empréstimo passa a receber bookId/userId. Ver [plano atual](docs/codex/plans/2026-10-20-simplified-bookstore.md).

- X-User-Id declara autoria; não autentica nem autoriza. Em produção, obter a identidade de autenticação confiável e
  aplicar autorização por operação/recurso.
- Migrations são manuais e precisam preceder tráfego/rollout. Evoluir para etapa coordenada de deployment, permissões
  separadas e mudanças de schema compatíveis com as versões em execução.
- Locks por título priorizam simplicidade e consistência. Medir contenção, duração de transações e deadlocks antes de
  aumentar a granularidade; a ordem uniforme não elimina todo risco de deadlock. Controle de versão/ETag para edições
  antigas é uma evolução distinta, ainda não adotada.
- Cache não oferece consistência forte com PostgreSQL. Avaliar mecanismo durável de invalidação se o requisito de
  atualização ficar mais rigoroso.
- A tradução genérica de violação de unicidade é um fallback; não identifica sozinha conflito de ISBN, falha de
  identificador interno ou replay idempotente. Os empréstimos precisarão dessa distinção.
- Testes funcionais/concorrentes, readiness/liveness completos, OpenTelemetry e Kubernetes ainda estão pendentes. Antes
  de produção, verificar esses contratos, carga real, segurança, backups/restauração, gestão de segredos e
  dimensionamento de conexões entre réplicas.
