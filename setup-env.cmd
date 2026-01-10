@echo off
REM setup-env.cmd - Quick environment setup script for ExpressRecipe (Windows)

echo.
echo ?? ExpressRecipe Environment Setup
echo ====================================
echo.

REM Check if .env already exists
if exist .env (
    echo ??  .env file already exists!
    set /p OVERWRITE="Do you want to overwrite it? (y/N): "
    if /i not "%OVERWRITE%"=="y" (
        echo ? Setup cancelled. Existing .env file kept.
        exit /b 1
    )
)

REM Check if template exists
if not exist .env.template (
    echo ? Error: .env.template not found!
    echo Make sure you're running this from the project root directory.
    exit /b 1
)

echo ?? Copying .env.template to .env...
copy .env.template .env >nul

echo ?? Generating secure JWT secret...
REM Generate random base64 string using PowerShell
powershell -Command "$bytes = New-Object byte[] 64; (New-Object Security.Cryptography.RNGCryptoServiceProvider).GetBytes($bytes); $secret = [Convert]::ToBase64String($bytes); (Get-Content .env) -replace 'REPLACE-WITH-STRONG-SECRET-MIN-64-CHARS-USE-OPENSSL-RAND-BASE64-64', $secret | Set-Content .env"

echo ? .env file created successfully!
echo.
echo ?? JWT secret has been generated and stored in .env file
echo.
echo ?? Next steps:
echo    1. Review and edit .env file if needed
echo    2. Add any external API keys (USDA, OpenAI, etc.)
echo    3. Run the application: cd src\ExpressRecipe.AppHost.New && dotnet run
echo.
echo ??  Remember: NEVER commit the .env file to git!
echo.
pause
