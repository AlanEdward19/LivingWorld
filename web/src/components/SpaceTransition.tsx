// Fase 15.1, T14: transição visual contínua entre espaços (VTT2-09; master prompt §6/§37) —
// fade+zoom via CSS (`styles/global.css`), nunca uma troca abrupta de tela. A troca de `key`
// força o React a desmontar/remontar o conteúdo quando o espaço muda, o que reinicia a animação
// CSS a cada transição.
import type { ReactNode } from "react";

export interface SpaceTransitionProps {
  spaceKey: string;
  children: ReactNode;
}

export function SpaceTransition({ spaceKey, children }: SpaceTransitionProps) {
  return (
    <div key={spaceKey} className="space-transition">
      {children}
    </div>
  );
}
