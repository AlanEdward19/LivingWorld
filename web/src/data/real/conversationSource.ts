import { endConversation, sendConversationMessage, startConversation } from "../../api";
import type { ConversationSource } from "../sources";

export class RealConversationSource implements ConversationSource {
  start(npcId: number) {
    return startConversation(npcId);
  }

  send(sessionId: number, message: string) {
    return sendConversationMessage(sessionId, message);
  }

  end(sessionId: number) {
    return endConversation(sessionId);
  }
}
