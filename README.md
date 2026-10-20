# BookStore

API de biblioteca para o desafio de empréstimos concorrentes e auditáveis em .NET 10, com PostgreSQL e Redis.

## Situação

Projeto em construção: fundação, modelo inicial do catálogo, migrations e handler de cadastro com exemplares/auditoria presentes. Endpoints do catálogo, empréstimos, cache e verificação funcional ainda não estão concluídos. A padronização de respostas HTTP está aprovada e em adaptação. Consulte o [estado atual](docs/project/status.md) para evidências e limites.

## Documentação

| Leitor | Entrada | Conteúdo |
|---|---|---|
| Avaliação do case | [Guia do case](docs/case/README.md) | Enunciado, síntese das decisões, atendimento ao desafio e uso de IA |
| Desenvolvimento e manutenção | [Documentação técnica](docs/project/README.md) | Arquitetura, regras, contratos, transações, correlação e ambiente local |
| Agentes Codex | [AGENTS.md](AGENTS.md) | Instruções do repositório e navegação do processo assistido |

O [índice de documentação](docs/README.md) define a responsabilidade de cada área. O [enunciado original](docs/desafio-candidato-api-emprestimos-dotnet.md) é a fonte principal dos requisitos.

## Compilação

Pré-requisitos: SDK .NET 10 e Docker com Compose para executar PostgreSQL 16, Redis 8 e a API em containers. Não é necessário instalar PostgreSQL/Redis no host nem provisionar um cluster Kubernetes para o ambiente local.

Na raiz, com SDK .NET 10:

```powershell
dotnet restore BookStore.sln
dotnet build BookStore.sln -c Release --no-restore
```

Build não comprova integridade transacional, concorrência, startup ou contratos HTTP. Preparação e limitações de execução estão no [guia de desenvolvimento](docs/project/development.md).

## Configuração local

Não versionar .env, credenciais ou dados sensíveis. Usar .env.example somente com exemplos sanitizados. O .env está ignorado; revisar o índice antes de commits autorizados. O hook de proteção preparado anteriormente está ausente no último checkpoint, portanto não presumir que bloqueio adicional está ativo.

Os arquivos Compose e Dockerfile fazem parte da base local. As migrations são aplicadas manualmente antes de iniciar a API; subir a aplicação não atualiza o schema. Consulte as [instruções de migrations](docs/project/development.md#migrations-manuais) e o estado atual antes de iniciar serviços ou executar migrations contra dados existentes.

### Variáveis de ambiente

| Variável | Uso |
|---|---|
| POSTGRES_USER | Usuário do PostgreSQL local, fornecido ao Compose |
| POSTGRES_PASSWORD | Senha local, fornecida externamente; nunca versionar valores reais |
| ASPNETCORE_ENVIRONMENT | Ambiente da API; Development expõe OpenAPI/Scalar |
| LOG_LEVEL | Nível Serilog no Compose, com fallback atual Verbose |
| ConnectionStrings__Postgres | Conexão usada pela API e ferramentas EF; o Compose monta a conexão para sua rede |
| ConnectionStrings__Redis | Conexão Redis fornecida à API pelo Compose; uso funcional do cache ainda pendente |

O inventário versionável está em [.env.example](.env.example). Para executar ferramentas no host, configurar a conexão PostgreSQL externamente com localhost e a porta publicada 15432. Dentro do Compose, usar postgres:5432. O comando dotnet ef não carrega .env automaticamente. Não imprimir conexão ou credenciais nos logs.

### Execução e migrations

Preparar o .env local com valores próprios a partir do exemplo e iniciar as dependências:

```powershell
docker compose up -d postgres redis
dotnet tool restore
```

Após o PostgreSQL estar pronto e ConnectionStrings__Postgres estar configurada no processo do host para o banco local desejado, aplicar as migrations:

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

No host, fornecer também o ambiente e as configurações necessárias por fontes externas; o ambiente do Compose não é automaticamente herdado por dotnet run. Estes comandos descrevem o procedimento esperado; a sequência completa e as migrations no PostgreSQL ainda precisam de verificação. Não executar contra banco existente sem identificar o destino e obter autorização. O depends_on atual não aguarda prontidão das dependências.

## Testes

Testes automatizados foram adiados pelo autor para a etapa final. Os projetos e cenários de integração ainda estão pendentes; executar dotnet test sem testes não comprova comportamento. Quando os projetos previstos estiverem disponíveis, a execução será:

```powershell
dotnet test BookStore.sln -c Release
```

Os testes de integração usarão PostgreSQL/Redis descartáveis, sem banco de desenvolvimento ou credenciais do .env. Devem cobrir especialmente disputa pelo último exemplar, idempotência, atomicidade da auditoria, invalidação de cache e contratos HTTP. A matriz completa está no [plano](docs/codex/plans/2026-10-20-bookstore-implementation.md).

## Decisões de consistência

As decisões abaixo estão aprovadas, mas os fluxos de empréstimo/cache e seus testes ainda não foram implementados/verificados.

| Área | Escolha e trade-off |
|---|---|
| Concorrência | Controle pessimista no PostgreSQL: transações Read Committed, locks SELECT FOR UPDATE na ordem Book → BookCopy → Loan e revalidação após aquisição. Escritas no mesmo título são serializadas, inclusive em exemplares diferentes. Índice único parcial em Loan.BookCopyId para empréstimo Active como proteção adicional. Evita dependência de memória local entre réplicas, com custo de espera e necessidade de transações curtas. |
| Idempotência | Idempotency-Key e hash dos parâmetros de negócio no Loan; chave global única e retenção de sucessos. Mesma chave e mesmos dados recuperam a criação original; dados diferentes produzem conflito. Dispensa uma tabela genérica, mas acopla os metadados ao empréstimo e exige recuperação específica de corridas concorrentes. |
| Cache | Redis para catálogo, sem decidir disponibilidade de empréstimo. Cache-aside com geração de namespace e TTL; invalidar após commit. Capturar a geração antes da leitura do banco. Aceita consistência eventual e fallback ao banco; TTL limita dados antigos após falha de invalidação. |
| Auditoria | AuditEvent no mesmo contexto/transação da operação, com ator declarado, UTC, correlação e detalhes JSON selecionados pela Application. Não substituir por logs ou serializar entidades indiscriminadamente. O histórico exige evolução cuidadosa do formato dos detalhes. |

Razões e alternativas estão no [desenho técnico](docs/project/design.md); transações e locks estão detalhados no [guia transacional](docs/project/transactions.md).

## Limitações e evolução para produção

- X-User-Id declara autoria; não autentica nem autoriza. Em produção, obter a identidade de autenticação confiável e aplicar autorização por operação/recurso.
- Migrations são manuais e precisam preceder tráfego/rollout. Evoluir para etapa coordenada de deployment, permissões separadas e mudanças de schema compatíveis com as versões em execução.
- Locks por título priorizam simplicidade e consistência. Medir contenção, duração de transações e deadlocks antes de aumentar a granularidade; a ordem uniforme não elimina todo risco de deadlock. Controle de versão/ETag para edições antigas é uma evolução distinta, ainda não adotada.
- Cache não oferece consistência forte com PostgreSQL. Avaliar mecanismo durável de invalidação se o requisito de atualização ficar mais rigoroso.
- A tradução genérica de violação de unicidade é um fallback; não identifica sozinha conflito de ISBN, falha de identificador interno ou replay idempotente. Os empréstimos precisarão dessa distinção.
- Testes funcionais/concorrentes, readiness/liveness completos, OpenTelemetry e Kubernetes ainda estão pendentes. Antes de produção, verificar esses contratos, carga real, segurança, backups/restauração, gestão de segredos e dimensionamento de conexões entre réplicas.
