-- SQL Script to Fix Databases Stuck in SINGLE_USER Mode
-- Run this directly against the SQL Server Docker instance on localhost,1436
-- Connect to the 'master' database before running

USE master;
GO

PRINT '=== ExpressRecipe Database Recovery Script ===';
PRINT 'Fixing databases stuck in SINGLE_USER mode...';
PRINT '';

-- Function to fix a single database
DECLARE @DbName NVARCHAR(128);
DECLARE @Sql NVARCHAR(MAX);
DECLARE @KillSql NVARCHAR(MAX);

-- List of all ExpressRecipe databases
DECLARE db_cursor CURSOR FOR
SELECT name 
FROM sys.databases 
WHERE name LIKE 'ExpressRecipe.%'
ORDER BY name;

OPEN db_cursor;
FETCH NEXT FROM db_cursor INTO @DbName;

WHILE @@FETCH_STATUS = 0
BEGIN
    PRINT '----------------------------------------';
    PRINT 'Processing: ' + @DbName;
    
    -- Check current state
    DECLARE @CurrentState NVARCHAR(60);
    SELECT @CurrentState = user_access_desc 
    FROM sys.databases 
    WHERE name = @DbName;
    
    PRINT 'Current state: ' + @CurrentState;
    
    IF @CurrentState = 'SINGLE_USER'
    BEGIN
        BEGIN TRY
            -- Kill all connections to this database
            SET @KillSql = '';
            SELECT @KillSql = @KillSql + 'KILL ' + CONVERT(VARCHAR(5), spid) + '; '
            FROM master.dbo.sysprocesses
            WHERE dbid = DB_ID(@DbName) AND spid <> @@SPID;
            
            IF LEN(@KillSql) > 0
            BEGIN
                PRINT 'Killing active connections...';
                EXEC sp_executesql @KillSql;
            END
            
            -- Set database to MULTI_USER with rollback immediate
            SET @Sql = 'ALTER DATABASE [' + @DbName + '] SET MULTI_USER WITH ROLLBACK IMMEDIATE;';
            EXEC sp_executesql @Sql;
            
            PRINT '? SUCCESS: ' + @DbName + ' is now MULTI_USER';
        END TRY
        BEGIN CATCH
            PRINT '? ERROR: ' + ERROR_MESSAGE();
        END CATCH
    END
    ELSE
    BEGIN
        PRINT '? OK: Already ' + @CurrentState;
    END
    
    PRINT '';
    
    FETCH NEXT FROM db_cursor INTO @DbName;
END

CLOSE db_cursor;
DEALLOCATE db_cursor;

PRINT '----------------------------------------';
PRINT 'Recovery complete!';
PRINT '';
PRINT 'Final Status:';
SELECT 
    name AS DatabaseName,
    user_access_desc AS AccessMode,
    state_desc AS State
FROM sys.databases
WHERE name LIKE 'ExpressRecipe.%'
ORDER BY name;
GO
