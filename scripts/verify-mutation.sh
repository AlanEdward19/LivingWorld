#!/usr/bin/env bash
# Harness de mutação do gate (Fase 0, task 8): prova que verify.sh sabe reprovar.
# Copia o repo para um dir temporário, aplica um mutante conhecido, exige saída != 0.
# Uso: bash scripts/verify-mutation.sh
set -uo pipefail
cd "$(dirname "$0")/.."
REPO_DIR="$(pwd)"

copy_repo() {
  local dest="$1"
  mkdir -p "$dest"
  tar --exclude=bin --exclude=obj --exclude=.git -cf - -C "$REPO_DIR" . | tar -xf - -C "$dest"
}

mutate_random_in_domain() {
  cat >> src/LivingWorld.Domain/MutantRandom.cs << 'EOF'
namespace LivingWorld.Domain;
internal static class MutantRandom { internal static readonly System.Random R = new System.Random(); }
EOF
}

mutate_inverted_assert() {
  cat >> tests/LivingWorld.Tests.Unit/MutantInvertedAssertTests.cs << 'EOF'
using Xunit;
namespace LivingWorld.Tests;
public class MutantInvertedAssertTests { [Fact] public void Mutant() => Assert.True(false); }
EOF
}

mutate_long_markdown() {
  { echo "# mutante"; for _ in $(seq 1 200); do echo "linha de enchimento"; done; } > docs/MUTANT-200-LINHAS.md
}

run_mutant() {
  local name="$1" mutate_fn="$2" tmp
  tmp="$(mktemp -d)"
  copy_repo "$tmp"
  ( cd "$tmp" && "$mutate_fn" )

  if ( cd "$tmp" && bash scripts/verify.sh > /tmp/verify-mutation-"$name".log 2>&1 ); then
    echo "verify-mutation: FALHOU — mutante '$name' não foi pego (verify.sh saiu 0)" >&2
    rm -rf "$tmp"
    return 1
  fi
  echo "verify-mutation: OK — mutante '$name' reprovado como esperado"
  rm -rf "$tmp"
  return 0
}

fail=0
run_mutant "random-em-domain" mutate_random_in_domain || fail=1
run_mutant "assert-invertido" mutate_inverted_assert || fail=1
run_mutant "md-200-linhas" mutate_long_markdown || fail=1

if [ "$fail" = 0 ]; then
  echo "verify-mutation: OK — os 3 mutantes foram pegos, o gate reprova de verdade"
else
  echo "verify-mutation: reprovado — pelo menos 1 mutante passou despercebido pelo gate" >&2
fi
exit "$fail"
