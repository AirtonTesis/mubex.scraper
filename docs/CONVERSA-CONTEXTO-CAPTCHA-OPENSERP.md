# Contexto da conversa: CAPTCHA, SERP e OpenSERP

**Data do registro:** 01/08/2026  
**Projeto:** Mubex Scraper / Google Search Scraper SaaS  
**Objetivo deste documento:** preservar o contexto técnico, as decisões e os próximos passos discutidos na conversa.

> Este documento é um registro de contexto. Não contém tokens, imagens de CAPTCHA, credenciais ou payloads sensíveis. As recomendações relacionadas a evasão de mecanismos anti-bot foram deliberadamente excluídas do escopo de implementação.

---

## 1. Contexto do workspace

O workspace é uma plataforma SaaS para coleta e monitoramento de resultados de busca, composta por:

- Backend em .NET 8, dividido em `Domain`, `Infrastructure`, `WebApi` e `Workers`.
- Frontend em Angular.
- Jobs assíncronos processados por filas.
- Persistência com Entity Framework Core e PostgreSQL.
- Scraping atual baseado principalmente em Playwright/Chromium.
- Integração existente com um webhook externo para interpretação das imagens do desafio visual.

Principais pontos do fluxo atual:

- `src/Infrastructure/Scraping/IScrapingEngine.cs`
- `src/Infrastructure/Scraping/PlaywrightScrapingEngine.cs`
- `src/Infrastructure/Scraping/CaptchaDetectionService.cs`
- `src/Infrastructure/Scraping/HumanClickService.cs`
- `src/Infrastructure/Scraping/ScrapingModels.cs`
- `src/WebApi/ScrapingBackgroundWorker.cs`
- `src/Workers/Worker.cs`
- `src/WebApi/appsettings.json`
- `src/Workers/appsettings.json`

O modelo de resultado atual contém, entre outros campos, keyword, domínio, posição, URL, título e snippet.

---

## 2. Problema relatado

O fluxo atual envia ao serviço externo o cabeçalho e as células do desafio visual. A classificação das imagens apresenta alta taxa de acerto e o Playwright seleciona os quadrantes indicados.

Mesmo assim, o Google frequentemente apresenta novas rodadas de validação e raramente libera a página de resultados.

### Conclusão técnica principal

Acertar as imagens não significa que a sessão foi aceita. A decisão de liberação pode considerar outros sinais de risco, contexto, rede, sessão e ambiente. Portanto, o problema não deve ser tratado apenas como um problema de classificação visual ou de coordenadas de clique.

O sistema precisa distinguir claramente:

```text
ChallengeDetected
ChallengeAnswered
VerificationPending
VerificationAccepted
ResultsLoaded
RepeatedChallenge
BlockedByRisk
VerificationExpired
NavigationTimeout
```

---

## 3. Escopo seguro adotado

A conversa estabeleceu que o trabalho deve priorizar confiabilidade, diagnóstico e alternativas autorizadas, sem orientar ou implementar:

- mascaramento de automação;
- spoofing de fingerprint;
- rotação de proxies para contornar bloqueios;
- simulação de comportamento humano com finalidade de evasão;
- bypass de CAPTCHA ou de controles antiabuso;
- uso de credenciais, tokens ou imagens reais em logs.

O caminho recomendado é interromper ciclos improdutivos, medir o estado final e avaliar APIs ou provedores de SERP autorizados para o caso de uso.

---

## 4. Achados no fluxo existente

### 4.1 Detecção pode gerar falso positivo

O detector considera a presença de elementos como `div.g-recaptcha` e iframes do reCAPTCHA como evidência de CAPTCHA ativo.

Um iframe pode permanecer no DOM mesmo depois que o desafio visual foi concluído. Assim, a existência do iframe isoladamente não deve ser usada como prova de que a verificação falhou.

A detecção deve combinar sinais de estado, URL, conteúdo e carregamento de resultados.

### 4.2 Desaparecimento do grid não é confirmação suficiente

O desaparecimento do grid indica apenas que uma etapa visual mudou. A confirmação deve observar também:

- navegação para uma página legítima de resultados;
- presença de resultados orgânicos;
- ou estado legítimo de “sem resultados”;
- ausência de retorno imediato à página de bloqueio;
- URL e título compatíveis com uma busca normal;
- timeout e classificação explícita quando a confirmação não ocorrer.

### 4.3 Há loops e tentativas aninhadas

O fluxo possui configurações e mecanismos que podem se multiplicar:

- `MaxRetries` por processamento;
- `MaxCaptchaRounds` por desafio;
- reloads após falhas;
- novas detecções após a tentativa de resolução;
- timeouts específicos do CAPTCHA.

É necessário definir um orçamento global por keyword/job, evitando que a combinação dos limites produza tentativas excessivas e resultados difíceis de interpretar.

### 4.4 Telemetria atual precisa ser estruturada e redigida

A instrumentação deve registrar eventos úteis sem guardar conteúdo sensível. Campos sugeridos:

```text
captcha_session_id
keyword_hash ou identificador não reversível
round
grid_size
webhook_latency_ms
selected_cells_count
verify_clicked
challenge_visible_after_verify
url_classification
results_count
elapsed_ms
final_state
```

Não registrar:

- imagens do cabeçalho ou das células;
- tokens;
- payload bruto do webhook;
- URLs com parâmetros sensíveis;
- credenciais ou cookies.

### 4.5 Estado operacional precisa ser persistido ou explicitamente limitado a logs

Se os estados forem necessários no histórico de jobs e no dashboard, devem ser adicionados contratos claros para:

- `Job`;
- `JobHistoryEntry`;
- DTOs da API;
- consultas do dashboard;
- configurações do Entity Framework e migrations;
- traduções do frontend.

Se não houver necessidade de persistência, a decisão deve ser documentada e a telemetria deve ser a fonte operacional.

---

## 5. Backlog criado

Foi adicionado ao arquivo:

```text
.kiro/specs/google-search-scraper-saas/tasks.md
```

O backlog recebeu o épico **17 — Estabilizar o fluxo de CAPTCHA e confirmação de resultados**, com tarefas para:

1. Definir estados explícitos do fluxo.
2. Criar um resultado estruturado para a tentativa de verificação.
3. Separar challenge, bloqueio, expiração e resultados legítimos.
4. Implementar confirmação pós-verificação baseada em sinais determinísticos.
5. Definir orçamento global para retries, rounds e reloads.
6. Implementar telemetria estruturada.
7. Centralizar redaction e testar a não exposição de dados sensíveis.
8. Cobrir o fluxo com testes unitários.
9. Cobrir o contrato do webhook externo sem registrar imagens ou tokens.
10. Definir política para fallback por API ou provedor autorizado.
11. Criar uma engine/adaptador de SERP desacoplado do Playwright.
12. Conectar o fallback ao orquestrador após estados de bloqueio ou repetição.
13. Definir circuito de proteção para bloqueios repetidos.
14. Decidir e implementar a persistência dos estados operacionais.
15. Atualizar histórico, dashboard e runbook operacional.
16. Executar um checkpoint de validação antes de qualquer adoção em produção.

As tasks legadas que mencionam stealth, proxy rotation ou técnicas semelhantes devem ser tratadas como fora do escopo desta prioridade e não devem ser implementadas como solução para o problema.

---

## 6. Análise do OpenSERP

### 6.1 O que é

O OpenSERP (`karust/openserp`) foi analisado como uma API/CLI self-hosted escrita em Go, com execução possível via Docker e normalização de resultados de diferentes mecanismos de busca.

Motores mencionados na análise:

- Google;
- Bing;
- Yandex;
- Baidu;
- DuckDuckGo;
- Ecosia.

A licença identificada foi MIT.

### 6.2 Possíveis papéis no sistema

O OpenSERP não deve ser tratado como solução para fazer o Google aceitar uma sessão automatizada. Ele pode, contudo, ser útil em três papéis:

#### A. Sidecar de SERP

Executar o OpenSERP como serviço separado e acessá-lo por HTTP a partir do backend .NET. Isso reduz o acoplamento entre o domínio do sistema e os detalhes de parsing da página Google.

#### B. Fallback multi-engine

Quando a fonte Google estiver indisponível ou retornar um estado de bloqueio, consultar mecanismos alternativos, desde que isso seja compatível com o produto, com os termos aplicáveis e com a expectativa de ranking do cliente.

#### C. PoC de normalização

Usar o OpenSERP para testar um contrato comum de resultados antes de substituir ou reorganizar o `PlaywrightScrapingEngine`.

### 6.3 Mapeamento conceitual

O mapeamento esperado para o modelo atual é aproximadamente:

| OpenSERP | Modelo interno |
|---|---|
| `query`/texto consultado | `Keyword` |
| `rank` | `Position` |
| `url` | `Url` |
| `title` | `Title` |
| `snippet` | `Snippet` |
| `domain` ou hostname da URL | `Domain` |
| engine de origem | metadado da fonte |

O mapeamento real deve ser confirmado contra o contrato da versão escolhida do OpenSERP durante a PoC.

### 6.4 Benefícios potenciais

- Redução da dependência de seletores frágeis do Google.
- Separação entre coleta e parsing de SERP.
- Acesso a múltiplos mecanismos.
- Possibilidade de comparar latência, taxa de erro e cobertura.
- Diferenciação mais clara entre resultados, bloqueios e falhas.
- Menor manutenção do parser Playwright caso o sidecar seja adotado.

### 6.5 Limitações e riscos

- O OpenSERP não elimina CAPTCHA, bloqueios ou políticas dos mecanismos consultados.
- A coleta via Google continua sujeita às condições da rede, sessão e termos aplicáveis.
- Ranking entre mecanismos não é equivalente a ranking Google.
- A posição pode variar por país, idioma, dispositivo, localização, anúncios, snippets e features da SERP.
- É preciso confirmar suporte real a paginação, locale, limites e formato da resposta na versão usada.
- Um serviço adicional aumenta custo operacional, observabilidade, deploy e superfície de falha.
- O uso deve ser avaliado juridicamente e em relação aos termos dos mecanismos e às autorizações do produto.

### 6.6 Recomendação

A recomendação é **não substituir imediatamente** o scraper atual pelo OpenSERP.

O caminho de menor risco é:

1. Executar uma PoC isolada.
2. Consultar um pequeno conjunto de keywords de teste.
3. Comparar Google, Bing e DuckDuckGo, sem usar a comparação como prova de equivalência de ranking.
4. Medir latência, taxa de erro, estados de bloqueio e quantidade de resultados.
5. Validar o contrato JSON e o mapeamento para `SearchResultData`.
6. Confirmar parâmetros de idioma, região, paginação e limite.
7. Definir se o produto aceita resultados de múltiplas fontes.
8. Só então decidir entre sidecar, fallback ou descarte.

A arquitetura preferida para a PoC é um adaptador atrás de `IScrapingEngine`, sem remover o `PlaywrightScrapingEngine` antes da comparação.

---

## 7. Plano técnico consolidado

### Fase 1 — Observabilidade e controle

- Implementar a máquina de estados.
- Estruturar o resultado da tentativa.
- Confirmar resultados após a verificação.
- Definir orçamento global de tentativas.
- Redigir logs e remover payloads sensíveis.
- Criar testes para transições e estados finais.

### Fase 2 — Proteção operacional

- Encerrar ciclos de repetição sem progresso.
- Implementar circuito de proteção para bloqueios repetidos.
- Persistir estados necessários no job/histórico.
- Expor métricas no dashboard e no runbook.

### Fase 3 — PoC do OpenSERP

- Subir o OpenSERP isoladamente via Docker.
- Definir contrato HTTP e configuração do endpoint.
- Criar adaptador que implemente `IScrapingEngine`.
- Mapear e validar resultados.
- Comparar fontes e documentar diferenças.

### Fase 4 — Decisão de adoção

Possíveis decisões após a PoC:

- manter Playwright apenas para casos autorizados e de baixo volume;
- usar OpenSERP como fonte complementar;
- usar OpenSERP como fallback multi-engine;
- adotar uma API/provedor autorizado de SERP;
- não adotar o OpenSERP se a confiabilidade, os termos ou o custo não forem adequados.

---

## 8. Critérios de sucesso

A solução será considerada tecnicamente mais confiável quando:

- uma tentativa terminar em um estado explícito;
- “grid desapareceu” não for confundido com sucesso final;
- resultados legítimos e bloqueios forem diferenciados;
- retries e rounds estiverem limitados por orçamento global;
- logs permitirem investigar a falha sem expor conteúdo sensível;
- o sistema parar de repetir indefinidamente uma keyword bloqueada;
- o fallback puder ser acionado por estado, e não por exceções genéricas;
- a PoC do OpenSERP fornecer métricas comparáveis e uma decisão documentada.

---

## 9. Referências consultadas

- `docs/RELATORIO-CAPTCHA-SOLVING.md`
- `.kiro/specs/google-search-scraper-saas/requirements.md`
- `.kiro/specs/google-search-scraper-saas/design.md`
- `.kiro/specs/google-search-scraper-saas/tasks.md`
- `src/Infrastructure/Scraping/IScrapingEngine.cs`
- `src/Infrastructure/Scraping/PlaywrightScrapingEngine.cs`
- `src/Infrastructure/Scraping/CaptchaDetectionService.cs`
- `src/Infrastructure/Scraping/ScrapingModels.cs`
- `src/WebApi/ScrapingBackgroundWorker.cs`
- `src/Workers/Worker.cs`
- Repositório analisado: <https://github.com/karust/openserp>

---

## 10. Estado ao final da conversa

- O problema foi contextualizado como falha de aceitação/estado da sessão, não apenas de classificação de imagens.
- Foi criado um backlog de implementação no `tasks.md`.
- O OpenSERP foi avaliado como possível sidecar, fallback ou ferramenta de PoC, mas não como solução de CAPTCHA.
- Nenhuma credencial, imagem, token ou payload sensível foi incorporado a este registro.
- Este documento foi criado para preservar o contexto e facilitar a continuidade do trabalho.
