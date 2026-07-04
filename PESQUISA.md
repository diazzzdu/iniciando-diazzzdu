Relatório Técnico — Arquitetura do Sistema de Biblioteca

**Autor:** [Luis Henrique]
**Stack analisada:** ASP.NET Core MVC + Entity Framework Core + SQLite

Este relatório documenta os fundamentos técnicos da base de código herdada do projeto Biblioteca, cobrindo três pilares: o mecanismo de Injeção de Dependência do ASP.NET Core, o funcionamento do ORM (Entity Framework Core) e as limitações do SQLite como banco de dados de produção.

**Injeção de Dependência (Dependency Injection)** é um padrão de projeto em que uma classe **não cria** as instâncias dos objetos de que depende — ela apenas **declara** essas dependências (geralmente via construtor) e um componente externo, o **container de DI**, é responsável por instanciá-las e "injetá-las" automaticamente.

No nosso projeto, o exemplo mais direto é o `DbContext` sendo recebido no construtor de um Controller:

```csharp
public class LivrosController : Controller
{
    private readonly BibliotecaContext _context;

    public LivrosController(BibliotecaContext context)
    {
        _context = context;
    }
}
```

O Controller **nunca** executa `new BibliotecaContext(...)`. Ele apenas pede ("injete aqui um `BibliotecaContext`") e o ASP.NET Core entrega a instância correta, já configurada.

**Problemas que a DI resolve:**

- **Acoplamento forte:** sem DI, o Controller precisaria conhecer a string de conexão, o provider do banco (SQLite, SQL Server etc.) e a lógica de configuração do `DbContext`. Isso significa que qualquer mudança de infraestrutura obrigaria a alterar o Controller.
- **Testabilidade:** com DI, é possível injetar um `DbContext` falso (in-memory ou mock) em testes automatizados, sem tocar no banco real. Sem DI, isso seria extremamente difícil.
- **Responsabilidade única (SRP):** o Controller foca em orquestrar a requisição HTTP; quem cria e gerencia o ciclo de vida do `DbContext` é o framework.
- **Centralização da configuração:** toda a "receita" de como construir um `BibliotecaContext` fica em um único lugar — o `Program.cs` — em vez de espalhada pelo código.

Ao registrar um serviço no container (`Program.cs`), escolhemos quanto tempo aquela instância deve "viver":

| Ciclo de vida | Quando uma nova instância é criada | Exemplo de uso típico |
|---|---|---|
| **Transient** | A **cada vez** que o serviço é solicitado, mesmo dentro da mesma requisição | Serviços leves, sem estado (stateless), como um gerador de hash ou um validador simples |
| **Scoped** | **Uma instância por requisição HTTP**. Todos os componentes que pedirem o serviço dentro da mesma requisição recebem a mesma instância | `DbContext` — exatamente o nosso caso com `AddScoped<BibliotecaContext>` |
| **Singleton** | **Uma única instância para toda a aplicação**, criada na primeira solicitação (ou na inicialização) e reutilizada para sempre | Configurações em memória, cache global, logging |

**Por que o `DbContext` é registrado como Scoped e nunca como Singleton:**

O `DbContext` do EF Core **não é thread-safe**. Ele mantém um estado interno (o *Change Tracker*) que rastreia quais entidades foram carregadas, modificadas, adicionadas ou removidas durante aquele contexto de uso.

Se o `DbContext` fosse **Singleton**:

1. Concorrência quebrada: múltiplas requisições simultâneas (de usuários diferentes) compartilhariam a mesma instância, e como o EF Core não foi projetado para acesso concorrente por múltiplas threads, isso geraria exceções (`InvalidOperationException: A second operation was started on this context...`) ou corrupção silenciosa de dados.
2. **Vazamento de estado entre usuários:** o Change Tracker acumularia entidades de todas as requisições já processadas desde que a aplicação subiu, consumindo memória indefinidamente e potencialmente misturando dados de um usuário com a consulta de outro.
3. **Conexões "presas":** a conexão com o banco ficaria aberta pelo tempo de vida inteiro da aplicação, em vez de ser aberta e fechada de forma controlada a cada requisição.

Já se fosse **Transient**, cada componente da aplicação (Controller, serviço, repositório) que injetasse o `DbContext` receberia uma instância **diferente** dentro da mesma requisição — quebrando a consistência transacional, pois alterações feitas em uma instância não seriam visíveis na outra, e o `SaveChanges()` de uma não afetaria as entidades rastreadas pela outra.

**Scoped é o equilíbrio correto:** garante que toda a requisição HTTP trabalhe com a **mesma instância** de `DbContext` (preservando consistência e rastreamento de mudanças), mas essa instância é **descartada ao final da requisição**, evitando vazamento de estado e problemas de concorrência entre usuários diferentes.


**ORM (Object-Relational Mapper)** é uma camada de abstração que faz a "tradução" entre o mundo orientado a objetos (classes, propriedades, listas) usado no código C# e o mundo relacional (tabelas, colunas, chaves estrangeiras) usado pelo banco de dados.

Em vez de escrever:

```sql
SELECT * FROM Livros WHERE AutorId = 3;
```

Escrevemos:

```csharp
var livros = _context.Livros.Where(l => l.AutorId == 3).ToList();
```

O EF Core converte essa expressão LINQ em SQL otimizado para o provider configurado (SQLite, no nosso caso), executa a consulta e converte o resultado de volta em objetos `Livro`.

Vantagens para o time:

- **Produtividade:** elimina a necessidade de escrever e manter SQL manualmente para operações CRUD básicas, reduzindo tempo de desenvolvimento.
- **Segurança:** o EF Core gera consultas parametrizadas automaticamente, mitigando riscos de **SQL Injection** que existiriam ao concatenar strings SQL manualmente.
- **Portabilidade:** como o código C# não contém SQL específico de um banco, trocar de provider (de SQLite para PostgreSQL, por exemplo) exige, na maioria dos casos, apenas trocar o pacote NuGet e a connection string — não reescrever as consultas.
- **Manutenibilidade:** mudanças no modelo de dados (adicionar uma propriedade, por exemplo) são refletidas em C#, com o compilador ajudando a pegar erros em tempo de compilação, em vez de erros de SQL detectados apenas em runtime.
- **Rastreamento de estado:** o Change Tracker sabe automaticamente quais objetos foram alterados, permitindo que um único `SaveChanges()` gere os comandos `INSERT`, `UPDATE` e `DELETE` necessários, sem o desenvolvedor escrever cada um manualmente.

**Code-First** é a abordagem em que o **modelo de dados (classes C#) é a fonte da verdade**, e o esquema do banco de dados (tabelas, colunas, relacionamentos) é **derivado** a partir dele — e não o contrário.

No nosso projeto, classes como:

```csharp
public class Livro
{
    public int Id { get; set; }
    public string Titulo { get; set; }
    public int AutorId { get; set; }
    public Autor Autor { get; set; }
}
```

são escritas primeiro. O EF Core analisa essas classes (e as configurações no `DbContext`, via Fluent API ou Data Annotations) e gera o esquema relacional correspondente — definindo `Livro` como tabela, `Id` como chave primária, `AutorId` como chave estrangeira para `Autor`, etc.

A alternativa seria o **Database-First**, em que o banco já existe (criado manualmente ou por um DBA) e as classes C# são geradas a partir do esquema existente — o caminho inverso.

Code-First é vantajoso quando a equipe de desenvolvimento **controla a evolução do esquema** junto com a evolução do código, mantendo tudo versionado no mesmo repositório Git.

**Migrations** são o mecanismo que permite evoluir o esquema do banco de dados de forma **incremental e versionada**, mantendo sincronia entre o modelo C# e as tabelas reais.

**O fluxo de trabalho:**

1. O desenvolvedor altera uma classe de modelo (ex.: adiciona a propriedade `DataPublicacao` em `Livro`).
2. Executa `dotnet ef migrations add AdicionaDataPublicacao`.
3. O EF Core **compara** o estado atual do modelo (definido pelas classes C#) com um **snapshot** do último estado conhecido do banco — esse snapshot fica armazenado no arquivo `*ModelSnapshot.cs`, dentro da pasta `Migrations/`.
4. A partir da diferença encontrada, o EF Core gera um novo arquivo de migration contendo dois métodos:
   - `Up()`: o que deve ser executado para **aplicar** a mudança (ex.: `ADD COLUMN DataPublicacao`).
   - `Down()`: o que deve ser executado para **reverter** a mudança (ex.: `DROP COLUMN DataPublicacao`), permitindo rollback.

**O que acontece ao rodar `dotnet ef database update`:**

1. O EF Core conecta no banco de dados configurado na connection string.
2. Verifica a existência de uma tabela de controle interna chamada **`__EFMigrationsHistory`**, que armazena o nome de **todas as migrations já aplicadas** naquele banco específico.
3. Compara essa lista com as migrations existentes no projeto (na pasta `Migrations/`).
4. Para cada migration que existe no código mas **não** está registrada em `__EFMigrationsHistory`, o EF Core executa o método `Up()` correspondente, na ordem cronológica em que foram criadas.
5. Após aplicar cada migration com sucesso, insere uma nova linha em `__EFMigrationsHistory` registrando que ela foi aplicada.

É exatamente essa tabela de controle que permite ao EF Core saber **"o que já foi criado e o que é novo"** — sem ela, o framework não teria como diferenciar um banco já atualizado de um banco desatualizado, e poderia tentar recriar tabelas já existentes.

Isso também é o que possibilita que **vários desenvolvedores** trabalhem no mesmo projeto: cada um aplica as migrations criadas pelos colegas em sua máquina local, e todos os bancos (dev, teste, produção) evoluem de forma consistente e rastreável — tudo versionado no Git junto com o código.


O SQLite é um **banco de dados embarcado**, ou seja, ele não roda como um processo de servidor separado — o banco inteiro é um único arquivo `.db` lido e escrito diretamente pela aplicação.

**Vantagens nesse estágio do projeto:**

- **Zero configuração:** não exige instalação de servidor de banco, criação de usuários, configuração de rede ou portas — basta o arquivo.
- **Portabilidade:** o arquivo `.db` pode ser copiado, versionado (com cuidado) ou movido entre máquinas com facilidade, o que é ótimo para ambiente de desenvolvimento e testes locais.
- **Velocidade de setup:** um novo desenvolvedor no time consegue clonar o repositório, rodar `dotnet ef database update` e já ter um banco funcional em segundos, sem depender de infraestrutura externa.
- **Custo zero:** não há custo de hospedagem de banco de dados durante a fase de prototipação.
- **Simplicidade para testes automatizados:** é comum usar SQLite in-memory em testes de integração, pois cada teste pode ter seu próprio banco isolado e descartável.

O principal limite do SQLite está no **modelo de bloqueio (locking) a nível de arquivo**.

Por padrão, o SQLite permite **múltiplas leituras simultâneas**, mas **apenas uma escrita por vez** em todo o banco de dados. Quando uma operação de escrita (`INSERT`, `UPDATE`, `DELETE`) está em andamento, o arquivo inteiro fica bloqueado para qualquer outra escrita — e, dependendo do modo configurado, também pode bloquear leituras.

Isso acontece porque o SQLite não possui um processo servidor gerenciando conexões concorrentes de forma sofisticada (como filas de transações e controle de concorrência multiversão completo) — ele depende de mecanismos de lock do próprio sistema de arquivos.

Na prática, com 10.000 acessos simultâneos:
- Se uma fração relevante desses acessos envolver escrita (empréstimo de um livro, cadastro de um novo usuário, devolução), as requisições começarão a **enfileirar e travar**, aguardando o lock ser liberado.
- O resultado são timeouts, exceções do tipo `SQLite Error 5: 'database is locked'`, e degradação severa de performance — o sistema pode parecer "travado" para boa parte dos usuários.
- Mesmo com o modo **WAL (Write-Ahead Logging)**, que melhora a concorrência entre leitores e um escritor, o SQLite continua permitindo **apenas um escritor por vez** — ele não resolve o problema de múltiplas escritas simultâneas, apenas o atenua.

Em resumo: SQLite foi projetado para cargas de **baixa a média concorrência**, tipicamente aplicações desktop, mobile ou sistemas internos pequenos — não para cenários de alto tráfego web com múltiplos usuários escrevendo dados ao mesmo tempo.

### 3.3 Quando migrar para um banco robusto em nuvem

A migração de SQLite para um banco como **PostgreSQL** ou **SQL Server** deixa de ser opcional e passa a ser necessária quando aparecem sinais como:

- **Alto volume de escritas concorrentes:** quando múltiplos usuários simultâneos realizam operações de escrita com frequência (exatamente o cenário dos 10.000 acessos/mês citado pelo CTO, especialmente se parte significativa envolver cadastro, empréstimo ou devolução de livros).
- **Necessidade de alta disponibilidade:** sistemas de produção geralmente exigem replicação, failover automático e backups consistentes em ambiente distribuído — recursos nativos em bancos como PostgreSQL, mas inexistentes (ou muito limitados) no SQLite.
- **Múltiplos servidores de aplicação:** se a aplicação precisar escalar horizontalmente (rodar em mais de uma instância/contêiner ao mesmo tempo), um banco de arquivo único como o SQLite se torna um gargalo e um ponto de inconsistência, pois cada instância acessaria o mesmo arquivo competindo por locks.
- **Requisitos de segurança e controle de acesso granular:** bancos como PostgreSQL e SQL Server oferecem gerenciamento de usuários, papéis (roles) e permissões em nível de banco — algo que o SQLite não foi projetado para suportar de forma robusta.
- **Volume de dados crescente com necessidade de otimizações avançadas:** índices mais sofisticados, particionamento de tabelas, procedures armazenadas e otimizador de consultas mais avançado tornam-se relevantes à medida que o volume de dados cresce.

**Importante:** graças à arquitetura ORM do EF Core (Code-First + Migrations), essa migração é significativamente facilitada. Na maior parte dos casos, a troca envolve:

1. Trocar o pacote NuGet do provider (`Microsoft.EntityFrameworkCore.Sqlite` → `Npgsql.EntityFrameworkCore.PostgreSQL`, por exemplo).
2. Atualizar a connection string no `appsettings.json`.
3. Regenerar as migrations para o novo provider (alguns tipos de dados têm mapeamentos diferentes entre SQLite e PostgreSQL/SQL Server).
4. Rodar `dotnet ef database update` apontando para o novo banco.

O código de Controllers, Services e a lógica de negócio em si **praticamente não precisa ser alterado**, pois toda a comunicação com o banco passa pela abstração do `DbContext` — o que reforça, na prática, a vantagem do ORM e da DI discutidas nas seções anteriores.

---

## Conclusão

A combinação de **Injeção de Dependência** (gerenciando o ciclo de vida do `DbContext` de forma segura via Scoped), **Entity Framework Core** (abstraindo o acesso a dados via Code-First e Migrations) e **SQLite** (como banco leve para desenvolvimento) forma uma base sólida e produtiva para a fase atual do projeto.

No entanto, a arquitetura escolhida tem um teto de escalabilidade claro: o modelo de concorrência do SQLite não suporta o volume de escritas simultâneas esperado para produção em larga escala. A boa notícia é que, justamente por seguir as boas práticas de DI e ORM desde o início, a migração futura para PostgreSQL ou SQL Server é uma troca de configuração e provider — não uma reescrita do sistema.

---

*Referências sugeridas para aprofundamento: documentação oficial da Microsoft sobre [ASP.NET Core Dependency Injection](https://learn.microsoft.com/aspnet/core/fundamentals/dependency-injection), [EF Core Migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations/) e [SQLite — Appropriate Uses](https://www.sqlite.org/whentouse.html).*
