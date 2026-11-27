-- @senha é fornecida como parâmetro pela ferramenta de configuração.
IF DB_NAME() <> N'locacar-db'
    THROW 50001, N'Banco incorreto para criar o usuário da API.', 1;

IF DATABASE_PRINCIPAL_ID(N'locacar_api') IS NULL
BEGIN
    -- CREATE USER exige um literal; QUOTENAME duplica apóstrofos na senha.
    DECLARE @literal nvarchar(258) = QUOTENAME(@senha, '''');
    IF @literal IS NULL
        THROW 50002, N'Senha de runtime inválida.', 1;

    DECLARE @comando nvarchar(512) =
        N'CREATE USER [locacar_api] WITH PASSWORD = N' + @literal + N', DEFAULT_SCHEMA = [dbo];';
    EXEC sys.sp_executesql @comando;
END;
