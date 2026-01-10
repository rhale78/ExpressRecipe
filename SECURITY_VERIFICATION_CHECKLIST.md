# ?? Security Configuration Verification Checklist

Run these commands to verify everything is properly configured:

## ? Step 1: Verify .gitignore Protection

```bash
# Test that .env is ignored
git check-ignore -v .env
# Expected output: .gitignore:177:**/.env	.env
```

? **VERIFIED**: `.env` file is git-ignored at line 177

## ? Step 2: Verify No Secrets in Git History

```bash
# Search for any JWT secrets in git history
git log -S "JWT_SECRET_KEY" --all --oneline

# Search for any secrets in current staging area
git grep -i "secretkey.*:" -- '*.json'
```

Expected: Only finds placeholders in `.env.template`, not real secrets

## ? Step 3: Test Environment File Creation

```bash
# Run setup script
./setup-env.sh      # Linux/macOS
# OR
setup-env.cmd       # Windows

# Verify .env was created
ls -la .env
# OR (Windows)
dir .env
```

Expected: `.env` file exists with generated JWT secret

## ? Step 4: Verify Configuration Loads

```bash
# Start the AppHost
cd src/ExpressRecipe.AppHost.New
dotnet run
```

Expected: Services start without "JWT SecretKey not configured" errors

## ? Step 5: Test JWT Token Generation

```bash
# Test AuthService endpoint (once running)
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"test123"}'
```

Expected: Returns JWT token or appropriate error (not configuration error)

## ? Step 6: Verify Files Safe to Commit

```bash
# Check what's staged
git status

# Files that SHOULD be staged:
# ? .env.template
# ? .gitignore
# ? setup-env.sh
# ? setup-env.cmd
# ? ENVIRONMENT_VARIABLES_SECURITY.md
# ? SECURITY_MIGRATION_COMPLETE.md
# ? src/Services/*/appsettings.json (cleaned)

# Files that should NOT appear (git-ignored):
# ?? .env
# ?? .env.local
# ?? appsettings.Development.json
# ?? appsettings.Production.json
```

## ? Step 7: Verify Secret Rotation Ready

```bash
# Generate new production secret
openssl rand -base64 64

# Windows PowerShell:
[Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Maximum 256 }))
```

Expected: Can generate new secrets easily for rotation

## ?? Quick Verification Commands

Run all checks at once:

### Linux/macOS
```bash
echo "=== Checking .gitignore ==="
git check-ignore -v .env

echo "=== Checking for secrets in git ==="
git log -S "JWT_SECRET_KEY" --all --oneline | head -5

echo "=== Verifying .env file ==="
test -f .env && echo "? .env exists" || echo "? .env missing"

echo "=== Checking .env is ignored ==="
git status --short .env | grep -q "^$" && echo "? .env is git-ignored" || echo "? .env not ignored"

echo "=== Checking for secrets in appsettings.json ==="
grep -r "SecretKey.*:" src/Services/*/appsettings.json && echo "?? Found secrets!" || echo "? No secrets in appsettings.json"
```

### Windows PowerShell
```powershell
Write-Host "=== Checking .gitignore ===" -ForegroundColor Cyan
git check-ignore -v .env

Write-Host "=== Checking for secrets in git ===" -ForegroundColor Cyan
git log -S "JWT_SECRET_KEY" --all --oneline | Select-Object -First 5

Write-Host "=== Verifying .env file ===" -ForegroundColor Cyan
if (Test-Path .env) { Write-Host "? .env exists" -ForegroundColor Green } else { Write-Host "? .env missing" -ForegroundColor Red }

Write-Host "=== Checking .env is ignored ===" -ForegroundColor Cyan
$status = git status --short .env
if ([string]::IsNullOrWhiteSpace($status)) { Write-Host "? .env is git-ignored" -ForegroundColor Green } else { Write-Host "? .env not ignored" -ForegroundColor Red }

Write-Host "=== Checking for secrets in appsettings.json ===" -ForegroundColor Cyan
$secrets = Select-String -Path src\Services\*\appsettings.json -Pattern "SecretKey.*:" -ErrorAction SilentlyContinue
if ($secrets) { Write-Host "?? Found secrets!" -ForegroundColor Yellow } else { Write-Host "? No secrets in appsettings.json" -ForegroundColor Green }
```

## ?? Expected Results Summary

| Check | Expected Result | Status |
|-------|----------------|--------|
| `.gitignore` includes `.env` | ? YES | ? Verified |
| `.env` file is git-ignored | ? YES | ? Verified |
| No secrets in `appsettings.json` | ? YES | ? Verified |
| `.env.template` has placeholders only | ? YES | ? Verified |
| Setup scripts work | ? YES | ? Test manually |
| Services start without config errors | ? YES | ? Test manually |
| JWT tokens can be generated | ? YES | ? Test manually |

## ?? What to Do If Checks Fail

### If `.env` appears in `git status`
```bash
# Verify .gitignore has the rule
grep ".env" .gitignore

# Force git to re-check ignores
git rm --cached .env
git status  # Should not show .env anymore
```

### If secrets found in `appsettings.json`
```bash
# Remove the secret manually
# Edit src/Services/*/appsettings.json
# Remove the "JwtSettings" section or set to empty/default values

# Verify removal
git diff src/Services/*/appsettings.json
```

### If `.env` file doesn't exist
```bash
# Run setup script
./setup-env.sh  # Linux/macOS
setup-env.cmd   # Windows

# Or manually copy template
cp .env.template .env

# Edit .env and add JWT secret
nano .env  # or use your preferred editor
```

### If services fail to start
```bash
# Check .env file has JWT_SECRET_KEY
grep JWT_SECRET_KEY .env

# Verify it's not just a comment
grep -v "^#" .env | grep JWT_SECRET_KEY

# Check environment variable is set
echo $JWT_SECRET_KEY  # Linux/macOS
echo %JWT_SECRET_KEY%  # Windows CMD
$env:JWT_SECRET_KEY   # Windows PowerShell
```

## ?? Pre-Commit Checklist

Before committing any code, verify:

- [ ] ? `.env` file is NOT in the commit
- [ ] ? No secrets in `appsettings.json` files
- [ ] ? No secrets in any committed files
- [ ] ? `.gitignore` includes `.env` protection
- [ ] ? `.env.template` contains only placeholders
- [ ] ? Documentation is up to date

## ?? Training for Team

Share with new developers:

1. **Read** `ENVIRONMENT_VARIABLES_SECURITY.md`
2. **Run** `./setup-env.sh` or `setup-env.cmd`
3. **Verify** using this checklist
4. **Never commit** `.env` files
5. **Ask questions** if unsure about any secret

## ?? Reference Documentation

- `ENVIRONMENT_VARIABLES_SECURITY.md` - Complete security guide
- `SECURITY_MIGRATION_COMPLETE.md` - What was changed and why
- `.env.template` - Template for environment variables
- `setup-env.sh` / `setup-env.cmd` - Automated setup scripts

## ? Final Status

**Date:** 2024
**Security Review:** ? PASSED
**Git Protection:** ? VERIFIED
**Configuration:** ? MIGRATED
**Documentation:** ? COMPLETE

---

**?? Remember:** Security is not a one-time task. Review this checklist regularly and update as needed.
