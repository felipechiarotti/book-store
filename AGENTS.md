# BookStore — instruções de engenharia

## Fontes e escopo

- Navegação: [case](docs/case/README.md) para avaliação; [documentação técnica](docs/project/README.md) para desenvolvimento/manutenção; [guia dos agentes](docs/codex/README.md) para processo. Não misturar roteiros de estudo, contratos do sistema e instruções de interação.
- Ler e seguir [engineering-workflow.md](docs/codex/engineering-workflow.md) antes de executar ou revisar uma etapa.
- A fonte principal dos requisitos é [docs/desafio-candidato-api-emprestimos-dotnet.md](docs/desafio-candidato-api-emprestimos-dotnet.md). Preservar o enunciado original.
- Consultar o [desenho aprovado](docs/project/design.md), o [plano por blocos](docs/codex/plans/2026-10-20-simplified-bookstore.md) e os [diagramas](docs/project/architecture.md).
- Gringotts é referência de organização e conceitos. Não importar suas regras financeiras, seu workflow ou comandos de validação para este projeto.
- O usuário é o arquiteto principal. Propor soluções com razões e trade-offs; não iniciar funcionalidades ou expandir escopo por conta própria. Aprovações dadas permanecem válidas no respectivo escopo.

## Checkout e alterações

- **Não usar ou criar worktrees para este projeto. Trabalhar no checkout e na branch atuais para que o usuário acompanhe as alterações em tempo real.**
- Não trocar/criar branch sem solicitação. A branch pode mudar por ação do usuário; conferir o estado atual em vez de presumir sempre main.
- Preservar alterações locais e staged do usuário. Não descartar, sobrescrever, reformatar globalmente ou reorganizar trabalho alheio.
- Sem commit, push, merge, deploy, exclusão de branches ou alteração do índice Git sem autorização correspondente.
- Não ler/expor segredos do .env ou usar banco existente para testes. Usar infraestrutura descartável; migrations em banco existente precisam de autorização explícita.
- Nunca adicionar, forçar inclusão ou commitar arquivos .env. Antes de qualquer commit autorizado, conferir que `git ls-files --cached -- .env ':(glob)**/.env'` não retorna caminhos; caso contrário, bloquear o commit e retirar somente esses arquivos do índice, preservando o conteúdo local. Apenas .env.example sanitizado pode ser versionado. Não contornar o hook com --no-verify ou desabilitá-lo.

## Planejamento e implementação

- Aplicar skills disponíveis ao escopo real. Usar Superpowers quando aplicável.
- Features, refatorações e mudanças de comportamento: brainstorming, desenho aprovado e plano antes de código. Bugs: systematic-debugging, reprodução, causa, proposta e plano; TDD para regressão.
- Planos de execução em docs/codex/plans/ e desenho do sistema em docs/project/design.md. Preservar a preferência por execução acompanhada pelo usuário, sem propor execução autônoma a cada etapa.
- Dividir trabalho em funcionalidades ou blocos correlacionados. Preparar configurações/dependências necessárias antes do comportamento; implementar testes e auditoria junto da funcionalidade.
- Preferir a menor solução que cumpra o exercício. Não criar abstrações especulativas, endpoints ou integrações não necessários.
- Preferência posterior expressa do usuário: deixar criação/execução dos testes automatizados para a etapa final. Essa decisão substitui a ordem TDD deste plano durante a construção atual, sem eliminar cenários de teste ou o gate final. Continuar revisão e checks de build/configuração pertinentes, registrar verificação parcial e nunca alegar RED/GREEN retroativo.

## C#, arquitetura e qualidade humana

- .NET 10 e C# 14, com recursos modernos compatíveis. Evitar previews e LangVersion=latest; escolher sintaxe pela clareza, expressividade e segurança, não por novidade.
- Onion nas dependências, Vertical Slice nas funcionalidades, módulos Minimal API e padrões aplicáveis do Gringotts.
- Nomes expressivos em inglês, métodos coesos, responsabilidades claras, dependências explícitas e organização consistente. Comentários explicam razões e invariantes.
- Entidades com comportamento e factories; records para DTOs/mensagens quando adequados. Domain sem EF/HTTP; Application sem HttpContext.
- Nullable habilitado, async/await, propagação de CancellationToken, lifetimes corretos e descarte de recursos. Evitar .Result/.Wait, supressões nullable e catch genérico que esconda defeitos.
- Revisar N+1, tracking, projeções, consultas limitadas, enumeração repetida, índices, transações e concorrência EF/PostgreSQL. Cache não decide disponibilidade.
- Decisão de logging: Serilog no host Api, console JSON, ILogger<T> nas camadas consumidoras. Não usar Serilog estático/LogContext nos handlers ou Domain, não duplicar logs com outro provider/exportador. Preservar correlationId em escopos, resumo HTTP e erros; TraceId/SpanId continuam próprios de OpenTelemetry. Logs não substituem auditoria. Conferir a ordem dos middlewares e evitar payloads/segredos em logs conforme desenho/plano.

## Após o usuário concluir uma implementação

- Tratar mensagens como "terminei", "implementei" ou "pode revisar" como pedido para executar o workflow de revisão/verificação do bloco afetado.
- Começar informando concisamente o fluxo que será aplicado: identificar escopo/diff, comparar com plano, revisar código/segurança, verificar comportamento e documentação, relatar evidências e pendências.
- Revisar o código do usuário sem reescrevê-lo ou corrigir produção automaticamente. Apresentar achados e propostas; executar correções somente quando autorizadas.
- Para implementação significativa, usar requesting-code-review e preferir revisão independente. Para gate final .NET, ler e usar a skill local [dotnet-final-validation](.agents/skills/dotnet-final-validation/SKILL.md) junto de verification-before-completion. A cópia do projeto é a referência para este gate; não depender de caminhos globais nem duplicar suas instruções aqui. A skill não se aplica a revisões somente de leitura ou alterações apenas documentais.
- Não criar aprovação retroativa ou evidência fictícia de TDD. Se não há evidência de RED/GREEN, declarar a limitação.
- O relatório deve distinguir fatos verificados, hipóteses, riscos, falhas e checks não executados. Não afirmar conclusão sem evidência atual.

## Documentação

- Distribuição aprovada: docs/case/ contém síntese da entrega e uso de IA; docs/project/ contém referência técnica canônica para equipe, razões e trade-offs; docs/codex/ contém workflow, planos e evidências dos agentes.
- Material pessoal de estudo/apresentação fica fora do repositório. O arquivo local de metadados Git info/bookstore-study-path, quando existir, informa sua localização. Não versionar esse material, copiar seu conteúdo para documentos públicos ou criar links públicos para ele. Verificar que o destino resolvido é externo ao repositório antes de atualizá-lo. Sua ausência em outro clone não bloqueia o trabalho público.
- Sempre contextualizar decisões relevantes: na documentação técnica, problema, papel na aplicação, escolha, alternativas, contrato, consequências e evidências; no estudo pessoal disponível, explicação aprofundada, cenário real, relação com o fluxo maior e perguntas/respostas de apresentação. A referência técnica tem prioridade sobre o guia pessoal.
- Atualizar README, .env.example quando houver configuração nova/alterada, decisões e diagramas junto da implementação.
- O README principal deve sempre cobrir: pré-requisitos e comandos de execução; execução de migrations e testes; variáveis de ambiente sem segredos; decisões e trade-offs de concorrência, idempotência, cache e auditoria; limitações conhecidas e evolução para produção. Distinguir desenho aprovado, implementação existente e comportamento verificado; não apresentar testes inexistentes ou comandos não exercitados como evidência de sucesso.
- Documentar motivo, alternativas e trade-offs de decisões relevantes, incluindo custos de desempenho, segurança e operação.
- Manter diagramas Mermaid e explicações para o domínio de system design e apresentação do case. Diferenciar desenho proposto de comportamento implementado/verificado.
- Documentação isolada exige checks de links, contratos, consistência e diff, não build/testes da aplicação sem necessidade.
