# BookStore

API de biblioteca para o desafio de empréstimos concorrentes e auditáveis em .NET 10, com PostgreSQL e Redis.

## Situação

Projeto em construção: fundação e cadastro de livro com quantidade e auditoria presentes; BookCopy foi removido do código.
A consulta por ID, listagem paginada e a migration de remoção de BookCopy estão presentes. A infraestrutura de usuários
conta com entidade, mapping, repositório scoped e migration AddUsers gerada, ainda não aplicada nesta etapa.
O código de POST /users está presente: recebe name e retorna201 com id/name normalizado, sem Location e sem exigir X-User-Id, conforme contrato aprovado.
Devolução e cancelamento também estão no código, com204, ClosedAt e auditoria transacional; repetir a mesma ação não gera novo efeito. Catálogo e cache estão no código; a verificação funcional permanece pendente. O contrato de
respostas HTTP está aprovado; a centralização dos erros foi adiada pelo arquiteto. Consulte
o [estado atual](docs/project/status.md) para evidências e limites.

A base de empréstimos conta com Loan/LoanStatus, FKs restritivas, chave de idempotência única e migration AddLoans
gerada sem acesso ao banco. O código de POST /loans e GET /loans/{id} está presente, com lock de Book, contagem de ativos, auditoria e recuperação idempotente. HTTP, persistência e concorrência real ainda não foram exercitados nesta etapa.

A consulta GET /books/{id}/availability está no código e retorna quantity, activeLoans, availableQuantity e isActive, sem ator/cache/lock pessimista. Livro inativo tem disponibilidade0. É uma fotografia, não uma reserva; POST /loans revalida a capacidade na transação. Ver [contrato e decisões](docs/project/availability.md). Execução HTTP e PostgreSQL permanecem pendentes.

## Documentação

[Telemetria de empréstimos](docs/project/telemetry.md) implementada: contadores de criação pós-commit, rejeição por falta de quantidade e replay válido; duração HTTP e traces HTTP/PostgreSQL com SQL parametrizado, sem valores dos parâmetros. Tracing Redis dispensado; Serilog permanece como único pipeline de logs. Revisão independente e diagnósticos isolados realizados; PostgreSQL/receptor OTLP reais e testes formais seguem pendentes.

Configuração opcional de exportação, também repassada pelo Compose: `OTEL_SERVICE_NAME` (padrão `BookStore.Api`), `OTEL_EXPORTER_OTLP_ENDPOINT` (endpoint base; vazio desabilita exportação de rede) e `OTEL_EXPORTER_OTLP_PROTOCOL` (`http/protobuf` por padrão ou `grpc`). Não há Collector/dashboard no Compose; um destino compatível e acessível pela API é necessário para visualizar os sinais. `.env.example` contém apenas exemplos sanitizados. Contadores reiniciam com o processo e podem perder sinais; agregação entre réplicas pertence ao backend, sem substituir a auditoria transacional.

Health checks simplificados: `/health/live` e `/health` verificam só o processo; `/health/ready` verifica PostgreSQL e Redis com checks prontos. Resposta padrão em texto,200/503, sem X-User-Id. [Escolhas e limites](docs/project/health-checks.md). A API não aplica migrations no startup; prepare o schema antes de iniciá-la. Readiness confirma conectividade, não versão do schema. Exposição no Scalar e testes reais permanecem pendentes.

O tamanho padrão das consultas paginadas é configurado em `Pagination:DefaultPageSize` (valor inicial20 no appsettings.json), com override nativo `Pagination__DefaultPageSize` no processo da API. Configuração válida1..100; `pageSize` omitido usa esse valor e explícito prevalece após validação. O Compose repassa PAGINATION_DEFAULT_PAGE_SIZE para Pagination__DefaultPageSize. [Contrato e decisão](docs/project/pagination.md).

`GET /audit-events` consulta a auditoria com filtros opcionais e exatos por entityName, entityId e correlationId (AND), paginação1/20 máximo100 e ordem CreatedAt/Id desc. Details retorna como string JSON; Action é enum numérico. Sem X-User-Id. [Contrato e trade-offs](docs/project/audit-events.md). Migration AddAuditCorrelationIndex gerada offline, não aplicada; sem configuração nova. HTTP/PostgreSQL continuam pendentes.

`GET /users/{id}/loans` consulta empréstimos do usuário em todos os estados, inclusive de livros desativados, com o mesmo formato paginado e status textual do histórico por livro. Usuário inexistente404; sem empréstimos200 vazio; sem X-User-Id. Paginação1/20, máximo100, ordem LoanedAt/Id desc. [Contrato e decisões](docs/project/loan-history.md#histórico-por-usuário). Sem nova configuração/migration; HTTP e PostgreSQL permanecem pendentes.

`GET /books/{id}/history` consulta empréstimos ativos, devolvidos e cancelados do livro, inclusive desativado, com paginação e TotalCount. Ordena por LoanedAt/Id decrescentes e retorna status textual; não exige X-User-Id. Livro inexistente404, histórico vazio200. [Contrato e trade-offs](docs/project/loan-history.md). Sem nova configuração/migration; validação HTTP/PostgreSQL ainda pendente.

`DELETE /books/{id}` desativa o título com X-User-Id obrigatório e retorna204. Preserva quantidade, empréstimos e histórico; grava a transição e auditoria na mesma transação sob lock do livro. Repetir não gera outro evento. Novos empréstimos são rejeitados para inativos; devolução/cancelamento dos existentes continuam permitidos. Ver [contrato de desativação](docs/project/books.md#desativação--contrato-e-razão). Sem nova configuração ou migration; testes HTTP/PostgreSQL permanecem pendentes.

O código de `PATCH /books/{id}` atualiza título, autor e quantidade, com X-User-Id obrigatório. Campos ausentes são preservados; null explícito e corpo sem campos editáveis são rejeitados400. Quantidade abaixo dos empréstimos ativos gera409. Lock do livro protege a alteração; Book e auditoria Before/After são gravados atomicamente. Repetir valores iguais retorna204 sem novo evento. Ver [contrato, decisões e limites](docs/project/books.md). HTTP e concorrência real permanecem pendentes para a etapa final.

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

Dockerfile e Compose preservam API/PostgreSQL/Redis, com publicação multi-stage e runtime não root. Migrations são uma etapa explícita antes de iniciar a API: o startup não cria/atualiza o schema. A imagem foi construída e inspecionada; a sequência completa contra serviços reais continua pendente. Consulte as [instruções de migrations](docs/project/development.md#migrations-manuais) e o estado atual antes de executar alterações no banco.

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
| ConnectionStrings__Redis    | Conexão Redis fornecida à API pelo Compose; cache do catálogo implementado; execução real ainda pendente |

O inventário versionável está em [.env.example](.env.example). Para executar ferramentas no host, configurar a conexão
PostgreSQL externamente com localhost e a porta publicada 15432. Dentro do Compose, usar postgres:5432. O comando dotnet
ef não carrega .env automaticamente. Não imprimir conexão ou credenciais nos logs.

### Execução e migrations

Preparar o .env local com valores próprios a partir do exemplo e iniciar as dependências:

```powershell
docker compose up -d --wait postgres redis
dotnet tool restore
```

O manifesto local fixa dotnet-ef10.0.13; dotnet tool restore restaura essa versão sem alterar ferramentas globais. Após as dependências estarem prontas, configure ConnectionStrings__Postgres no processo do host para o banco desejado e ConnectionStrings__Redis para a configuração da aplicação. Use localhost:15432 para PostgreSQL a partir do host; a conexão Redis do host precisa de endereço acessível, pois redis:6379 é DNS interno do Compose. Não exponha valores reais no histórico/README. Então aplique as migrations:

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
obter autorização. O depends_on da API aguarda os healthchecks PostgreSQL/Redis no startup. Isso não reinicia a API por falhas posteriores nem comprova credenciais/schema. Em múltiplas réplicas, aplicar migrations antes do rollout é responsabilidade do operador; não existe migrador ou Job automático neste bloco.

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

## Kubernetes

Manifests em [deploy/k8s](deploy/k8s): Deployment com 2 réplicas, Service ClusterIP 80 → 8080, ConfigMap e referências obrigatórias ao Secret externo bookstore-api-connections. Requests iniciais 100m/128Mi e limits 500m/256Mi, sem dimensionamento por carga ainda. Probes usam /health/live e /health/ready existentes; o container está configurado para executar como não root.

Antes de aplicar, escolha um namespace existente, forneça o Secret com keys ConnectionStrings__Postgres e ConnectionStrings__Redis, prepare o schema por migrations explícitas e substitua a imagem de exemplo bookstore.api:case por uma referência disponível nos nodes. Não há PostgreSQL/Redis, Job de migration, HPA ou cluster incluído nos manifests. Connection strings reais não pertencem ao YAML, ConfigMap ou Git. [Procedimento completo](docs/project/kubernetes.md#procedimento-para-o-operador).

Os comandos abaixo são instruções para um ambiente autorizado, não foram executados nesta entrega:

```powershell
kubectl --namespace bookstore apply -f deploy/k8s
kubectl --namespace bookstore rollout status deployment/bookstore-api
kubectl --namespace bookstore port-forward service/bookstore-api 15080:80
```

O namespace bookstore é exemplo e deve existir com o Secret antes de aplicar. Mudanças de ConfigMap/Secret como env exigem recriar/rollout dos Pods. A referência de imagem precisa identificar a versão implantada; construir localmente não garante disponibilidade nos nodes.

**Por que o desenho preserva integridade entre 2 e 11 réplicas:** todas usam o mesmo PostgreSQL; lock de Book serializa decisões de capacidade e a contagem de ativos ocorre na transação; chave de idempotência única e recuperação de corrida evitam duplicação; Loan e auditoria confirmam juntos. Redis compartilhado mantém catálogo/geração de invalidação e não decide disponibilidade. Métricas são por processo, agregadas no backend. Nenhuma dessas garantias depende de lock ou sessão na memória de um Pod.

Isso não comprova throughput ou alta disponibilidade das dependências: pools/conexões, contenção, memória/CPU e réplicas precisam de testes. Não há HPA; scale manual é uma opção operacional, e aplicar novamente o manifesto pode restaurar replicas:2. Schema validado localmente com kubeconform 0.8.0/Kubernetes 1.35.0: 3 recursos válidos, nenhum erro/ignorado; revisão independente sem bloqueios. Nenhum cluster foi provisionado ou acessado, conforme o escopo do exercício.

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
- Verificações funcionais/concorrentes, probes e exportação de telemetria contra serviços reais ainda estão pendentes. Os manifests Kubernetes foram validados por schema, sem execução em cluster. Antes
  de produção, verificar esses contratos, carga real, segurança, backups/restauração, gestão de segredos e
  dimensionamento de conexões entre réplicas.

A projeção do histórico reutiliza LoanResponse por escolha do autor; LoanHistoryResponse preserva o status textual do novo endpoint. Ver [decisão e trade-off](docs/project/loan-history.md).

## Cache Redis do catálogo

Usamos StackExchange.Redis diretamente porque a inicialização da geração exige SET NX, ausente no contrato IDistributedCache. RedisConnectionProvider administra a conexão compartilhada; essa escolha acrescenta código de lifecycle a manter. [Alternativas e trade-offs](docs/project/catalog-cache.md#decisão-stackexchangeredis-direto-versus-idistributedcache).

GET /books usa cache-aside: Redis primeiro; miss consulta PostgreSQL e armazena a página com TTL. A chave inclui geração, página e pageSize efetivo. Criação, alteração e desativação efetivas invalidam após commit; no-op e empréstimos não invalidam essa representação sem disponibilidade dinâmica. PostgreSQL continua decidindo capacidade/concorrência/idempotência.

| Compose | Configuração nativa da API | Valor inicial |
|---|---|---|
| CATALOG_CACHE_TTL_SECONDS | CatalogCache__TimeToLiveSeconds | 60 segundos |
| CATALOG_CACHE_OPERATION_TIMEOUT_MS | CatalogCache__OperationTimeoutMilliseconds | 200 milissegundos por método do cache |
| PAGINATION_DEFAULT_PAGE_SIZE | Pagination__DefaultPageSize | 20 |

Opções do cache positivas e ConnectionStrings__Redis presente são necessárias; o Compose já fornece a conexão por sua configuração. Falha operacional do Redis causa fallback ao banco e warning sem dados sensíveis. Invalidação pós-commit tem timeout próprio, independente do cancelamento da chamada, e falha de Redis não desfaz negócio confirmado. Geração capturada antes da leitura evita que preenchimento atrasado republique dados antigos no namespace novo.

Não há atomicidade PostgreSQL/Redis: janela de invalidação e falhas podem servir dados antigos; TTL limita residência desde publicação, sem garantia estrita a partir do commit. Produção pode exigir invalidação durável/outbox e proteção de stampede. [Fluxo, decisões, limites e evidências](docs/project/catalog-cache.md). Build/Compose e diagnósticos passaram; Redis/PostgreSQL reais, HTTP, expiração e concorrência ainda não foram exercitados. Testes automatizados permanecem para a etapa final.
