-- Atualize a matriz no mesmo PR das migrations que mudarem o acesso da API.
-- Para retirar acesso existente, acrescente um REVOKE explícito.
IF DB_NAME() <> N'locacar-db'
    THROW 50001, N'Banco incorreto para configurar as permissões da API.', 1;

IF @@TRANCOUNT = 0
    THROW 50004, N'A aplicação e a validação dos grants exigem uma transação.', 1;

DECLARE @usuario int = DATABASE_PRINCIPAL_ID(N'locacar_api');
IF NOT EXISTS (
    SELECT 1 FROM sys.database_principals
    WHERE principal_id = @usuario AND type = 'S' AND authentication_type_desc = N'DATABASE'
        AND default_schema_name = N'dbo'
) OR EXISTS (
    SELECT 1 FROM sys.database_role_members WHERE member_principal_id = @usuario
)
    THROW 50003, N'O principal não corresponde ao usuário contido restrito.', 1;

DECLARE @matriz TABLE (Tabela sysname PRIMARY KEY, Permissoes nvarchar(50) NOT NULL);
INSERT @matriz (Tabela, Permissoes) VALUES
    (N'Usuarios', N'SELECT, INSERT, UPDATE'),
    (N'Sessoes', N'SELECT, INSERT, UPDATE'),
    (N'RefreshTokens', N'SELECT, INSERT, UPDATE'),
    (N'Grupos', N'SELECT, INSERT, UPDATE'),
    (N'Planos', N'SELECT, INSERT, UPDATE'),
    (N'Veiculos', N'SELECT, INSERT, UPDATE'),
    (N'Clientes', N'SELECT, INSERT, UPDATE'),
    (N'Condutores', N'SELECT, INSERT, UPDATE'),
    (N'TaxasServico', N'SELECT, INSERT, UPDATE'),
    (N'Seguros', N'SELECT, INSERT, UPDATE'),
    (N'PrecosCombustivel', N'SELECT, INSERT, UPDATE'),
    (N'Alugueis', N'SELECT, INSERT, UPDATE'),
    (N'FotosObjetos', N'SELECT, INSERT, UPDATE'),
    (N'ContadoresCodigo', N'SELECT, INSERT, UPDATE'),
    (N'Empresas', N'SELECT, INSERT'),
    (N'ContratosSnapshot', N'SELECT, INSERT'),
    (N'ItensContratados', N'SELECT, INSERT'),
    (N'FechamentosAlugueis', N'SELECT, INSERT'),
    (N'ItensFechamentoAluguel', N'SELECT, INSERT'),
    (N'IdempotenciaAlugueis', N'SELECT, INSERT'),
    (N'VinculosPfPj', N'SELECT, INSERT, DELETE'),
    (N'Auditorias', N'INSERT');

IF EXISTS (SELECT 1 FROM @matriz WHERE OBJECT_ID(N'dbo.' + QUOTENAME(Tabela), N'U') IS NULL)
    THROW 50005, N'A matriz de permissões referencia uma tabela ausente.', 1;

DECLARE @esperadas TABLE (ObjetoId int, Permissao sysname, PRIMARY KEY (ObjetoId, Permissao));
INSERT @esperadas
SELECT OBJECT_ID(N'dbo.' + QUOTENAME(m.Tabela), N'U'), TRIM(p.value)
FROM @matriz AS m
CROSS APPLY STRING_SPLIT(m.Permissoes, N',') AS p;

-- Inventário como administrador: o runtime não vê objetos sem acesso.
DECLARE @objetos TABLE (Id int PRIMARY KEY, Nome nvarchar(517) NOT NULL);
INSERT @objetos
SELECT object_id, QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name)
FROM sys.objects
WHERE is_ms_shipped = 0 AND parent_object_id = 0 AND type NOT IN ('IT', 'TT');

DECLARE @schemas TABLE (Nome nvarchar(258) PRIMARY KEY);
INSERT @schemas
SELECT QUOTENAME(name) FROM sys.schemas WHERE name NOT IN (N'sys', N'INFORMATION_SCHEMA');

-- Flags de delegação, grants fora da matriz e permissões de coluna não são aceitos.
IF EXISTS (
    SELECT 1 FROM sys.database_permissions AS p
    WHERE p.grantee_principal_id = @usuario
      AND NOT (p.state = 'G' AND p.class = 0 AND p.permission_name = N'CONNECT')
      AND NOT (p.state = 'G' AND p.class = 1 AND p.minor_id = 0 AND EXISTS (
          SELECT 1 FROM @esperadas AS e
          WHERE e.ObjetoId = p.major_id
            AND e.Permissao = p.permission_name COLLATE DATABASE_DEFAULT
      ))
) OR EXISTS (
    SELECT 1 FROM sys.database_permissions AS p
    JOIN @objetos AS o ON o.Id = p.major_id
    WHERE p.grantee_principal_id = DATABASE_PRINCIPAL_ID(N'public') AND p.class = 1 AND p.minor_id > 0
)
    THROW 50006, N'Foram encontradas permissões explícitas fora da matriz de runtime.', 1;

DECLARE @grants nvarchar(max);
SELECT @grants = STRING_AGG(CONVERT(nvarchar(max),
    N'GRANT ' + Permissoes + N' ON OBJECT::[dbo].' + QUOTENAME(Tabela) + N' TO [locacar_api];'), NCHAR(10))
FROM @matriz;
EXEC sys.sp_executesql @grants;

DECLARE @efetivas TABLE (ObjetoId int, Permissao sysname);
EXECUTE AS USER = 'locacar_api';
BEGIN TRY
    IF EXISTS (
        SELECT 1 FROM sys.fn_my_permissions(NULL, N'DATABASE')
        WHERE permission_name NOT IN (
            N'CONNECT', N'VIEW ANY COLUMN ENCRYPTION KEY DEFINITION', N'VIEW ANY COLUMN MASTER KEY DEFINITION'
        )
    ) OR EXISTS (
        SELECT 1
        FROM @schemas AS s
        CROSS APPLY sys.fn_my_permissions(s.Nome, N'SCHEMA') AS p
        WHERE p.permission_name IS NOT NULL
    )
        THROW 50007, N'O usuário de runtime possui privilégios excedentes no banco ou em um schema.', 1;

    INSERT @efetivas
    SELECT o.Id, p.permission_name
    FROM @objetos AS o
    CROSS APPLY sys.fn_my_permissions(o.Nome, N'OBJECT') AS p
    WHERE p.permission_name IS NOT NULL AND ISNULL(p.subentity_name, N'') = N'';
END TRY
BEGIN CATCH
    REVERT;
    THROW;
END CATCH;
REVERT;

IF EXISTS (SELECT ObjetoId, Permissao FROM @esperadas EXCEPT SELECT ObjetoId, Permissao FROM @efetivas)
OR EXISTS (SELECT ObjetoId, Permissao FROM @efetivas EXCEPT SELECT ObjetoId, Permissao FROM @esperadas)
    THROW 50008, N'As permissões efetivas do usuário de runtime divergem da matriz.', 1;
