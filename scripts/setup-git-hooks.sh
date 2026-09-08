#!/bin/sh
set -eu
command -v coord-guard >/dev/null 2>&1 || { echo "Install coord-guard before setting up hooks." >&2; exit 1; }
hook="$(git rev-parse --git-path hooks/pre-commit)"
if [ -s "$hook" ]; then
    command -v rg >/dev/null 2>&1 || { echo "Install ripgrep to verify the existing hook." >&2; exit 1; }
    rg -q 'coord-guard' "$hook" || { echo "Existing unrelated pre-commit hook preserved; add coord-guard manually." >&2; exit 1; }
    echo "Coordination guard is already installed."
    exit 0
fi
mkdir -p "$(dirname "$hook")"
cat > "$hook" <<'HOOK'
#!/bin/sh
set -eu
coord-guard
HOOK
chmod +x "$hook"
echo "Installed coordination guard."
