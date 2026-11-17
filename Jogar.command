#!/bin/bash
# Living World: duplo clique aqui (macOS) para jogar. Liga o mundo, abre o jogo e, ao fechar o jogo, desliga o que ligou.
# É o mesmo que rodar `bash scripts/play.sh` no Terminal.
cd "$(dirname "$0")" || exit 1
LW_PAUSE_ON_ERROR=1 exec bash scripts/play.sh "$@"
