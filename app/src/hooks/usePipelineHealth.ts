import { useCallback } from "react";
import { BUILD_ROLE } from "@/config/build-role";
import { useBackendQuery } from "@/hooks/useBackendQuery";
import { agentClient } from "@/lib/agent-client";
import { backendClient } from "@/lib/backend-client";
import type {
  ConnectionHealth,
  DashboardEdgePipelineHealth,
  PipelineHealthStatus,
} from "@/types";

export type PipelineStageKey = "collector" | "uploader" | "ingest" | "redis" | "writer" | "tdengine";

export interface PipelineStageHealth {
  key: PipelineStageKey;
  status: PipelineHealthStatus;
  detail: string;
  description: string;
}

export interface PipelineHealthSnapshot {
  stages: PipelineStageHealth[];
  generatedAt: string;
}

function inactiveStage(key: PipelineStageKey): PipelineStageHealth {
  return { key, status: "inactive", detail: "不适用", description: "当前安装角色不使用该模块" };
}

function processStage(
  key: PipelineStageKey,
  result: PromiseSettledResult<{ status: string }>,
  applicable: boolean,
): PipelineStageHealth {
  if (!applicable) return inactiveStage(key);
  if (result.status === "rejected") {
    return { key, status: "warning", detail: "状态未知", description: "中心 Agent 暂时不可达" };
  }
  const status = result.value.status.trim().toLowerCase();
  if (status === "running") {
    return { key, status: "healthy", detail: "运行中", description: "模块进程运行正常" };
  }
  if (status === "stopped" || status === "crashed") {
    return {
      key,
      status: "critical",
      detail: status === "crashed" ? "异常退出" : "已停止",
      description: status === "crashed" ? "模块进程异常退出" : "模块进程当前已停止",
    };
  }
  return { key, status: "warning", detail: "状态未知", description: `Agent 返回状态：${status || "unknown"}` };
}

function connectionStage(
  key: "redis" | "tdengine",
  health: ConnectionHealth | null,
): PipelineStageHealth {
  const item = health?.[key];
  if (!item || item.status === "unknown" || item.status === "checking") {
    return { key, status: "warning", detail: "检测中", description: item?.message || "数据库健康状态暂不可用" };
  }
  if (item.status === "connected") {
    const latency = item.latencyMs == null ? "已连接" : `${item.latencyMs}ms`;
    return { key, status: "healthy", detail: latency, description: "实际查询检测通过" };
  }
  return { key, status: "critical", detail: "不可用", description: item.message || "实际查询检测失败" };
}

function edgeStage(
  key: "collector" | "uploader",
  data: DashboardEdgePipelineHealth,
): PipelineStageHealth {
  const item = data[key];
  if (item.totalNodes === 0) {
    return { key, status: "unknown", detail: "暂无节点", description: "暂无已审批边缘节点" };
  }
  const attention = item.warningNodes + item.criticalNodes;
  const description = item.status === "healthy"
    ? `${item.totalNodes} 个边缘节点全部正常`
    : `${item.healthyNodes}/${item.totalNodes} 正常，${attention} 个节点需关注`;
  return {
    key,
    status: item.status,
    detail: `${item.healthyNodes}/${item.totalNodes} 正常`,
    description,
  };
}

function unavailableEdgeStage(key: "collector" | "uploader"): PipelineStageHealth {
  return {
    key,
    status: "warning",
    detail: "状态未知",
    description: "边缘节点聚合状态暂不可用",
  };
}

export function usePipelineHealth() {
  const load = useCallback(async (token: string): Promise<PipelineHealthSnapshot> => {
    const edgeRequest = BUILD_ROLE === "distributed-center"
      ? backendClient.dashboardEdgePipelineHealth(token)
      : Promise.resolve<DashboardEdgePipelineHealth | null>(null);
    const inactiveProcess = { status: "inactive" };
    const collectorRequest = BUILD_ROLE === "centralized"
      ? agentClient.getProcessStatus("collector")
      : Promise.resolve(inactiveProcess);
    const ingestRequest = BUILD_ROLE === "distributed-center"
      ? agentClient.getProcessStatus("ingest")
      : Promise.resolve(inactiveProcess);
    const [edgeResult, connectionResult, collectorResult, ingestResult, writerResult] =
      await Promise.allSettled([
        edgeRequest,
        agentClient.getConnectionHealth(),
        collectorRequest,
        ingestRequest,
        agentClient.getProcessStatus("writer"),
      ]);

    if (BUILD_ROLE === "distributed-center" && edgeResult.status === "rejected") {
      throw edgeResult.reason;
    }
    const edgeHealth = edgeResult.status === "fulfilled" ? edgeResult.value : null;
    const connectionHealth = connectionResult.status === "fulfilled" ? connectionResult.value : null;
    const collector = BUILD_ROLE === "distributed-center"
      ? (edgeHealth ? edgeStage("collector", edgeHealth) : unavailableEdgeStage("collector"))
      : processStage("collector", collectorResult, BUILD_ROLE === "centralized");
    const uploader = BUILD_ROLE === "distributed-center"
      ? (edgeHealth ? edgeStage("uploader", edgeHealth) : unavailableEdgeStage("uploader"))
      : inactiveStage("uploader");

    return {
      stages: [
        collector,
        uploader,
        processStage("ingest", ingestResult, BUILD_ROLE === "distributed-center"),
        connectionStage("redis", connectionHealth),
        processStage("writer", writerResult, BUILD_ROLE !== "edge"),
        connectionStage("tdengine", connectionHealth),
      ],
      generatedAt: new Date().toISOString(),
    };
  }, []);

  return useBackendQuery(load, { intervalMs: 10_000 });
}

