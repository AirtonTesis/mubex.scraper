# Implementation Plan: SaaS de Extração de Dados do Google Search

## Overview

Este plano de implementação detalha a construção de uma plataforma SaaS completa para extração de dados do Google Search, utilizando uma arquitetura distribuída moderna com:

- **Backend**: .NET Core 8 com CQRS, Vertical Slice Architecture, auto-validação de domínio (Map/Ensure)
- **Frontend**: Angular com design Glassmorphism e sidebar expansível por hover
- **Workers**: Componentes assíncronos de scraping usando Playwright/Selenium com técnicas stealth

A implementação segue uma abordagem incremental, construindo camadas de infraestrutura, domínio, aplicação, apresentação e workers de forma modular e testável.

## Tasks

- [x] 1. Configurar estrutura base do projeto e dependências
  - Criar solução .NET com projetos: WebApi, Domain, Infrastructure, Workers
  - Criar projeto Angular com módulos core, shared e features
  - Configurar Entity Framework Core com PostgreSQL
  - Configurar MediatR para CQRS
  - Configurar sistema de filas (RabbitMQ ou Azure Service Bus)
  - Criar arquivo de configuração de i18n (pt-BR.json)
  - _Requirements: 9.1, 11.1_

- [x] 2. Implementar camada de domínio com auto-validação
  - [x] 2.1 Criar BaseEntity com Id, CreatedAt e UpdatedAt
    - Implementar classe abstrata BaseEntity
    - Adicionar método Touch() para atualizar UpdatedAt
    - _Requirements: 8.2, 12.1, 12.2_

  - [x]* 2.2 Escrever testes de propriedade para BaseEntity
    - **Property 24: Entity Creation Sets Timestamps**
    - **Valida: Requirements 12.3, 12.4**

  - [x] 2.3 Criar interfaces e classes de validação (IMapEnsure, ValidationResult, ValidationKey)
    - Implementar interface IMapEnsure com métodos Map e Ensure
    - Implementar ValidationResult com Success/Failure
    - Implementar ValidationKey com padrão de nomenclatura estruturado
    - Criar métodos auxiliares Required, MaxLength, MinLength
    - _Requirements: 8.3, 8.4, 8.5_

  - [x]* 2.4 Escrever testes de propriedade para ValidationKey
    - **Property 20: Validation Key Format Compliance**
    - **Valida: Requirements 8.5**

  - [x] 2.5 Criar entidade User
    - Implementar User estendendo BaseEntity
    - Adicionar propriedades: Email, PasswordHash
    - _Requirements: 1.1_

  - [x] 2.6 Criar entidade SearchList com validação Map/Ensure
    - Implementar SearchList estendendo BaseEntity
    - Adicionar propriedades: Name, Keywords (List), Domains (List), UserId
    - Implementar método estático Create com validação
    - Implementar Map para validação estrutural
    - Implementar Ensure para regras de negócio
    - Implementar método Update
    - _Requirements: 3.2, 3.3, 8.3_

  - [x]* 2.7 Escrever testes de propriedade para SearchList
    - **Property 5: SearchList Validation via Map and Ensure**
    - **Property 6: Validation Failures Return Structured Keys**
    - **Valida: Requirements 3.2, 3.3, 8.4**

  - [x] 2.8 Criar entidade Job com máquina de estados
    - Implementar enum JobStatus (Pending, Active, Paused, Completed, Failed)
    - Implementar Job estendendo BaseEntity
    - Adicionar propriedades: SearchListId, Status, StartedAt, CompletedAt, RetryCount, ErrorMessage
    - Implementar métodos de transição: Start, Pause, Resume, Complete, Fail
    - Implementar AddHistoryEntry para rastreamento de mudanças
    - _Requirements: 4.3, 4.4, 4.5, 4.6, 4.7_

  - [x]* 2.9 Escrever testes de propriedade para Job
    - **Property 10: Job State Transitions Follow State Machine**
    - **Valida: Requirements 4.5, 4.7**

  - [x] 2.10 Criar entidade JobHistoryEntry
    - Implementar JobHistoryEntry com Id, JobId, Status, Timestamp
    - _Requirements: 4.8_

- [x] 3. Checkpoint - Validar camada de domínio
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 4. Implementar camada de infraestrutura (Persistência e Mensageria)
  - [-] 4.1 Configurar ApplicationDbContext com EF Core
    - Implementar DbContext com DbSets (Users, SearchLists, Jobs, JobHistoryEntries)
    - Override SaveChangesAsync para atualizar timestamps automaticamente
    - _Requirements: 12.3, 12.4, 12.5, 12.6_

  - [ ]* 4.2 Escrever testes de propriedade para timestamps automáticos
    - **Property 24: Entity Creation Sets Timestamps**
    - **Property 25: Entity Update Modifies UpdatedAt Only**
    - **Valida: Requirements 12.3, 12.4, 12.5, 12.6**

  - [ ] 4.3 Criar configurações de entidades EF Core
    - Implementar UserConfiguration
    - Implementar SearchListConfiguration com conversão JSON para Keywords/Domains
    - Implementar JobConfiguration com conversão de enum e relacionamentos
    - Criar índices: UserId, CreatedAt, Status
    - _Requirements: 9.5, 11.1_

  - [-] 4.4 Criar interface e implementação IQueueManager
    - Definir interface IQueueManager com EnqueueJobAsync, DequeueJobAsync, UpdateJobStatusAsync
    - Implementar RabbitMqQueueManager (ou Azure Service Bus)
    - Configurar declaração de fila com durabilidade
    - _Requirements: 4.1, 4.2_

  - [ ]* 4.5 Escrever testes de integração para QueueManager
    - Testar enfileiramento e desenfileiramento
    - Testar atualização de status de jobs
    - _Requirements: 4.2, 4.3_

  - [ ] 4.6 Criar serviço JWT (JwtTokenService)
    - Implementar interface IJwtTokenService
    - Implementar GenerateToken com claims (NameIdentifier, Email, Jti)
    - Implementar ValidateToken com validação de assinatura e expiração
    - Configurar algoritmo HmacSha256
    - _Requirements: 1.1, 1.2, 1.5_

  - [ ]* 4.7 Escrever testes de propriedade para JWT
    - **Property 1: JWT Token Generation with Expiration**
    - **Property 3: Token Validation Enforces Signature and Expiration**
    - **Valida: Requirements 1.2, 1.5**

- [ ] 5. Checkpoint - Validar camada de infraestrutura
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 6. Implementar camada de aplicação CQRS (Commands e Queries)
  - [ ] 6.1 Criar comando e handler: CreateSearchListCommand
    - Implementar CreateSearchListCommand com Name, Keywords, Domains, UserId
    - Implementar CreateSearchListHandler usando SearchList.Create
    - Retornar Result<Guid> com validação de erros
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

  - [ ]* 6.2 Escrever testes de propriedade para CreateSearchList
    - **Property 7: SearchList Persistence Round-Trip**
    - **Valida: Requirements 3.4**

  - [ ] 6.3 Criar comando e handler: UpdateSearchListCommand
    - Implementar UpdateSearchListCommand com Id, Name, Keywords, Domains
    - Implementar UpdateSearchListHandler com busca e atualização
    - _Requirements: 3.6_

  - [ ] 6.4 Criar comando e handler: DeleteSearchListCommand
    - Implementar DeleteSearchListCommand com Id
    - Implementar DeleteSearchListHandler com remoção
    - _Requirements: 3.7, 3.8_

  - [ ]* 6.5 Escrever testes de propriedade para DeleteSearchList
    - **Property 8: SearchList Deletion Removes Entity**
    - **Valida: Requirements 3.8**

  - [ ] 6.6 Criar query e handler: GetAllSearchListsQuery
    - Implementar GetAllSearchListsQuery com UserId
    - Implementar GetAllSearchListsHandler com AsNoTracking
    - Retornar List<SearchListDto>
    - _Requirements: 3.5, 9.5_

  - [ ]* 6.7 Escrever testes de propriedade para read queries
    - **Property 22: Read Queries Have No Side Effects**
    - **Valida: Requirements 9.5**

  - [ ] 6.8 Criar comando e handler: EnqueueJobCommand
    - Implementar EnqueueJobCommand com SearchListId
    - Implementar EnqueueJobHandler criando Job e enfileirando via IQueueManager
    - Retornar imediatamente sem aguardar processamento
    - _Requirements: 4.1, 4.2_

  - [ ]* 6.9 Escrever testes de propriedade para EnqueueJob
    - **Property 9: Job Enqueue is Non-Blocking**
    - **Valida: Requirements 4.2**

  - [ ] 6.10 Criar comando e handler: PauseJobCommand
    - Implementar PauseJobCommand com JobId
    - Implementar PauseJobHandler chamando Job.Pause
    - Atualizar status via IQueueManager
    - _Requirements: 4.4, 4.5_

  - [ ] 6.11 Criar comando e handler: ActivateJobCommand
    - Implementar ActivateJobCommand com JobId
    - Implementar ActivateJobHandler chamando Job.Resume
    - Atualizar status via IQueueManager
    - _Requirements: 4.6, 4.7_

  - [ ] 6.12 Criar query e handler: GetJobHistoryQuery
    - Implementar GetJobHistoryQuery com JobId
    - Implementar GetJobHistoryHandler retornando histórico ordenado cronologicamente
    - Usar AsNoTracking
    - _Requirements: 4.8, 4.10_

  - [ ]* 6.13 Escrever testes de propriedade para job history
    - **Property 11: Job History Chronological Ordering**
    - **Valida: Requirements 4.10**

  - [ ] 6.14 Criar query e handler: GetDashboardMetricsQuery
    - Implementar GetDashboardMetricsQuery com StartDate, EndDate
    - Implementar GetDashboardMetricsHandler calculando métricas
    - Calcular: TotalSearches, SuccessRate, FailureRate, ActiveExecutions
    - Usar AsNoTracking para performance
    - _Requirements: 7.1, 7.8, 7.9_

  - [ ]* 6.15 Escrever testes de propriedade para dashboard metrics
    - **Property 17: Dashboard Metrics Calculation Accuracy**
    - **Property 18: Custom Date Range Filtering**
    - **Valida: Requirements 7.1, 7.7**

  - [ ] 6.16 Criar comando e handler: LoginCommand
    - Implementar LoginCommand com Email, Password
    - Implementar LoginCommandHandler validando credenciais
    - Retornar JWT token em caso de sucesso
    - Retornar ValidationKey em caso de falha
    - _Requirements: 1.2, 1.3_

  - [ ]* 6.17 Escrever testes de propriedade para Login
    - **Property 2: Invalid Credentials Return Validation Keys**
    - **Valida: Requirements 1.3**

- [ ] 7. Checkpoint - Validar camada de aplicação CQRS
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 8. Implementar controllers da API (Vertical Slices)
  - [ ] 8.1 Criar AuthController com endpoint Login
    - Implementar POST /api/auth/login
    - Enviar LoginCommand via MediatR
    - Retornar token JWT ou 400 com ValidationKeys
    - _Requirements: 1.2, 1.3_

  - [ ] 8.2 Criar SearchListsController com CRUD endpoints
    - Implementar POST /api/searchlists (Create)
    - Implementar GET /api/searchlists (GetAll)
    - Implementar PUT /api/searchlists/{id} (Update)
    - Implementar DELETE /api/searchlists/{id} (Delete)
    - Adicionar atributo [Authorize] para proteção
    - Retornar ValidationProblemDetails em caso de erro
    - _Requirements: 3.1, 3.5, 3.6, 3.7_

  - [ ] 8.3 Criar JobsController com endpoints de gerenciamento
    - Implementar POST /api/jobs (Enqueue)
    - Implementar PATCH /api/jobs/{id}/pause (Pause)
    - Implementar PATCH /api/jobs/{id}/activate (Activate)
    - Implementar GET /api/jobs/{id}/history (GetHistory)
    - Adicionar atributo [Authorize]
    - _Requirements: 4.1, 4.4, 4.6, 4.8_

  - [ ] 8.4 Criar DashboardController com endpoint de métricas
    - Implementar GET /api/dashboard/metrics com query params startDate, endDate
    - Enviar GetDashboardMetricsQuery via MediatR
    - Adicionar atributo [Authorize]
    - _Requirements: 7.1_

  - [ ]* 8.5 Escrever testes de integração para controllers
    - Testar autenticação JWT em endpoints protegidos
    - Testar retorno de status codes corretos (200, 201, 400, 401, 404)
    - _Requirements: 1.4, 13.3, 13.4, 13.5_

- [ ] 9. Implementar tratamento de erros global e logging
  - [ ] 9.1 Criar GlobalExceptionHandler
    - Implementar IExceptionHandler
    - Logar exceções com correlationId, stack trace, path
    - Retornar ProblemDetails com status 500
    - _Requirements: 13.1_

  - [ ]* 9.2 Escrever testes de propriedade para error handling
    - **Property 26: Exception Logging Includes Context**
    - **Property 28: Error Responses Use Appropriate HTTP Status Codes**
    - **Valida: Requirements 13.1, 13.3, 13.4, 13.5**

  - [ ] 9.3 Criar CorrelationIdMiddleware
    - Extrair ou gerar X-Correlation-ID
    - Adicionar ao HttpContext.TraceIdentifier
    - Adicionar ao response header
    - Configurar LogContext para incluir correlationId
    - _Requirements: 13.7_

  - [ ]* 9.4 Escrever testes de propriedade para correlation ID
    - **Property 29: Correlation ID Propagation Across Components**
    - **Valida: Requirements 13.7**

  - [ ] 9.5 Configurar Serilog para logging estruturado
    - Configurar console e file sinks
    - Adicionar enrichers (Application, CorrelationId)
    - Configurar níveis de log
    - _Requirements: 13.1, 13.2_

- [ ] 10. Checkpoint - Validar backend API completo
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 11. Implementar Workers de Scraping
  - [ ] 11.1 Criar serviços de suporte (Proxy, User-Agent, CAPTCHA Detection)
    - Implementar IProxyRotationService e ProxyRotationService
    - Implementar IUserAgentRotationService e UserAgentRotationService
    - Implementar ICaptchaDetectionService e CaptchaDetectionService
    - _Requirements: 5.5, 5.6, 6.1_

  - [ ]* 11.2 Escrever testes de propriedade para rotation services
    - **Property 13: User-Agent Rotation Between Requests**
    - **Property 14: Proxy Rotation Between Requests**
    - **Valida: Requirements 5.5, 5.6**

  - [ ] 11.3 Criar IScrapingEngine e PlaywrightScrapingEngine
    - Implementar interface IScrapingEngine com ExecuteAsync
    - Implementar PlaywrightScrapingEngine com lógica de scraping
    - Configurar Playwright com proxy e user-agent rotativo
    - Adicionar stealth scripts (remover navigator.webdriver)
    - Implementar emulação de comportamento humano (delays, movimentos)
    - Implementar detecção de CAPTCHA
    - Implementar lógica de retry com rotação de proxy
    - Extrair resultados de busca e filtrar por domínios alvo
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 6.2, 6.3, 6.4_

  - [ ]* 11.4 Escrever testes de propriedade para scraping engine
    - **Property 12: Scraping Emulation Includes Randomization**
    - **Property 15: Retry Mechanism Respects Maximum Attempts**
    - **Property 16: Retry Uses Different Proxy**
    - **Valida: Requirements 5.3, 6.3, 6.4**

  - [ ] 11.5 Criar ScrapingWorkerService
    - Implementar BackgroundService
    - Implementar loop de processamento com DequeueJobAsync
    - Atualizar status do job (Active, Completed, Failed)
    - Chamar IScrapingEngine.ExecuteAsync
    - Logar erros detalhados com proxy e razão de falha
    - Implementar delay em caso de erro
    - _Requirements: 4.2, 10.1, 10.4, 13.2_

  - [ ]* 11.6 Escrever testes de propriedade para worker
    - **Property 23: Distributed Job Processing Without Duplication**
    - **Property 27: Failed Job Logging Includes Details**
    - **Valida: Requirements 10.4, 13.2**

- [ ] 12. Checkpoint - Validar workers de scraping
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 13. Implementar Frontend Angular
  - [ ] 13.1 Configurar módulo Core (Auth, API, i18n)
    - Criar AuthService com login e JWT storage
    - Criar AuthGuard para proteção de rotas
    - Criar JwtInterceptor para adicionar token em requests
    - Criar ApiService para comunicação HTTP
    - Criar TranslationService para carregar pt-BR.json
    - Criar ValidationKeyPipe para traduzir chaves de validação
    - _Requirements: 1.1, 8.6, 11.1, 11.3_

  - [ ]* 13.2 Escrever testes unitários para serviços Core
    - Testar AuthService login e token storage
    - Testar JwtInterceptor adiciona token ao header
    - _Requirements: 1.1_

  - [ ] 13.3 Criar arquivo de tradução pt-BR.json
    - Adicionar traduções para todas as ValidationKeys
    - Incluir mensagens de erro, labels, botões
    - _Requirements: 8.6, 11.3, 11.4_

  - [ ]* 13.4 Escrever testes de propriedade para i18n
    - **Property 21: Validation Key Translation to PT-BR**
    - **Valida: Requirements 8.6, 11.3**

  - [ ] 13.4 Implementar módulo Shared (Sidebar, ErrorDisplay)
    - Criar SidebarComponent com expansão via hover
    - Implementar lógica de expand/collapse com @HostListener
    - Criar estilos SCSS com Glassmorphism
    - Criar ErrorDisplayComponent para exibir ValidationKeys traduzidas
    - _Requirements: 2.2, 2.3, 2.4, 2.5, 3.3_

  - [ ]* 13.5 Escrever testes unitários para Sidebar
    - **Property 4: Sidebar Content Adjustment on Expansion**
    - **Valida: Requirements 2.4**

  - [ ] 13.6 Criar base de estilos Glassmorphism (SCSS)
    - Criar mixin glass-card com backdrop-filter
    - Criar classes utilitárias (.glass-container, .glass-card)
    - Definir variáveis de cores e opacidades
    - _Requirements: 2.1_

  - [ ] 13.7 Implementar DashboardModule
    - Criar DashboardComponent com visualização de métricas
    - Criar TemporalFilterComponent com opções Day, Week, Month, Custom
    - Criar MetricCardComponent para exibir estatísticas
    - Implementar lógica de seleção de filtros
    - Implementar lógica de Custom Range com date pickers
    - Conectar DashboardService com backend API
    - Aplicar estilos Glassmorphism
    - _Requirements: 3.1, 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7_

  - [ ]* 13.8 Escrever testes de integração para Dashboard
    - Testar filtros temporais retornam dados corretos
    - Testar cálculo de métricas
    - _Requirements: 7.3, 7.4, 7.5, 7.7_

  - [ ] 13.9 Implementar SearchListsModule
    - Criar SearchListsComponent com listagem
    - Criar CreateEditSearchListComponent com formulário
    - Implementar validação de formulário no frontend
    - Exibir erros traduzidos usando ValidationKeyPipe
    - Conectar SearchListsService com backend API
    - Aplicar estilos Glassmorphism
    - _Requirements: 3.1, 3.3, 3.5, 3.6, 3.7_

  - [ ] 13.10 Implementar JobsModule
    - Criar JobQueueComponent com listagem de jobs
    - Criar controles para pause/activate/delete
    - Criar JobHistoryComponent com histórico cronológico
    - Conectar JobsService com backend API
    - Aplicar estilos Glassmorphism
    - _Requirements: 4.9, 4.10_

- [ ] 14. Checkpoint final - Validar sistema completo
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 15. Configurar infraestrutura e deployment
  - [ ] 15.1 Criar migrations do Entity Framework
    - Gerar migration inicial com todas as entidades
    - Aplicar migration ao banco de dados
    - _Requirements: 12.1_

  - [ ] 15.2 Criar arquivos de configuração (appsettings.json)
    - Configurar connection strings (PostgreSQL, RabbitMQ)
    - Configurar JWT settings (Secret, Issuer, Audience, ExpirationMinutes)
    - Configurar lista de proxies
    - Configurar Serilog
    - _Requirements: 1.1, 5.6_

  - [ ] 15.3 Criar Dockerfiles e docker-compose
    - Criar Dockerfile para WebApi
    - Criar Dockerfile para Workers
    - Criar Dockerfile para Angular (nginx)
    - Criar docker-compose.yml com PostgreSQL, RabbitMQ, Redis
    - _Requirements: 10.1_

  - [ ] 15.4 Criar scripts de seed inicial
    - Criar usuário de teste
    - Criar SearchList de exemplo
    - _Requirements: 1.1_

- [ ] 16. Checkpoint final - Sistema pronto para uso
  - Ensure all tests pass, ask the user if questions arise.

## 17. Estabilizar o fluxo de CAPTCHA e confirmação de resultados

> **Escopo de segurança:** este épico não inclui técnicas para burlar CAPTCHA, spoofing de fingerprint, mascaramento de automação, rotação de proxies para evasão ou simulação de comportamento humano com esse objetivo. O foco é diagnóstico, controle de estado, encerramento seguro de loops, observabilidade e uso de fontes autorizadas.

- [ ] 17.1 Definir estados explícitos do fluxo de verificação
  - Criar um modelo de estado para separar `ChallengeDetected`, `AnswerSubmitted`, `VerificationPending`, `VerificationAccepted`, `ResultsLoaded`, `RepeatedChallenge`, `Blocked` e `Failed`.
  - Definir transições válidas e transições terminais.
  - Garantir que cada transição registre estado anterior, estado novo, motivo e timestamp UTC.
  - **Critérios de aceite:** nenhuma execução pode permanecer em loop sem limite; `RepeatedChallenge` e `Blocked` devem ser estados distintos de erro técnico.
  - **Dependências:** nenhuma.
  - **Arquivos relacionados:** `src/Infrastructure/Scraping/ScrapingModels.cs`, `src/Infrastructure/Scraping/PlaywrightScrapingEngine.cs`.

- [ ] 17.2 Criar um resultado estruturado para o ciclo de CAPTCHA
  - Substituir o retorno booleano usado no fluxo de resolução por um resultado tipado contendo estado final, quantidade de rounds, duração, erro categorizado e evidências de navegação.
  - Preservar a compatibilidade com `ScrapingResult` e `SearchResultData`.
  - **Critérios de aceite:** o chamador consegue distinguir `VerificationAccepted`, `RepeatedChallenge`, `VerificationExpired`, `WebhookInvalid`, `NavigationTimeout` e `Blocked` sem interpretar texto de log.
  - **Dependências:** 17.1.
  - **Arquivos relacionados:** `src/Infrastructure/Scraping/ScrapingModels.cs`, `src/Infrastructure/Scraping/PlaywrightScrapingEngine.cs`.

- [ ] 17.3 Separar detecção de challenge, bloqueio e resultados
  - Refatorar `ICaptchaDetectionService` para expor responsabilidades distintas, evitando tratar a simples presença de iframe ou `div.g-recaptcha` como prova suficiente de bloqueio.
  - Criar verificações independentes para challenge visível, página de bloqueio, resultados carregados e página legítima sem resultados.
  - **Critérios de aceite:** a presença residual de um iframe não pode, sozinha, transformar uma página com resultados válidos em falha; cada classificação deve indicar o sinal que a motivou.
  - **Dependências:** 17.1.
  - **Arquivos relacionados:** `src/Infrastructure/Scraping/CaptchaDetectionService.cs`, `src/Infrastructure/Scraping/PlaywrightScrapingEngine.cs`.

- [ ] 17.4 Implementar confirmação pós-verificação baseada na navegação
  - Após a ação de verificação, aguardar e classificar o estado final usando URL, conteúdo, frame, carregamento de resultados e estado legítimo sem resultados.
  - Não considerar apenas o desaparecimento do grid como confirmação de sucesso.
  - **Critérios de aceite:** o sistema só classifica como `VerificationAccepted` quando a navegação posterior é consistente; se surgir outro challenge, classifica como `RepeatedChallenge`.
  - **Dependências:** 17.2 e 17.3.
  - **Arquivos relacionados:** `src/Infrastructure/Scraping/PlaywrightScrapingEngine.cs`, `src/Infrastructure/Scraping/CaptchaDetectionService.cs`.

- [ ] 17.5 Limitar rounds, reloads e tentativas por execução
  - Centralizar limites configuráveis para rounds de challenge, reloads, tempo total de verificação e tentativas por keyword.
  - Definir um orçamento global por keyword e por job, contabilizando loops aninhados (`MaxRetries`, rounds e reloads) para impedir multiplicação inadvertida de tentativas.
  - Remover caminhos que fazem retentativas indefinidas ou repetem a mesma sessão após `RepeatedChallenge`/`Blocked`.
  - **Critérios de aceite:** ao atingir qualquer limite, o job termina com categoria determinística e não executa nova interação de UI; os limites aparecem na configuração e nos logs; o número total de tentativas nunca excede o orçamento global.
  - **Dependências:** 17.1 e 17.2.
  - **Arquivos relacionados:** `src/Infrastructure/Scraping/PlaywrightScrapingEngine.cs`, `src/WebApi/appsettings.json`, `src/Workers/appsettings.json`.

- [ ] 17.6 Adicionar telemetria estruturada do ciclo completo
  - Registrar, sem armazenar imagens, tokens ou credenciais: `execution_id`, `keyword`, `round`, `grid_size`, latência do webhook, quantidade de células selecionadas, clique de verificação, estado pós-verificação, URL sanitizada, quantidade de resultados e duração total.
  - Proibir payload JSON bruto do webhook, base64 de imagens, tokens, cookies, credenciais e parâmetros sensíveis de URL nos logs de produção.
  - Implementar redaction centralizado e teste específico para confirmar que os campos sensíveis não são emitidos.
  - Categorizar falhas do webhook, parsing, expiração, navegação, challenge repetido e bloqueio.
  - **Critérios de aceite:** uma execução pode ser reconstruída a partir dos logs; dados sensíveis são redigidos; métricas permitem calcular taxas por estado final; nenhum log de produção contém imagens, payloads brutos ou tokens.
  - **Dependências:** 17.2.
  - **Arquivos relacionados:** `src/Infrastructure/Scraping/PlaywrightScrapingEngine.cs`, `src/Infrastructure/Scraping/CaptchaDetectionService.cs`.

- [ ] 17.7 Criar testes unitários para classificação e transições
  - Testar todas as transições válidas e inválidas do modelo de estado.
  - Testar challenge aceito com resultados, challenge aceito sem resultados, challenge repetido, bloqueio, expiração, webhook inválido e timeout.
  - Testar que a presença de iframe residual não é suficiente para classificar uma página como bloqueada.
  - **Critérios de aceite:** os testes são determinísticos e cobrem os critérios de aceite das tarefas 17.1 a 17.4.
  - **Dependências:** 17.1 a 17.4.
  - **Arquivos relacionados:** `tests/Infrastructure.Tests`, novos testes do scraper e dos serviços de detecção.

- [ ] 17.8 Criar testes de integração do webhook n8n com contrato estável
  - Usar um cliente HTTP substituível ou servidor fake para testar respostas válidas, listas com tamanho incorreto, JSON inválido, resposta vazia, HTTP 4xx/5xx e timeout.
  - Não depender do endpoint n8n real nos testes automatizados.
  - **Critérios de aceite:** cada falha produz uma categoria de erro específica e não gera loop silencioso.
  - **Dependências:** 17.2 e 17.6.
  - **Arquivos relacionados:** `src/Infrastructure/Scraping/PlaywrightScrapingEngine.cs`, `tests/Infrastructure.Tests`.

- [ ] 17.9 Adicionar circuit breaker operacional para bloqueios repetidos
  - Criar um mecanismo por domínio/ambiente que interrompa novas execuções após uma quantidade configurável de `RepeatedChallenge` ou `Blocked` em janela de tempo definida.
  - Expor o estado do circuito nos logs e permitir reset explícito por configuração/operação.
  - **Critérios de aceite:** com o circuito aberto, novas execuções são encerradas antes de iniciar nova interação; o motivo é visível e o circuito volta ao estado fechado conforme a política configurada.
  - **Dependências:** 17.1, 17.5 e 17.6.
  - **Arquivos relacionados:** `src/Infrastructure/Scraping`, `src/WebApi/appsettings.json`, `src/Workers/appsettings.json`.

- [ ] 17.10 Definir política de fallback autorizado
  - Documentar quando uma execução deve ser desviada para uma fonte autorizada de resultados, como API oficial aplicável ou provedor contratado.
  - Definir contrato comum para mapear resultados externos para `SearchResultData`.
  - Definir o roteamento efetivo no orquestrador: `RepeatedChallenge`/`Blocked` deve encerrar a sessão atual e, quando habilitado, acionar a engine autorizada sem iniciar novas interações de UI na mesma sessão.
  - Registrar no resultado final qual fonte foi utilizada e se houve coleta parcial antes do fallback.
  - **Critérios de aceite:** o job não depende de resolver repetidamente um challenge para entregar resultado; quotas, erros e indisponibilidade do provedor são tratados explicitamente; o fallback não é apenas documentado, mas acionado por uma transição testável.
  - **Dependências:** 17.2, 17.5 e decisão do provedor.
  - **Arquivos relacionados:** `src/Infrastructure/Scraping/IScrapingEngine.cs`, `src/Infrastructure/Scraping/ScrapingModels.cs`, `src/Infrastructure/Scraping/PlaywrightScrapingEngine.cs`.

- [ ] 17.11 Implementar engine de resultados autorizado como fallback
  - Criar uma implementação de `IScrapingEngine` para o provedor aprovado após definição de credenciais, quota, limites e termos de uso.
  - Mapear paginação, domínio, posição, título, URL e snippet conforme o contrato disponível.
  - **Critérios de aceite:** respostas válidas são convertidas para o modelo interno; erros de autenticação, quota e indisponibilidade produzem estados operacionais claros; credenciais não ficam no código.
  - **Dependências:** 17.10 e escolha formal do provedor.
  - **Arquivos relacionados:** `src/Infrastructure/Scraping`, `src/WebApi/appsettings.json`, `src/Workers/appsettings.json`.

- [ ] 17.12 Persistir o resultado do ciclo de verificação
  - Decidir e documentar quais campos serão persistidos em `Job`/`JobHistoryEntry`: estado final, categoria de erro, engine utilizada, quantidade de rounds e duração.
  - Adicionar configurações EF, DTOs e migrations somente para dados operacionais necessários, sem imagens, tokens ou payloads sensíveis.
  - **Critérios de aceite:** o estado exibido após reinício da API é consistente com o estado final registrado; o schema não armazena artefatos sensíveis do CAPTCHA.
  - **Dependências:** 17.1, 17.2 e 17.6.
  - **Arquivos relacionados:** `src/Domain/Entities/Job.cs`, `src/Domain/Entities/JobHistoryEntry.cs`, `src/Infrastructure/Persistence`, `src/WebApi/Features/Jobs`.

- [ ] 17.13 Atualizar dashboard e histórico de jobs com estados de CAPTCHA
  - Exibir estados finais e motivos sem expor detalhes sensíveis.
  - Diferenciar falha técnica, challenge repetido, bloqueio, fallback utilizado e sucesso com/sem resultados.
  - **Critérios de aceite:** o usuário consegue identificar por que um job não prosseguiu e qual fonte produziu os dados.
  - **Dependências:** 17.2, 17.6, 17.10 e 17.12.
  - **Arquivos relacionados:** `src/WebApi/Features/Jobs`, `src/WebApi/Features/Dashboard`, `frontend/src/app/features/jobs`, `frontend/src/app/features/dashboard`.

- [ ] 17.14 Criar runbook de operação e critérios de parada
  - Documentar interpretação dos estados, limites, circuit breaker, coleta de evidências, redaction de dados, procedimento de reset e acionamento do fallback.
  - Definir que `RepeatedChallenge`/`Blocked` encerra a execução atual sem novas tentativas automáticas na mesma sessão.
  - **Critérios de aceite:** outra pessoa consegue operar e diagnosticar o pipeline sem acessar imagens, tokens ou dados sensíveis.
  - **Dependências:** 17.5, 17.6 e 17.9.
  - **Arquivos relacionados:** `docs/RELATORIO-CAPTCHA-SOLVING.md`, novo runbook em `docs/`.

- [ ] 17.15 Marcar tarefas legadas fora do escopo desta prioridade
  - Revisar as tarefas 11.1 a 11.4 e 15.2 que mencionam stealth, proxy rotation, spoofing ou simulação de comportamento para evasão.
  - Marcar essas tarefas como fora de escopo/deprecadas para este épico, evitando que o backlog misture estabilização legítima com bypass de controles anti-automação.
  - **Critérios de aceite:** o plano de execução deixa explícito que a prioridade atual não inclui evasão; tarefas de infraestrutura que sejam necessárias por motivos legítimos permanecem separadas e justificadas.
  - **Dependências:** nenhuma.
  - **Arquivos relacionados:** `.kiro/specs/google-search-scraper-saas/tasks.md`, `.kiro/specs/google-search-scraper-saas/requirements.md`.

- [ ] 17.16 Checkpoint - Validar o fluxo estabilizado
  - Executar testes backend completos.
  - Executar testes específicos de Infrastructure e WebApi.
  - Validar build do frontend após instalação das dependências.
  - Confirmar que o workspace não grava imagens, tokens ou credenciais em logs de produção.
  - **Dependências:** 17.7 a 17.15.

### Dependências sugeridas do épico 17

```text
17.1 ─┬─> 17.2 ─┬─> 17.4 ─┬─> 17.7
      │         ├─> 17.6 ├─> 17.8
      └─> 17.3 ─┘        ├─> 17.9 ─> 17.13
                         └─> 17.10 ─> 17.11 ─> 17.12
17.5 depende de 17.1 + 17.2
17.12 depende de 17.1 + 17.2 + 17.6
17.13 depende de 17.2 + 17.6 + 17.10 + 17.12
17.14 depende de 17.5 + 17.6 + 17.9
17.16 depende de todas as tasks anteriores
```

## Notes

- Tarefas marcadas com `*` são opcionais e podem ser puladas para um MVP mais rápido
- Cada tarefa referencia requirements específicos para rastreabilidade
- Checkpoints garantem validação incremental do progresso
- Testes de propriedade validam propriedades de correção universais definidas no design
- Testes unitários e de integração validam exemplos específicos e casos de borda
- A arquitetura modular permite desenvolvimento paralelo de Backend, Frontend e Workers
- O sistema usa .NET Core 8 para backend/workers e Angular para frontend
- Playwright é a biblioteca primária de scraping, com Selenium como fallback

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1"] },
    { "id": 1, "tasks": ["2.1", "2.3"] },
    { "id": 2, "tasks": ["2.2", "2.4", "2.5"] },
    { "id": 3, "tasks": ["2.6", "2.8", "2.10"] },
    { "id": 4, "tasks": ["2.7", "2.9"] },
    { "id": 5, "tasks": ["3"] },
    { "id": 6, "tasks": ["4.1", "4.4", "4.6"] },
    { "id": 7, "tasks": ["4.2", "4.3", "4.5", "4.7"] },
    { "id": 8, "tasks": ["5"] },
    { "id": 9, "tasks": ["6.1", "6.3", "6.4", "6.6", "6.8", "6.10", "6.11", "6.12", "6.14", "6.16"] },
    { "id": 10, "tasks": ["6.2", "6.5", "6.7", "6.9", "6.13", "6.15", "6.17"] },
    { "id": 11, "tasks": ["7"] },
    { "id": 12, "tasks": ["8.1", "8.2", "8.3", "8.4"] },
    { "id": 13, "tasks": ["8.5", "9.1", "9.3", "9.5"] },
    { "id": 14, "tasks": ["9.2", "9.4"] },
    { "id": 15, "tasks": ["10"] },
    { "id": 16, "tasks": ["11.1"] },
    { "id": 17, "tasks": ["11.2", "11.3"] },
    { "id": 18, "tasks": ["11.4", "11.5"] },
    { "id": 19, "tasks": ["11.6"] },
    { "id": 20, "tasks": ["12"] },
    { "id": 21, "tasks": ["13.1", "13.3", "13.6"] },
    { "id": 22, "tasks": ["13.2", "13.4", "13.5"] },
    { "id": 23, "tasks": ["13.7", "13.9", "13.10"] },
    { "id": 24, "tasks": ["13.8"] },
    { "id": 25, "tasks": ["14"] },
    { "id": 26, "tasks": ["15.1", "15.2", "15.4"] },
    { "id": 27, "tasks": ["15.3"] },
    { "id": 28, "tasks": ["16"] }
  ]
}
```
