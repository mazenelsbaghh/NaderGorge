import {
  liveSupportAIDecisionTypes,
  liveSupportAIPaths,
  type LiveSupportAIMode,
  type LiveSupportAIPendingDecisionKind,
} from './live-support-contract';

const decisionCount: 6 = liveSupportAIDecisionTypes.length;
const supportedModes: LiveSupportAIMode[] = ['AiActive', 'AiResolved', 'HumanQueued', 'HumanAssigned'];
const supportedKinds: LiveSupportAIPendingDecisionKind[] = ['Action', 'Handoff', 'AccountCreation', 'Resolution'];

if (decisionCount !== 6 || supportedModes.length !== 4 || supportedKinds.length !== 4) {
  throw new Error('Live-support AI contract parity failed.');
}

if (liveSupportAIPaths.actionConfirm('c', 'd') !== '/live-support/participant/conversations/c/ai/actions/d/confirm') {
  throw new Error('Live-support action path is not stable.');
}
