import { useCallback, useEffect, useRef, useState } from "react";
import { BUILD_ROLE } from "@/config/build-role";
import { agentClient } from "@/lib/agent-client";
import { getAgentAuthToken } from "@/lib/agent-auth-token";
import { getBackendWebSocketUrl } from "@/lib/backend-client";
import { usePageRefresh } from "@/contexts/PageRefreshContext";
import type { AppError } from "@/types";
import type { RealtimeWireValue } from "@/types/device-monitor";

export type RealtimeConnectionStatus =
  | "idle"
  | "connecting"
  | "connected"
  | "disconnected"
  | "error";

interface RealtimeServerMessage {
  type?: string;
  message?: string;
  code?: number;
  device_code?: string;
  group_code?: string;
  values?: Record<string, RealtimeWireValue>;
  tdengineConnected?: boolean | null;
  deviceOffline?: boolean;
}

interface RealtimeWebSocketOptions {
  enabled: boolean;
  token: string;
  deviceCode: string;
  groupCode: string;
  flushIntervalMs: number;
}

export function useRealtimeWebSocket({
  enabled,
  token,
  deviceCode,
  groupCode,
  flushIntervalMs,
}: RealtimeWebSocketOptions) {
  const { refreshVersion } = usePageRefresh();
  const [values, setValues] = useState<Record<string, RealtimeWireValue>>({});
  const [status, setStatus] = useState<RealtimeConnectionStatus>("idle");
  const [source, setSource] = useState<"center" | "local">("center");
  const [deviceOffline, setDeviceOffline] = useState(false);
  const [tdengineConnected, setTdengineConnected] = useState<boolean | null>(null);
  const [lastSyncTime, setLastSyncTime] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const pendingRef = useRef<Record<string, RealtimeWireValue>>({});

  const flushPending = useCallback(() => {
    const pending = pendingRef.current;
    if (Object.keys(pending).length === 0) return;
    pendingRef.current = {};
    setValues((current) => ({ ...current, ...pending }));
    setLastSyncTime(Date.now());
  }, []);

  const clearValues = useCallback(() => {
    pendingRef.current = {};
    setValues({});
    setLastSyncTime(null);
    setDeviceOffline(false);
  }, []);

  useEffect(() => {
    if (!enabled) return;
    const timer = window.setInterval(flushPending, Math.max(100, flushIntervalMs));
    return () => {
      window.clearInterval(timer);
      flushPending();
    };
  }, [enabled, flushIntervalMs, flushPending]);

  useEffect(() => {
    if (!enabled || !token || !deviceCode) {
      setStatus("idle");
      return;
    }

    let socket: WebSocket | null = null;
    let reconnectTimer: number | null = null;
    let heartbeatTimer: number | null = null;
    let localPollTimer: number | null = null;
    let reconnectAttempts = 0;
    let disposed = false;
    let authRejected = false;
    const edgeRole = BUILD_ROLE === "edge";
    const localOnly = edgeRole && token === getAgentAuthToken();

    const stopLocalPolling = () => {
      if (localPollTimer !== null) window.clearInterval(localPollTimer);
      localPollTimer = null;
    };

    const pollLocal = async () => {
      try {
        const snapshot = await agentClient.getLocalRealtimeLatest(deviceCode, groupCode);
        if (disposed) return;
        pendingRef.current = {};
        setValues(snapshot.values ?? {});
        setDeviceOffline(snapshot.deviceOffline);
        setTdengineConnected(false);
        setLastSyncTime(Date.now());
        setStatus("connected");
        setSource("local");
        setError(null);
      } catch (reason) {
        if (disposed) return;
        const appError = reason as AppError;
        setStatus("disconnected");
        setError(appError?.type === "unauthorized" ? "本机会话已失效" : "本机实时数据读取失败");
      }
    };

    const startLocalPolling = () => {
      if (!edgeRole || localPollTimer !== null || authRejected) return;
      setSource("local");
      void pollLocal();
      localPollTimer = window.setInterval(() => void pollLocal(), Math.max(100, flushIntervalMs));
    };

    if (localOnly) {
      startLocalPolling();
      return () => {
        disposed = true;
        stopLocalPolling();
      };
    }

    if (import.meta.env.MODE !== "production" && import.meta.env.VITE_USE_MOCK === "true") {
      setStatus("connected");
      setSource("center");
      setDeviceOffline(false);
      setError(null);
      return;
    }

    const clearTimers = () => {
      if (reconnectTimer !== null) window.clearTimeout(reconnectTimer);
      if (heartbeatTimer !== null) window.clearInterval(heartbeatTimer);
      reconnectTimer = null;
      heartbeatTimer = null;
      stopLocalPolling();
    };

    const send = (message: object) => {
      if (socket?.readyState === WebSocket.OPEN) socket.send(JSON.stringify(message));
    };

    const scheduleReconnect = () => {
      if (disposed || authRejected || reconnectTimer !== null) return;
      const delay = Math.min(30_000, 1_000 * 2 ** reconnectAttempts);
      reconnectAttempts += 1;
      reconnectTimer = window.setTimeout(() => {
        reconnectTimer = null;
        connect();
      }, delay);
    };

    function connect() {
      if (disposed || socket?.readyState === WebSocket.CONNECTING || socket?.readyState === WebSocket.OPEN) return;
      setStatus("connecting");
      setError(null);
      try {
        socket = new WebSocket(getBackendWebSocketUrl());
      } catch {
        setStatus("disconnected");
        startLocalPolling();
        scheduleReconnect();
        return;
      }

      socket.onopen = () => {
        reconnectAttempts = 0;
        stopLocalPolling();
        setStatus("connected");
        setSource("center");
        setTdengineConnected(null);
        send({ type: "auth", token });
        send({ action: "subscribe", device_code: deviceCode, group_code: groupCode });
        heartbeatTimer = window.setInterval(() => send({ action: "ping" }), 15_000);
      };

      socket.onmessage = (event) => {
        if (event.data === "pong") return;
        let message: RealtimeServerMessage;
        try {
          message = JSON.parse(String(event.data)) as RealtimeServerMessage;
        } catch {
          return;
        }
        if (message.device_code && message.device_code !== deviceCode) return;
        if (message.group_code && message.group_code !== groupCode) return;
        if (message.type === "error") {
          authRejected = message.code === 401 || message.code === 403;
          setError(message.message ?? "实时订阅失败");
          setStatus("error");
          if (authRejected) stopLocalPolling();
          return;
        }
        if (message.type === "offline") {
          pendingRef.current = {};
          setValues({});
          setDeviceOffline(true);
          return;
        }
        if (message.type === "snapshot") {
          pendingRef.current = {};
          setValues(message.values ?? {});
          setTdengineConnected(message.tdengineConnected ?? null);
          setDeviceOffline(message.deviceOffline ?? false);
          setLastSyncTime(Date.now());
          return;
        }
        if (message.type === "diff" && message.values) {
          Object.assign(pendingRef.current, message.values);
          setDeviceOffline(false);
        }
      };

      socket.onerror = () => setStatus("disconnected");
      socket.onclose = () => {
        socket = null;
        if (heartbeatTimer !== null) window.clearInterval(heartbeatTimer);
        heartbeatTimer = null;
        if (!disposed && !authRejected) {
          startLocalPolling();
          setStatus("disconnected");
          scheduleReconnect();
        }
      };
    }

    const handleVisibilityChange = () => {
      if (!document.hidden && socket?.readyState !== WebSocket.OPEN) connect();
    };

    connect();
    document.addEventListener("visibilitychange", handleVisibilityChange);
    return () => {
      disposed = true;
      document.removeEventListener("visibilitychange", handleVisibilityChange);
      clearTimers();
      if (socket?.readyState === WebSocket.OPEN) {
        send({ action: "unsubscribe", device_code: deviceCode, group_code: groupCode });
      }
      socket?.close();
      socket = null;
    };
  }, [deviceCode, enabled, flushIntervalMs, groupCode, refreshVersion, token]);

  return {
    values,
    status,
    source,
    deviceOffline,
    tdengineConnected,
    lastSyncTime,
    error,
    clearValues,
  };
}