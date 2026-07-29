# Product Requirements Document (PRD) — SaaS de Extração de Dados do Google Search

## 1. Visão Geral do Produto
O **SaaS de Extração de Dados do Google Search** é uma plataforma web para automação, monitoramento e extração de resultados de buscas em larga escala. A ferramenta combina navegação furtiva de ponta (Playwright e Selenium) com uma arquitetura moderna e escalável (.NET Core com CQRS, Vertical Slice Architecture, entidades auto-validadas via `Map`/`Ensure` com chaves de tradução) e um frontend em **Angular** com design **Glassmorphism** e sidebar expansível por *hover*.

---

## 2. Personas e Público-Alvo
* **Profissionais de SEO & Marketing:** Necessitam monitorar o posicionamento de domínios e palavras-chave recorrentemente.
* **Analistas de Dados e Inteligência de Mercado:** Buscam extrair dados públicos do mecanismo de busca para cruzamento de tendências e preços.
* **Empresas de Tecnologia / Donos de Agências:** Precisam de uma ferramenta automatizada, estável e segura para auditorias de busca em massa.

---

## 3. Requisitos Funcionais

### 3.1. Frontend (Angular)
* **Layout e Estilização:** 
  * Interface desenvolvida em **Angular** utilizando o conceito de **Glassmorphism** (efeito de vidro translúcido em cards, menus e modais, evitando cores sólidas e chapadas).
* **Sidebar Interativa:**
  * Menu lateral recolhido por padrão para maximizar a área útil de trabalho.
  * Funcionalidade de expansão fluida ativada ao passar o mouse (**hover**), ajustando automaticamente o posicionamento do conteúdo principal.
* **Dashboard Analítico:**
  * Visualização de métricas de extração (volume de buscas, taxas de sucesso/falha, execuções ativas).
  * Filtros temporais dinâmicos para visualização por **Dia**, **Semana**, **Mês** e **Período Personalizado** (Custom Range).
* **Gestão de Listas:**
  * Telas de CRUD para criação, edição, exclusão e visualização de listas de palavras-chave e domínios.
  * Exibição de mensagens de erro traduzidas via frontend utilizando chaves retornadas pelo backend (ex: `validation.search_list.name_required`).

### 3.2. Backend (.NET Core + CQRS + Vertical Slice)
* **Arquitetura:**
  * Organização baseada em **Vertical Slice Architecture**, onde cada funcionalidade (feature) encapsula seu próprio endpoint, comandos/queries (MediatR), validadores e regras de negócio.
  * Aplicação estrita de **CQRS** para separar operações de escrita (comandos de disparo e salvamento) das operações de leitura otimizadas (dashboard e relatórios).
* **Modelagem de Domínio & Auto-Validação:**
  * Normalização de entidades utilizando uma classe abstrata base (**`BaseEntity`**), controlando `Id`, `CreatedAt` e `UpdatedAt`.
  * **Auto-validação funcional:** As entidades e seus validadores dedicados utilizam funções como **`Map`** e **`Ensure`** para proteger invariantes de domínio.
  * **Chaves de Tradução:** Falhas de validação no domínio retornam identificadores estruturados (chaves) direcionados à internacionalização no frontend, em vez de textos fixos.
* **Camada de Orquestração e Fila:**
  * Endpoints assíncronos para disparar rotinas de extração sem bloquear o fluxo HTTP.
  * Sistema de filas para gerenciamento de jobs de scraping.

### 3.3. Motor de Scraping & Navegação Furtiva (Workers)
* **Tecnologias de Automação:**
  * Uso prioritário de **Playwright (.NET)** para simulações rápidas e contextos isolados.
  * **Selenium** integrado como suporte/fallback para cenários avançados de *fingerprinting*.
* **Mecanismos Anti-Bot (Stealth):**
  * Emulação de interações humanas (movimentos de cursor, atrasos aleatórios).
  * Mascaramento de flags de automação (remoção de `navigator.webdriver`).
  * Suporte a rotação de proxies residenciais e gerenciamento dinâmico de User-Agents para evitar bloqueios e CAPTCHAs pelo Google.

---

## 4. Requisitos Não-Funcionais
* **Escalabilidade:** Arquitetura desacoplada permitindo escalar os workers de scraping horizontalmente de forma independente da API.
* **Desempenho:** Consultas de leitura do dashboard otimizadas (sem tracking excessivo no EF Core) para entrega rápida de relatórios independentemente do volume histórico.
* **Segurança e Consistência:** Autenticação baseada em tokens (JWT) e integridade de dados garantida pelas regras rígidas de auto-validação das entidades de domínio.

---

## 5. Critérios de Aceite (MVP)
1. O usuário consegue fazer login, visualizar a sidebar que expande via *hover* (com visual em glassmorphism) e navegar até o gerenciador de listas.
2. É possível submeter dados para criar uma lista; o backend processa via CQRS e valida a entidade de forma autocontida utilizando `Map` e `Ensure`.
3. Caso ocorram erros de validação, o backend retorna chaves padronizadas (ex: `validation.search_list.name_required`) para tradução correta no front.
4. O motor de scraping executa a navegação utilizando Playwright/Selenium com camuflagem contra detecção do Google.
5. O dashboard exibe as métricas atualizadas permitindo alternar dinamicamente entre os filtros de dia, semana, mês e período personalizado.