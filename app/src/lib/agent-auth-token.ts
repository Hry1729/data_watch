/** Agent 鉴权 token 独立存放，避免 AuthContext 依赖 agent-client 引发 HMR 后 Context 失配 */

let currentAgentToken: string | null = null;

export function setAgentAuthToken(token: string | null) {
  currentAgentToken = token;
}

export function getAgentAuthToken() {
  return currentAgentToken;
}
