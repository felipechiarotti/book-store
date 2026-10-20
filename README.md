# BookStore

API de biblioteca para o desafio de empréstimos concorrentes e auditáveis em .NET 10, com PostgreSQL e Redis.

## Situação

Projeto em construção: fundação parcialmente implementada pelo autor, incluindo DI, validação, tratamento de erros, correlação, Serilog e adaptadores transacionais. Catálogo e empréstimos ainda não estão implementados; testes e verificação funcional permanecem pendentes. Consulte o [estado atual](docs/project/status.md) para evidências e limites.

## Documentação

| Leitor | Entrada | Conteúdo |
|---|---|---|
| Avaliação do case | [Guia do case](docs/case/README.md) | Enunciado, síntese das decisões, atendimento ao desafio e uso de IA |
| Desenvolvimento e manutenção | [Documentação técnica](docs/project/README.md) | Arquitetura, regras, contratos, transações, correlação e ambiente local |
| Agentes Codex | [AGENTS.md](AGENTS.md) | Instruções do repositório e navegação do processo assistido |

O [índice de documentação](docs/README.md) define a responsabilidade de cada área. O [enunciado original](docs/desafio-candidato-api-emprestimos-dotnet.md) é a fonte principal dos requisitos.

## Compilação

Na raiz, com SDK .NET 10:

```powershell
dotnet restore BookStore.sln
dotnet build BookStore.sln -c Release --no-restore
```

Build não comprova integridade transacional, concorrência, startup ou contratos HTTP. Preparação e limitações de execução estão no [guia de desenvolvimento](docs/project/development.md).

## Configuração local

Não versionar .env, credenciais ou dados sensíveis. Usar .env.example somente com exemplos sanitizados. O .env está ignorado; revisar o índice antes de commits autorizados. O hook de proteção preparado anteriormente está ausente no último checkpoint, portanto não presumir que bloqueio adicional está ativo.

Os arquivos Compose e Dockerfile fazem parte da base local. Consulte o estado atual antes de iniciar serviços ou executar migrations contra dados existentes.
