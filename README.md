# SaaS de Extração de Dados do Google Search

Plataforma SaaS completa para extração e monitoramento de resultados de busca do Google em larga escala.

## Estrutura do Projeto

### Backend (.NET Core 8)

```
src/
├── WebApi/              # API REST com controllers
├── Domain/              # Entidades de domínio e lógica de negócio
├── Infrastructure/      # Persistência, mensageria e serviços externos
└── Workers/             # Workers de scraping assíncronos
```

### Frontend (Angular 19)

```
frontend/
├── src/app/
│   ├── core/            # Serviços core (auth, api, i18n)
│   ├── shared/          # Componentes e modelos compartilhados
│   └── features/        # Módulos de funcionalidades (dashboard, search-lists, jobs)
└── src/assets/i18n/     # Arquivos de tradução
```

## Tecnologias

### Backend
- **.NET Core 8**: Framework principal
- **Entity Framework Core**: ORM com PostgreSQL
- **MediatR**: CQRS pattern
- **RabbitMQ**: Sistema de filas
- **Serilog**: Logging estruturado
- **JWT**: Autenticação
- **Playwright/Selenium**: Automação de browser para scraping

### Frontend
- **Angular 19**: Framework frontend
- **SCSS**: Estilização com Glassmorphism
- **RxJS**: Programação reativa
- **Standalone Components**: Arquitetura moderna

## Configuração

### Pré-requisitos
- .NET 8.0 SDK
- Node.js 18+ e npm
- PostgreSQL 15+
- RabbitMQ 3.12+

### Backend

1. Restaurar pacotes:
```bash
dotnet restore
```

2. Configurar appsettings.json com connection strings

3. Executar migrations:
```bash
dotnet ef database update --project src/Infrastructure --startup-project src/WebApi
```

4. Executar WebApi:
```bash
dotnet run --project src/WebApi
```

5. Executar Workers:
```bash
dotnet run --project src/Workers
```

### Frontend

1. Instalar dependências:
```bash
cd frontend
npm install
```

2. Executar em desenvolvimento:
```bash
npm start
```

3. Build para produção:
```bash
npm run build
```

## Arquitetura

### CQRS com Vertical Slice Architecture
- Comandos para operações de escrita
- Queries para operações de leitura
- Features organizadas como slices verticais completos

### Auto-validação de Domínio (Map/Ensure)
- Entidades se auto-validam usando padrão Map/Ensure
- Retorna ValidationKeys estruturadas para i18n
- Validação no momento da criação/atualização

### Sistema de Filas Assíncrono
- Jobs de scraping enfileirados via RabbitMQ
- Workers processam jobs de forma distribuída
- Estado gerenciado por máquina de estados

### Glassmorphism UI
- Interface translúcida com efeitos de vidro
- Sidebar expansível por hover
- Design moderno e responsivo

## Próximos Passos

Esta tarefa criou a estrutura base. As próximas tarefas implementarão:

1. Camada de domínio com entidades e validação
2. Camada de infraestrutura (DbContext, Queue Manager, JWT)
3. Camada de aplicação CQRS (Commands e Queries)
4. Controllers da API
5. Workers de scraping com Playwright
6. Componentes Angular e módulos de features

## Dependências Configuradas

### WebApi
- MediatR 12.4.1
- Swashbuckle.AspNetCore 6.5.0
- Microsoft.AspNetCore.Authentication.JwtBearer 8.0.10
- Microsoft.EntityFrameworkCore.Tools 8.0.10
- Serilog.AspNetCore 8.0.3

### Infrastructure
- Npgsql.EntityFrameworkCore.PostgreSQL 8.0.10
- RabbitMQ.Client 6.8.1
- Microsoft.IdentityModel.Tokens 8.2.1
- System.IdentityModel.Tokens.Jwt 8.2.1
- Microsoft.EntityFrameworkCore.Design 8.0.10

### Workers
- Microsoft.Playwright 1.50.0
- Selenium.WebDriver 4.27.0
- RabbitMQ.Client 6.8.1
- Serilog.Extensions.Hosting 8.0.0

## Internacionalização

O sistema está configurado para PT-BR por padrão, com estrutura preparada para múltiplos idiomas:

- Arquivo de tradução: `frontend/src/assets/i18n/pt-BR.json`
- ValidationKeyPipe para traduzir chaves do backend
- TranslationService para gerenciar traduções

## Status

✅ Estrutura base configurada
✅ Dependências instaladas
✅ Projeto .NET buildando com sucesso
✅ Projeto Angular criado com estrutura modular
✅ Sistema de i18n configurado
✅ Serviços core do Angular implementados
