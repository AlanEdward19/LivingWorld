import type { ConversationSendOutcome, ConversationStartOutcome } from "../contracts";
import type { ConversationSource } from "../sources";

export class MockConversationSource implements ConversationSource {
  private nextSessionId = 1;
  private readonly active = new Set<number>();

  async start(): Promise<ConversationStartOutcome> {
    const sessionId = this.nextSessionId++;
    this.active.add(sessionId);
    return { accepted: true, sessionId };
  }

  async send(sessionId: number, message: string): Promise<ConversationSendOutcome> {
    if (!this.active.has(sessionId)) return { ok: false, reason: "session-not-found" };
    return { ok: true, turn: { dialogue: `[mock] ${message}`, emotion: "neutral", intent: "none" } };
  }

  async end(sessionId: number): Promise<void> {
    this.active.delete(sessionId);
  }
}
