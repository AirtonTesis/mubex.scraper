# Requirements Document

## Introduction

O **SaaS de Extração de Dados do Google Search** é uma plataforma web que automatiza a coleta e monitoramento de resultados de buscas em larga escala. O sistema combina um backend robusto em .NET Core (utilizando CQRS e Vertical Slice Architecture) com um frontend moderno em Angular (design Glassmorphism), além de workers especializados em scraping furtivo utilizando Playwright e Selenium. A plataforma permite que profissionais de SEO, analistas de dados e empresas de tecnologia extraiam, monitorem e analisem dados públicos do Google Search de forma escalável e segura.

## Glossary

- **System**: O sistema completo SaaS de Extração de Dados do Google Search, incluindo frontend, backend API e workers de scraping
- **Backend_API**: Camada de serviços REST em .NET Core responsável por gerenciar comandos, consultas, autenticação e orquestração
- **Frontend_App**: Aplicação Angular responsável pela interface do usuário, exibição de dados e interação
- **Scraping_Worker**: Componente de background responsável por executar jobs de extração utilizando Playwright ou Selenium
- **Queue_Manager**: Subsistema responsável por gerenciar filas de jobs de scraping
- **Dashboard_Module**: Módulo do frontend que exibe métricas analíticas e permite filtragem temporal
- **Search_List**: Entidade de domínio que contém listas de palavras-chave e domínios a serem monitorados
- **Job**: Tarefa de scraping agendada ou em execução que processa uma Search_List
- **User**: Usuário autenticado na plataforma (profissional de SEO, analista ou administrador)
- **Validation_Key**: Chave de tradução estruturada retornada pelo backend para identificar erros de validação (ex: `validation.search_list.name_required`)
- **Proxy_Rotation**: Mecanismo de alternância automática de proxies para evitar bloqueios
- **CAPTCHA_Detection**: Capacidade do sistema de detectar quando o Google apresenta um desafio CAPTCHA
- **Retry_Mechanism**: Sistema que reexecuta tentativas de scraping com diferentes proxies ao detectar bloqueio
- **Glassmorphism**: Estilo visual de interface que utiliza efeitos translúcidos semelhantes a vidro

## Requirements

### Requirement 1: Autenticação e Autorização de Usuários

**User Story:** Como um User, eu quero realizar login seguro na plataforma para acessar funcionalidades protegidas e gerenciar minhas listas de busca

#### Acceptance Criteria

1. THE Backend_API SHALL implement token-based authentication using JWT
2. WHEN a User submits valid credentials, THE Backend_API SHALL generate a JWT token with expiration
3. WHEN a User submits invalid credentials, THE Backend_API SHALL return an authentication error with Validation_Key
4. WHEN a User accesses a protected endpoint without valid token, THE Backend_API SHALL return HTTP 401 Unauthorized
5. THE Backend_API SHALL validate JWT token signature and expiration on every protected request

### Requirement 2: Interface Glassmorphism com Sidebar Interativa

**User Story:** Como um User, eu quero navegar em uma interface moderna e intuitiva com sidebar expansível para maximizar minha área de trabalho enquanto mantenho acesso rápido ao menu

#### Acceptance Criteria

1. THE Frontend_App SHALL render all interface components using Glassmorphism visual style with translucent effects
2. THE Frontend_App SHALL display a sidebar in collapsed state by default
3. WHEN a User hovers over the collapsed sidebar, THE Frontend_App SHALL expand the sidebar with smooth animation
4. WHEN the sidebar expands, THE Frontend_App SHALL adjust the main content area positioning automatically
5. WHEN a User moves cursor away from expanded sidebar, THE Frontend_App SHALL collapse the sidebar with smooth animation

### Requirement 3: CRUD Completo de Search Lists

**User Story:** Como um User, eu quero criar, editar, visualizar e excluir Search_Lists para organizar minhas palavras-chave e domínios a serem monitorados

#### Acceptance Criteria

1. THE Frontend_App SHALL provide a user interface to create a new Search_List with name, keywords and domains
2. WHEN a User submits a create Search_List request, THE Backend_API SHALL validate the entity using Map and Ensure functions
3. IF validation fails, THEN THE Backend_API SHALL return structured Validation_Key identifiers for frontend translation
4. WHEN validation succeeds, THE Backend_API SHALL persist the Search_List using CQRS command pattern
5. THE Frontend_App SHALL provide a user interface to view all existing Search_Lists
6. THE Frontend_App SHALL provide a user interface to edit an existing Search_List
7. THE Frontend_App SHALL provide a user interface to delete a Search_List
8. WHEN a User deletes a Search_List, THE Backend_API SHALL remove the entity and return success confirmation

### Requirement 4: Sistema de Filas para Jobs de Scraping

**User Story:** Como um User, eu quero gerenciar jobs de scraping através de um sistema de filas para controlar execuções, pausar, ativar e visualizar histórico de processamento

#### Acceptance Criteria

1. THE Backend_API SHALL provide an asynchronous endpoint to enqueue a scraping Job
2. WHEN a User creates a Job, THE Queue_Manager SHALL add the Job to the processing queue without blocking HTTP response
3. THE Queue_Manager SHALL maintain Job status including pending, active, paused, completed and failed states
4. THE Backend_API SHALL provide an endpoint to pause an active Job
5. WHEN a User pauses a Job, THE Queue_Manager SHALL update Job status to paused and stop processing
6. THE Backend_API SHALL provide an endpoint to activate a paused Job
7. WHEN a User activates a paused Job, THE Queue_Manager SHALL update Job status and resume processing
8. THE Backend_API SHALL provide an endpoint to retrieve Job execution history with timestamps and status transitions
9. THE Frontend_App SHALL display Job queue management interface with create, pause, activate and delete actions
10. THE Frontend_App SHALL display Job execution history with chronological list of status changes

### Requirement 5: Motor de Scraping com Playwright e Selenium

**User Story:** Como um Scraping_Worker, eu quero executar extração de dados do Google Search utilizando navegação automatizada furtiva para evitar detecção e bloqueios

#### Acceptance Criteria

1. THE Scraping_Worker SHALL use Playwright as primary automation library for scraping tasks
2. WHERE advanced fingerprinting scenarios are required, THE Scraping_Worker SHALL use Selenium as fallback
3. WHEN executing a scraping task, THE Scraping_Worker SHALL emulate human interactions with random cursor movements and delays
4. THE Scraping_Worker SHALL mask automation flags by removing navigator.webdriver property
5. THE Scraping_Worker SHALL rotate User-Agent headers for each scraping request
6. THE Scraping_Worker SHALL support dynamic Proxy_Rotation to distribute requests across different IP addresses

### Requirement 6: Retry Automático com Detecção de Bloqueio

**User Story:** Como um Scraping_Worker, eu quero detectar automaticamente bloqueios ou CAPTCHAs do Google e retentar a extração usando proxies diferentes para maximizar taxa de sucesso

#### Acceptance Criteria

1. WHEN a scraping attempt is made, THE Scraping_Worker SHALL monitor for CAPTCHA_Detection indicators
2. IF CAPTCHA_Detection occurs, THEN THE Scraping_Worker SHALL trigger Retry_Mechanism
3. THE Retry_Mechanism SHALL attempt up to 3 retries for each failed scraping task
4. WHEN retrying, THE Retry_Mechanism SHALL select a different proxy from the available pool
5. IF all 3 retry attempts fail, THEN THE Scraping_Worker SHALL mark the Job as failed and log detailed error information
6. WHEN a retry succeeds, THE Scraping_Worker SHALL mark the Job as completed and proceed with data extraction

### Requirement 7: Dashboard Analítico com Filtros Temporais

**User Story:** Como um User, eu quero visualizar métricas de extração no dashboard com filtros temporais dinâmicos para analisar desempenho em diferentes períodos

#### Acceptance Criteria

1. THE Dashboard_Module SHALL display metrics including total searches, success rate, failure rate and active executions
2. THE Dashboard_Module SHALL provide temporal filter options: Day, Week, Month and Custom Range
3. WHEN a User selects Day filter, THE Dashboard_Module SHALL display metrics for the last 24 hours
4. WHEN a User selects Week filter, THE Dashboard_Module SHALL display metrics for the last 7 days
5. WHEN a User selects Month filter, THE Dashboard_Module SHALL display metrics for the last 30 days
6. WHEN a User selects Custom Range filter, THE Dashboard_Module SHALL prompt for start date and end date
7. WHEN a User applies Custom Range, THE Dashboard_Module SHALL display metrics for the specified date range
8. THE Backend_API SHALL provide optimized read queries for dashboard metrics using CQRS query pattern
9. WHEN querying dashboard data, THE Backend_API SHALL use EF Core queries without change tracking for performance optimization

### Requirement 8: Auto-Validação de Entidades com Chaves de Tradução

**User Story:** Como um Backend_API developer, eu quero que entidades de domínio se auto-validem utilizando Map e Ensure retornando Validation_Keys para suportar internacionalização no frontend

#### Acceptance Criteria

1. THE Backend_API SHALL implement a BaseEntity abstract class with Id, CreatedAt and UpdatedAt properties
2. THE Backend_API SHALL implement domain entities extending BaseEntity
3. WHEN creating or updating a domain entity, THE Backend_API SHALL invoke Map and Ensure validation functions
4. IF domain validation fails, THEN THE Backend_API SHALL return structured Validation_Key identifiers instead of hardcoded text
5. THE Validation_Key SHALL follow naming pattern: `validation.{entity_name}.{field_name}_{error_type}`
6. THE Frontend_App SHALL translate Validation_Key to PT-BR user-friendly messages
7. WHERE internationalization is required in future, THE System SHALL support additional languages using the same Validation_Key structure

### Requirement 9: Arquitetura CQRS e Vertical Slice

**User Story:** Como um Backend_API developer, eu quero implementar CQRS com Vertical Slice Architecture para separar operações de leitura e escrita mantendo alta coesão por feature

#### Acceptance Criteria

1. THE Backend_API SHALL organize code using Vertical Slice Architecture where each feature encapsulates endpoints, commands, queries and validators
2. THE Backend_API SHALL implement CQRS pattern separating write operations using commands from read operations using queries
3. WHEN processing a write operation, THE Backend_API SHALL use MediatR command handlers
4. WHEN processing a read operation, THE Backend_API SHALL use MediatR query handlers
5. THE Backend_API SHALL optimize query handlers for read performance without persistence side effects

### Requirement 10: Escalabilidade Horizontal dos Workers

**User Story:** Como um system administrator, eu quero escalar workers de scraping horizontalmente de forma independente da API para aumentar capacidade de processamento sem afetar outros componentes

#### Acceptance Criteria

1. THE System SHALL implement decoupled architecture where Scraping_Worker instances are independent from Backend_API
2. THE Queue_Manager SHALL support multiple concurrent Scraping_Worker instances consuming from the same queue
3. WHEN workload increases, THE System SHALL allow deployment of additional Scraping_Worker instances without Backend_API changes
4. THE Scraping_Worker SHALL retrieve jobs from Queue_Manager in distributed manner without job duplication
5. THE Scraping_Worker SHALL report job status updates to Backend_API via asynchronous communication

### Requirement 11: Suporte a Internacionalização (i18n)

**User Story:** Como um User, eu quero que a interface seja exibida em PT-BR no MVP com estrutura preparada para suportar múltiplos idiomas no futuro

#### Acceptance Criteria

1. THE Frontend_App SHALL display all user interface text in PT-BR language
2. THE Frontend_App SHALL implement i18n framework structure to support future language additions
3. THE Frontend_App SHALL translate all Validation_Key messages received from Backend_API to PT-BR
4. THE Frontend_App SHALL store translation strings in structured JSON files organized by language code
5. WHERE additional languages are required in future, THE Frontend_App SHALL support language selection using the existing i18n structure

### Requirement 12: Persistência e Rastreamento de Entidades

**User Story:** Como um Backend_API developer, eu quero rastrear automaticamente timestamps de criação e atualização em todas as entidades para auditoria e histórico

#### Acceptance Criteria

1. THE BaseEntity SHALL include CreatedAt property of type DateTime
2. THE BaseEntity SHALL include UpdatedAt property of type DateTime
3. WHEN a new entity is created, THE Backend_API SHALL set CreatedAt to current UTC timestamp
4. WHEN a new entity is created, THE Backend_API SHALL set UpdatedAt to current UTC timestamp
5. WHEN an existing entity is updated, THE Backend_API SHALL update UpdatedAt to current UTC timestamp
6. THE Backend_API SHALL preserve original CreatedAt value when updating entities

### Requirement 13: Tratamento de Erros e Logging

**User Story:** Como um system administrator, eu quero que o sistema registre erros detalhados e falhas de scraping para diagnóstico e resolução de problemas

#### Acceptance Criteria

1. WHEN an unhandled exception occurs, THE Backend_API SHALL log error details including stack trace and request context
2. WHEN a scraping task fails after all retries, THE Scraping_Worker SHALL log detailed error information including proxy used and failure reason
3. THE Backend_API SHALL return user-friendly error responses with appropriate HTTP status codes
4. WHEN validation errors occur, THE Backend_API SHALL return HTTP 400 Bad Request with Validation_Key collection
5. WHEN authentication fails, THE Backend_API SHALL return HTTP 401 Unauthorized
6. WHEN authorization fails, THE Backend_API SHALL return HTTP 403 Forbidden
7. THE System SHALL implement structured logging with correlation IDs for request tracking across components
