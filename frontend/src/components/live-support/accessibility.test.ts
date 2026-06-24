import { ParticipantConversation } from "./participant/ParticipantConversation";
import { AIPendingActionCard } from "./participant/AIPendingActionCard";
import { AIHandoffConfirmation } from "./participant/AIHandoffConfirmation";
import { AIGuestVerification } from "./participant/AIGuestVerification";
import { AISecureRegistrationForm } from "./participant/AISecureRegistrationForm";
import { StaffConversationWorkspace } from "./staff/StaffConversationWorkspace";
import { AIHandoffSummary } from "./staff/AIHandoffSummary";

export function assertAccessibilityContract() {
  if (!ParticipantConversation || !AIPendingActionCard || !AIHandoffConfirmation || !AIGuestVerification || !AISecureRegistrationForm || !StaffConversationWorkspace || !AIHandoffSummary) {
    throw new Error("Accessibility contract: missing components.");
  }
}

assertAccessibilityContract();
