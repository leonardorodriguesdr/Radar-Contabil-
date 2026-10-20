# Controle Contábil

Aplicativo desktop para apoiar a rotina de escritórios contábeis no acompanhamento de empresas clientes, obrigações fiscais e contábeis, prazos e fechamentos mensais. A proposta é centralizar o trabalho da equipe em um só lugar: organizar responsabilidades, acompanhar pendências, executar checklists por empresa e competência, armazenar documentos e consultar relatórios de produtividade e auditoria.

O sistema foi desenvolvido em C# com .NET 10 e WPF. A arquitetura separa o domínio (`Core`), persistência e serviços (`Infrastructure`) e interface desktop (`Desktop`). O banco pode usar SQL Server ou PostgreSQL; a configuração local não deve ser compartilhada.

## Funcionalidades

- Painel de prazos, obrigações próximas e atrasadas, com visão por analista.
- Cadastro de empresas clientes, regime tributário e responsáveis.
- Geração e acompanhamento de obrigações por competência.
- Checklists de fechamento configuráveis e acompanhamento de progresso.
- Repositório de documentos associado à empresa e, opcionalmente, à obrigação.
- Relatórios de produtividade, pendências por cliente e auditoria, com exportação CSV.
- Cadastro de usuários, modelos de obrigações e modelos de checklist.

## Requisitos

- .NET SDK 10.0.100 ou superior, conforme `global.json`.
- Windows para executar o aplicativo WPF.
- Uma instância de SQL Server ou PostgreSQL configurada para o ambiente.

## Configuração local

O aplicativo lê `appsettings.json` e, em seguida, `appsettings.Local.json`, que substitui as configurações correspondentes para a máquina atual. Configure a seção `Banco` no arquivo local, sem publicar credenciais:

```json
{
  "Banco": {
    "Provedor": "SqlServer",
    "ConnectionString": "Server=localhost\\SQLEXPRESS;Database=ControleContabil;Integrated Security=True;TrustServerCertificate=True"
  }
}
```

Para PostgreSQL, use `"Provedor": "PostgreSql"` e uma connection string compatível. A pasta de documentos pode ser configurada em `Armazenamento:RaizDocumentos`; se omitida, o sistema usa a pasta padrão local.

Na primeira inicialização, o aplicativo cria as tabelas e carrega modelos iniciais no banco configurado. Confirme que a instância e o banco escolhidos são os corretos antes de iniciar.

## Executar

Para compilar a solução:

```powershell
dotnet build .\ControleContabil.sln
```

Para executar os testes:

```powershell
dotnet test .\tests\ControleContabil.Tests
```

Para iniciar o aplicativo pelo terminal:

```powershell
dotnet run --project .\src\ControleContabil.Desktop\ControleContabil.Desktop.csproj
```

Ou abra `ControleContabil.sln` no Visual Studio, defina `ControleContabil.Desktop` como projeto de inicialização e pressione F5.
